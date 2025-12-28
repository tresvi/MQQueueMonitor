using IBM.WMQ;
using MQQueueMonitor.Models;
using MQQueueMonitor.ConsoleComponents;
using System.Collections;
using Tresvi.CommandParser;
using Tresvi.CommandParser.Exceptions;
using Spectre.Console;


namespace MQQueueMonitordotnet
{
    //dotnet run -- -m "10.6.248.10;1414;CHANNEL1;MQGD" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //dotnet run -- -m "10.6.248.10;1514;CHANNEL1;MQGQ" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //dotnet run -- -m "192.168.0.31;1414;CHANNEL1;MQGD" -q "BNA.CU2.PEDIDO;BNA.CU2.RESPUESTA"
    //TODO: Asegurarse que cuando se cierre la aplicacion con Ctrl+C, se cierren las conexiones corecamente.
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

            if (options.RefreshInterval < CliParameters.MIN_REFRESH_INTERVAL_MS)
            {
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

                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[yellow]Presione Ctrl+C para terminar el proceso...[/]");
                AnsiConsole.MarkupLine($"[yellow]Conectado a manager {options.MqConnection}[/]\n");

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
            List<Panel> panels = new();
            ConsoleHProgressBar2 progressBar = new(40, 63, 88, true);
            
            foreach (var kvp in queueStats)
            {
                string queueName = kvp.Key;
                QueueStatistics stats = kvp.Value;
                
                string progressBarText = progressBar.GenerateMarkup(stats.CurrentDepth, stats.MaxDepth);
                string barColor = progressBar.GetBarColor(stats.CurrentDepth, stats.MaxDepth);

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
                
                panelContent.AddRow("[bold]Uso de cola:[/]", progressBarText);

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
