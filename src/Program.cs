using IBM.WMQ;
using MQQueueMonitor.Models;
using MQQueueMonitor.ConsoleComponents;
using System.Collections;
using Tresvi.CommandParser;
using Tresvi.CommandParser.Exceptions;
using Spectre.Console;


namespace MQQueueMonitor
{
    //dotnet run -- -m "10.6.248.10;1414;CHANNEL1;MQGD" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //dotnet run -- -m "192.168.0.31;1414;CHANNEL1;MQGD" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //TODO: Asegurarse que cuando se cierre la aplicacion con Ctrl+C, se cierren las conexiones corecamente.
    //TODO: Agregar numero de procesos que estan leyendo, GET y que estan escribiendo PUT (queue.OpenInputCount, queue.OpenOutputCount)
    //TODO: Agregar estado de la cola: Si esta pausada o en estado de Quiescing

    internal class Program
    {
        private const int PROGRESS_BAR_SIZE = 40;

        static void Main(string[] args)
        {
            // Configurar codificación UTF-8 para que los spinners Unicode se muestren correctamente
            // Si falla (sistemas muy antiguos), se mantiene la codificación por defecto
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
            catch { }
            
            CliParameters options;
            MQConnection mqConnection = new();
            List<string> queues;

            try
            {
                object verb = CommandLine.Parse(args, typeof(CliParameters));
                options = (CliParameters)verb;
                mqConnection.Load(options.MqConnection);
                queues = MQConnection.ParseQueues(options.QueuesNames);
            }
            catch (CommandParserBaseException ex)
            {
                Console.CursorVisible = true;
                Console.Error.WriteLine($"Error al parsear los argumentos: {ex.Message}");
                Environment.Exit(1);
                return;
            }
            catch (ArgumentException ex)
            {
                Console.CursorVisible = true;
                Console.Error.WriteLine($"Error en parametros Conexion MQ: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            if (options.RefreshInterval < CliParameters.MIN_REFRESH_INTERVAL_MS)
            {
                Console.CursorVisible = true;
                Console.Error.WriteLine($"Error: refreshInterval debe ser mayor o igual que {CliParameters.MIN_REFRESH_INTERVAL_MS}. Valor recibido: {options.RefreshInterval}");
                Environment.Exit(1);
                return;
            }

            var properties = new Hashtable
            {
                { MQC.HOST_NAME_PROPERTY, mqConnection.Ip },
                { MQC.PORT_PROPERTY, mqConnection.Port },
                { MQC.CHANNEL_PROPERTY, mqConnection.Channel }
            };

            MQQueueManager queueMgr = null;
            Dictionary<string, MQQueue> openQueues = new();
            
            // Configurar manejador para Ctrl+C para restaurar el cursor
            ConsoleCancelEventHandler cancelHandler = (sender, e) =>
            {
                Console.CursorVisible = true;
                e.Cancel = false; // Permitir la terminación normal
            };
            Console.CancelKeyPress += cancelHandler;
            
            try
            {
                AnsiConsole.MarkupLine($"[yellow]Conectandose a manager {options.MqConnection}...[/]\n");
                queueMgr = new MQQueueManager(mqConnection.ManagerName, properties);

                // Inicializar estadísticas por cola y abrir colas para consulta (INQUIRE)
                Dictionary<string, QueueStatistics> queueStats = [];
                
                int openOptions = MQC.MQOO_INQUIRE | MQC.MQOO_FAIL_IF_QUIESCING;
                
                foreach (string queueName in queues)
                {
                    MQQueue queue = queueMgr.AccessQueue(queueName, openOptions);
                    openQueues[queueName] = queue;
                    
                    int maxDepth = queue.MaximumDepth;
                    queueStats[queueName] = new QueueStatistics(queueName, maxDepth);
                }

/*
                // Energetic
                AnsiConsole.Status()
                    //.Spinner(Spinner.Known.BouncingBar)
                    .Spinner(Spinner.Known.Clock)
                    .SpinnerStyle(Style.Parse("yellow"))
                    .Start("DDDDDD...", ctx =>
                    {
                        Thread.Sleep(2000);
                    });
*/
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[yellow]Presione Ctrl+C para terminar el proceso...[/]");
                AnsiConsole.MarkupLine($"[yellow]Conectado a manager {options.MqConnection}[/]\n");

                // Usar Live display de Spectre.Console para actualizar en tiempo real
                AnsiConsole.Live(DisplayHelper.DrawScreen(queueStats))
                    .AutoClear(false)
                    .Overflow(VerticalOverflow.Ellipsis)
                    .Start(ctx =>
                    {
                        while (true)
                        {
                            foreach (string queueName in queues)
                            {
                                MQQueue queue = openQueues[queueName];
                                int depth = queue.CurrentDepth;
                                int openInputCount = queue.OpenInputCount;
                                int openOutputCount = queue.OpenOutputCount;
                                (bool? isGetInhibited, bool? isPutInhibited) = GetInhibitionState(queue);

                                QueueStatistics stats = queueStats[queueName];
                                stats.Update(depth, openInputCount, openOutputCount, isGetInhibited, isPutInhibited);
                            }

                            ctx.UpdateTarget(DisplayHelper.DrawScreen(queueStats));
                            Thread.Sleep(options.RefreshInterval);
                        }
                    });
            }
            catch (MQException ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"Reason = {ex.ReasonCode} Msg= {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            finally
            {
                try { AnsiConsole.Reset(); }
                catch { }
                
                // Cerrar todas las colas abiertas
                foreach (var queue in openQueues.Values)
                {
                    try { queue?.Close(); }
                    catch (Exception ex) { Console.Error.WriteLine($"Error al cerrar cola: {ex.Message}"); }
                }
                
                try { queueMgr?.Disconnect(); }
                catch (Exception ex) { Console.Error.WriteLine($"Error al desconectar Queue Manager: {ex.Message}"); }
            }
        }


        private static (bool? isGetInhibited, bool? isPutInhibited) GetInhibitionState(MQQueue queue)
        { 
            int[] intAttrs = [MQC.MQIA_INHIBIT_PUT, MQC.MQIA_INHIBIT_GET];

            try
            {
                int[] intVals = new int[2];
                queue.Inquire(intAttrs, intVals, null);

                bool? isPutInhibited = (intVals[0] == MQC.MQQA_PUT_INHIBITED);
                bool? isGetInhibited = (intVals[1] == MQC.MQQA_GET_INHIBITED);
                return (isGetInhibited, isPutInhibited);
            }
            catch (MQException)
            {
                // Si falla la consulta, mantener el valor anterior
                return (null, null);
            }
        }



    }
}
