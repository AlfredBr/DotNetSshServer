using Spectre.Console;

namespace SshServer.Demo;

/// <summary>
/// Monitoring dashboard application for live metrics.
/// </summary>
public class MonitoringApp : SshShellApplication
{
    private static readonly Random _random = new();

    protected override string Prompt => "[cyan]monitor[/]> ";

    protected override IEnumerable<string> Completions =>
        ["help", "metrics", "health", "alerts", "live", "chart", "clear", "menu", "exit"];

    protected override void OnWelcome()
    {
        WriteLine("[bold cyan]Monitoring Dashboard[/]");
        WriteLine("[dim]Type 'help' for commands. Type 'menu' to return to app selection.[/]");
        WriteLine();
    }

    protected override string? OnExec(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";

        return parts[0].ToLowerInvariant() switch
        {
            "health" => GetHealthStatus(),
            "metrics" => GetMetricsSummary(),
            "alerts" => GetAlertsSummary(),
            "help" => GetHelpText(),
            _ => $"Unknown command: {parts[0]}\nUse 'help' for available commands.\n"
        };
    }

    protected override bool OnCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
            case "?":
                ShowHelp();
                break;

            case "metrics":
                ShowMetrics();
                break;

            case "health":
                ShowHealth();
                break;

            case "alerts":
                ShowAlerts();
                break;

            case "live":
                ShowLiveMetrics();
                break;

            case "chart":
                ShowChart();
                break;

            case "clear":
                Clear();
                break;

            case "exit":
            case "quit":
                return false;

