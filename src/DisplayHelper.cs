using MQQueueMonitor.Models;
using MQQueueMonitor.ConsoleComponents;
using Spectre.Console;

namespace MQQueueMonitor;

sealed internal class DisplayHelper
{
    private const int PROGRESS_BAR_SIZE = 40;

    public static Rows DrawScreen(Dictionary<string, QueueStatistics> queueStats)
        {
            List<Panel> panels = new();
            ConsoleHProgressBar2 progressBar = new(PROGRESS_BAR_SIZE, 63, 88, true);
            
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
                panelContent.AddRow("[bold]Consumidores:[/]", $"[bold]{stats.OpenInputCount}[/]");
                panelContent.AddRow("[bold]Productores:[/]", $"[bold]{stats.OpenOutputCount}[/]");

                string putStatus = stats.IsPutInhibited == true ? "[yellow]SI[/]" : "[WHITE]NO[/]";
                string getStatus = stats.IsGetInhibited == true ? "[yellow]SI[/]" : "[WHITE]NO[/]";
                panelContent.AddRow("[bold]Inhibida para PUT/GET:[/]", $"{putStatus}/{getStatus}");
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


        private static string GetFormatedBooleanText(bool? input)
        {
            if (input is null) 
                return "[WHITE]??[/]";
            else if (input == true)
                return "[YELLOW]SI[/]";
            else
                return "[WHITE]NO[/]";
        }
}

