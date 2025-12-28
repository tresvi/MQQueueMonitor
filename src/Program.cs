using IBM.WMQ;
using MQQueueMonitor.Models;
using System.Collections;
using Tresvi.CommandParser;
using Tresvi.CommandParser.Exceptions;
using Spectre.Console;


namespace MQQueueMonitor
{
    //dotnet run -- -m "10.6.248.10;1414;CHANNEL1;MQGD" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //dotnet run -- -m "10.6.248.10;1514;CHANNEL1;MQGQ" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //dotnet run -- -m "192.168.0.31;1414;CHANNEL1;MQGD" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    internal class Program
    {
        static void Main(string[] args)
        {
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
                Console.Error.WriteLine($"Error al parsear los argumentos: {ex.Message}");
                Environment.Exit(1);
                return;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error en parametros Coneccion MQ: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            var properties = new Hashtable
            {
                { MQC.HOST_NAME_PROPERTY, mqConnection.Ip },
                { MQC.PORT_PROPERTY, mqConnection.Port },
                { MQC.CHANNEL_PROPERTY, mqConnection.Channel }
            };

            if (options.RefreshInterval < CliParameters.MIN_REFRESH_INTERVAL_MS)
            {
                Console.Error.WriteLine($"Error: refreshInterval debe ser mayor o igual que {CliParameters.MIN_REFRESH_INTERVAL_MS}. Valor recibido: {options.RefreshInterval}");
                Environment.Exit(1);
                return;
            }

            MQQueueManager queueMgr = null;
            Dictionary<string, MQQueue> openQueues = new();
            
            try
            {
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

                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[yellow]Presione Ctrl+C para terminar el proceso...[/]\n");

                // Usar Live display de Spectre.Console para actualizar en tiempo real
                AnsiConsole.Live(CreateQueueDisplay(queueStats))
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
                                QueueStatistics stats = queueStats[queueName];
                                stats.Update(depth);
                            }

                            ctx.UpdateTarget(CreateQueueDisplay(queueStats));
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
                // Cerrar todas las colas abiertas
                foreach (var queue in openQueues.Values)
                {
                    try
                    {
                        queue?.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error al cerrar cola: {ex.Message}");
                    }
                }
                
                try
                {
                    queueMgr?.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error al desconectar Queue Manager: {ex.Message}");
                }
                
            }
        }

        private static Rows CreateQueueDisplay(Dictionary<string, QueueStatistics> queueStats)
        {
            var panels = new List<Panel>();
            
            foreach (var kvp in queueStats)
            {
                string queueName = kvp.Key;
                QueueStatistics stats = kvp.Value;
                
                // Calcular porcentaje para la barra de progreso
                double percentage = stats.MaxDepth > 0 
                    ? Math.Min(100.0, (double)stats.CurrentDepth / stats.MaxDepth * 100.0) 
                    : 0.0;

                // Determinar color de la barra según umbrales (63% amarillo, 88% rojo)
                string barColor = percentage < 63 ? "green" : (percentage < 88 ? "yellow" : "red");

                // Crear barra de progreso como texto
                int barLength = 40;
                int filledChars = (int)Math.Round(percentage / 100.0 * barLength);
                filledChars = Math.Min(filledChars, barLength);
                string progressBar = $"[{barColor}]{new string('█', filledChars)}[/][dim]{new string('░', barLength - filledChars)}[/]";

                // Crear contenido del panel
                var panelContent = new Table()
                    .HideHeaders()
                    .NoBorder()
                    .AddColumn(new TableColumn("").NoWrap())
                    .AddColumn(new TableColumn("").NoWrap());

                panelContent.AddRow("[bold]Profundidad:[/]", $"[bold]{stats.CurrentDepth}[/]");
                
                string minText = !stats.HasMinDepth 
                    ? "[dim]N/A[/]" 
                    : $"{stats.MinDepth} ([dim]{stats.MinDepthTimestamp:HH:mm:ss.ff}[/])";
                panelContent.AddRow("[bold]Registro mín:[/]", minText);
                
                string maxText = !stats.HasMaxDepthRecorded 
                    ? "[dim]N/A[/]" 
                    : $"{stats.MaxDepthRecorded} ([dim]{stats.MaxDepthTimestamp:HH:mm:ss.ff}[/])";
                panelContent.AddRow("[bold]Registro máx:[/]", maxText);
                
                // Velocidad con color según el signo
                string rateColor = stats.RatePerSecond >= 0 ? "green" : "red";
                panelContent.AddRow("[bold]Velocidad [[msjes/seg]]:[/]", $"[{rateColor}]{stats.RatePerSecond:F2}[/]");
                
                // Agregar barra de progreso
                panelContent.AddRow("[bold]Progreso:[/]", progressBar);
                panelContent.AddRow("", $"[dim]{percentage:F1}%[/]");

                // Crear panel con título
                Color borderColor = barColor == "green" ? Color.Green : (barColor == "yellow" ? Color.Yellow : Color.Red);
                var panel = new Panel(panelContent)
                {
                    Header = new PanelHeader($"[bold green]Cola {queueName}[/] (Prof. Máxima: {stats.MaxDepth})"),
                    Border = BoxBorder.Rounded
                };
                panel.BorderStyle = new Style(borderColor);

                panels.Add(panel);
            }
            
            return new Rows(panels);
        }
    }
}