            default:
                WriteLine($"[red]Unknown command:[/] {Escape(parts[0])}");
                WriteLine("[dim]Type 'help' for available commands.[/]");
                break;
        }

        return true;
    }

    private void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[cyan]Command[/]")
            .AddColumn("[cyan]Description[/]");

        table.AddRow("help", "Show this help message");
        table.AddRow("metrics", "Show current system metrics");
        table.AddRow("health", "Show system health status");
        table.AddRow("alerts", "Show active alerts");
        table.AddRow("live", "Show live updating metrics (5 seconds)");
        table.AddRow("chart", "Show metrics as bar chart");
        table.AddRow("clear", "Clear the screen");
        table.AddRow("menu", "Return to application menu");
        table.AddRow("exit", "Disconnect from monitoring");

        Write(table);
    }

    private void ShowMetrics()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddColumn("Status");

        var cpu = _random.Next(15, 85);
        var memory = _random.Next(30, 80);
        var disk = _random.Next(20, 70);
        var network = _random.Next(100, 1000);
        var connections = _random.Next(1, 50);
        var requests = _random.Next(500, 5000);

        table.AddRow("CPU Usage", $"{cpu}%", GetStatusIndicator(cpu, 70, 90));
        table.AddRow("Memory Usage", $"{memory}%", GetStatusIndicator(memory, 70, 85));
        table.AddRow("Disk Usage", $"{disk}%", GetStatusIndicator(disk, 80, 95));
        table.AddRow("Network I/O", $"{network} KB/s", "[green]OK[/]");
        table.AddRow("Active Connections", connections.ToString(), GetStatusIndicator(connections, 30, 45));
        table.AddRow("Requests/min", requests.ToString(), "[green]OK[/]");
        table.AddRow("Uptime", FormatUptime(), "[green]OK[/]");

        Write(table);
    }

    private void ShowHealth()
    {
        var panel = new Panel(new Rows(
            new Markup("[green]●[/] SSH Server: [green]Healthy[/]"),
            new Markup("[green]●[/] Authentication: [green]Operational[/]"),
            new Markup("[green]●[/] Channel Manager: [green]Running[/]"),
            new Markup("[yellow]●[/] Session Cleanup: [yellow]Scheduled[/]"),
            new Markup("[green]●[/] Logging: [green]Active[/]")
        ))
        {
            Header = new PanelHeader("[cyan]System Health[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };

        Write(panel);

        WriteLine();
        WriteLine($"[dim]Last check: {DateTime.Now:HH:mm:ss}[/]");
    }

    private void ShowAlerts()
    {
        var alerts = GenerateAlerts();

        if (alerts.Count == 0)
        {
            WriteLine("[green]No active alerts.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Severity")
            .AddColumn("Time")
            .AddColumn("Message");

        foreach (var alert in alerts)
        {
            var severityColor = alert.Severity switch
            {
                "CRITICAL" => "red",
                "WARNING" => "yellow",
                "INFO" => "blue",
                _ => "dim"
            };
            table.AddRow(
                $"[{severityColor}]{alert.Severity}[/]",
                alert.Time.ToString("HH:mm:ss"),
                alert.Message);
        }

        Write(table);
    }

    private void ShowLiveMetrics()
    {
        WriteLine("[dim]Showing live metrics for 5 seconds... Press any key to stop.[/]");
        WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddColumn("Trend");

        Console.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                var prevCpu = 0;
                var prevMem = 0;
                var prevNet = 0;

                for (int i = 0; i < 10; i++)
                {
                    table.Rows.Clear();

                    var cpu = _random.Next(15, 85);
                    var memory = _random.Next(30, 80);
                    var network = _random.Next(100, 1000);
                    var connections = _random.Next(1, 50);

                    table.AddRow("CPU", $"{cpu}%", GetTrend(cpu, prevCpu));
                    table.AddRow("Memory", $"{memory}%", GetTrend(memory, prevMem));
                    table.AddRow("Network", $"{network} KB/s", GetTrend(network, prevNet));
                    table.AddRow("Connections", connections.ToString(), "[dim]-[/]");

                    prevCpu = cpu;
                    prevMem = memory;
                    prevNet = network;

                    ctx.Refresh();
                    Thread.Sleep(500);
                }
            });

        WriteLine();
        WriteLine("[dim]Live metrics stopped.[/]");
    }

    private void ShowChart()
    {
        Write(new BarChart()
            .Width(60)
            .Label("[cyan bold]Current System Metrics[/]")
            .CenterLabel()
            .AddItem("CPU", _random.Next(15, 85), Color.Green)
            .AddItem("Memory", _random.Next(30, 80), Color.Yellow)
            .AddItem("Disk", _random.Next(20, 70), Color.Blue)
            .AddItem("Network", _random.Next(10, 50), Color.Aqua));

        WriteLine();

        Write(new BarChart()
            .Width(60)
            .Label("[cyan bold]Connection Stats[/]")
            .CenterLabel()
            .AddItem("Active", _random.Next(5, 30), Color.Green)
            .AddItem("Idle", _random.Next(2, 15), Color.Yellow)
            .AddItem("Pending", _random.Next(0, 5), Color.Blue));
    }

    private static string GetStatusIndicator(int value, int warnThreshold, int critThreshold)
    {
        if (value >= critThreshold)
            return "[red]CRITICAL[/]";
        if (value >= warnThreshold)
            return "[yellow]WARNING[/]";
        return "[green]OK[/]";
    }

    private static string GetTrend(int current, int previous)
    {
        if (previous == 0) return "[dim]-[/]";
        if (current > previous) return "[red]↑[/]";
        if (current < previous) return "[green]↓[/]";
        return "[dim]→[/]";
    }

    private static string FormatUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (uptime.TotalDays >= 1)
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private static List<(string Severity, DateTime Time, string Message)> GenerateAlerts()
    {
        var alerts = new List<(string, DateTime, string)>();
        var now = DateTime.Now;

        // Randomly generate 0-3 alerts
        var count = _random.Next(0, 4);
        for (int i = 0; i < count; i++)
        {
            var severity = _random.Next(3) switch
            {
                0 => "CRITICAL",
                1 => "WARNING",
                _ => "INFO"
            };

            var message = severity switch
            {
                "CRITICAL" => _random.Next(2) == 0
                    ? "High CPU usage detected"
                    : "Memory threshold exceeded",
                "WARNING" => _random.Next(2) == 0
                    ? "Connection pool near capacity"
                    : "Slow response times detected",
                _ => _random.Next(2) == 0
                    ? "Scheduled maintenance in 2 hours"
                    : "New version available"
            };

            alerts.Add((severity, now.AddMinutes(-_random.Next(1, 60)), message));
        }

        return alerts.OrderByDescending(a => a.Time).ToList();
    }

    private string GetHealthStatus()
    {
        return """
            System Health: OK
            SSH Server: Healthy
            Authentication: Operational
            Uptime: """ + FormatUptime() + "\n";
    }

    private string GetMetricsSummary()
    {
        var cpu = _random.Next(15, 85);
        var memory = _random.Next(30, 80);
        return $"""
            CPU: {cpu}%
            Memory: {memory}%
            Uptime: {FormatUptime()}
            """;
    }

    private static string GetAlertsSummary()
    {
        var alerts = GenerateAlerts();
        if (alerts.Count == 0)
            return "No active alerts.\n";

        return $"Active alerts: {alerts.Count}\n" +
               string.Join("\n", alerts.Select(a => $"  [{a.Severity}] {a.Message}")) + "\n";
    }

    private static string GetHelpText()
    {
        return """
            Monitoring Commands:
              health    - Show system health status
              metrics   - Show current metrics
              alerts    - Show active alerts
              help      - Show this help
            """;
    }
}
