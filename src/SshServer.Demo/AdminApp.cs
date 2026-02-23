using Spectre.Console;

namespace SshServer.Demo;

/// <summary>
/// Admin console application for server administration.
/// </summary>
public class AdminApp : SshShellApplication
{
    private static readonly List<string> _recentLogs = [];
    private static int _connectionCount = 0;

    protected override string Prompt => "[red]admin[/]> ";

    protected override IEnumerable<string> Completions =>
        ["help", "users", "logs", "config", "restart", "shutdown", "clear", "menu", "exit"];

    protected override void OnConnect()
    {
        Interlocked.Increment(ref _connectionCount);
    }

    protected override void OnDisconnect()
    {
        Interlocked.Decrement(ref _connectionCount);
    }

    protected override void OnWelcome()
    {
        WriteLine("[bold red]Admin Console[/]");
        WriteLine("[dim]Type 'help' for commands. Type 'menu' to return to app selection.[/]");
        WriteLine();
    }

    protected override string? OnExec(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";

        return parts[0].ToLowerInvariant() switch
        {
            "users" => $"Active connections: {_connectionCount}\n",
            "logs" => string.Join("\n", GetRecentLogs()) + "\n",
            "config" => GetConfigSummary(),
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

            case "users":
                ShowUsers();
                break;

            case "logs":
                ShowLogs(parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 10);
                break;

            case "config":
                ShowConfig();
                break;

            case "restart":
                SimulateRestart();
                break;

            case "shutdown":
                return ConfirmShutdown();

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
            .AddColumn("[red]Command[/]")
            .AddColumn("[red]Description[/]");

        table.AddRow("help", "Show this help message");
        table.AddRow("users", "Show active connections");
        table.AddRow("logs [n]", "Show recent log entries (default: 10)");
        table.AddRow("config", "Show server configuration");
        table.AddRow("restart", "Simulate server restart");
        table.AddRow("shutdown", "Simulate server shutdown");
        table.AddRow("clear", "Clear the screen");
        table.AddRow("menu", "Return to application menu");
        table.AddRow("exit", "Disconnect from admin console");

        Write(table);
    }

    private void ShowUsers()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Active Connections", $"[green]{_connectionCount}[/]");
        table.AddRow("Max Connections", Options.MaxConnections.ToString());
        table.AddRow("Your Connection ID", $"[yellow]{Connection.ConnectionId}[/]");
        table.AddRow("Your Username", $"[blue]{Escape(Connection.Username)}[/]");

        Write(table);
    }

    private void ShowLogs(int count)
    {
        var logs = GetRecentLogs().TakeLast(count).ToList();

        if (logs.Count == 0)
        {
            WriteLine("[dim]No log entries available.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Time")
            .AddColumn("Level")
            .AddColumn("Message");

        foreach (var log in logs)
        {
            var parts = log.Split('|');
            if (parts.Length >= 3)
            {
                var levelColor = parts[1].Trim() switch
                {
                    "ERROR" => "red",
                    "WARN" => "yellow",
                    "INFO" => "green",
                    _ => "dim"
                };
                table.AddRow(parts[0], $"[{levelColor}]{parts[1]}[/]", parts[2]);
            }
        }

        Write(table);
    }

    private void ShowConfig()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[red]Setting[/]")
            .AddColumn("[red]Value[/]");

        table.AddRow("Port", Options.Port.ToString());
        table.AddRow("Banner", Escape(Options.Banner));
        table.AddRow("Host Key", Escape(Options.HostKeyPath));
        table.AddRow("Max Connections", Options.MaxConnections.ToString());
        table.AddRow("Log Level", Options.LogLevel);
        table.AddRow("Allow Anonymous", Options.AllowAnonymous ? "[green]Yes[/]" : "[red]No[/]");
        table.AddRow("Session Timeout", Options.SessionTimeoutMinutes == 0
            ? "[dim]Disabled[/]"
            : $"{Options.SessionTimeoutMinutes} min");

        Write(table);
    }

    private void SimulateRestart()
    {
        if (!Confirm("[yellow]Simulate server restart?[/]", false))
        {
            WriteLine("[dim]Cancelled.[/]");
            return;
        }

        Status("[yellow]Restarting server...[/]", () =>
        {
            Thread.Sleep(500);
            AddLog("INFO", "Shutdown initiated by admin");
            Thread.Sleep(500);
            AddLog("INFO", "Stopping all connections...");
            Thread.Sleep(500);
            AddLog("INFO", "Server stopped");
            Thread.Sleep(500);
            AddLog("INFO", "Starting server...");
            Thread.Sleep(500);
            AddLog("INFO", "Server started successfully");
        });

        WriteLine("[green]Server restart simulated.[/]");
    }

    private bool ConfirmShutdown()
    {
        if (!Confirm("[red]Simulate server shutdown? This will disconnect you.[/]", false))
        {
            WriteLine("[dim]Cancelled.[/]");
            return true;
        }

        AddLog("WARN", $"Shutdown initiated by {Connection.Username}");
        WriteLine("[red]Server shutdown simulated. Disconnecting...[/]");
        Thread.Sleep(500);
        return false;
    }

    private static List<string> GetRecentLogs()
    {
        lock (_recentLogs)
        {
            if (_recentLogs.Count == 0)
            {
                // Seed with some fake logs
                var now = DateTime.Now;
                _recentLogs.Add($"{now.AddMinutes(-30):HH:mm:ss}|INFO|Server started on port 2222");
                _recentLogs.Add($"{now.AddMinutes(-25):HH:mm:ss}|INFO|Host key loaded: ecdsa-sha2-nistp256");
                _recentLogs.Add($"{now.AddMinutes(-20):HH:mm:ss}|INFO|Anonymous authentication enabled");
                _recentLogs.Add($"{now.AddMinutes(-15):HH:mm:ss}|DEBUG|Connection accepted from 127.0.0.1");
                _recentLogs.Add($"{now.AddMinutes(-10):HH:mm:ss}|INFO|User 'demo' authenticated");
                _recentLogs.Add($"{now.AddMinutes(-5):HH:mm:ss}|DEBUG|PTY allocated: xterm-256color 120x40");
            }
            return [.. _recentLogs];
        }
    }

    private static void AddLog(string level, string message)
    {
        lock (_recentLogs)
        {
            _recentLogs.Add($"{DateTime.Now:HH:mm:ss}|{level}|{message}");
            if (_recentLogs.Count > 100)
                _recentLogs.RemoveAt(0);
        }
    }

    private string GetConfigSummary()
    {
        return $"""
            Port: {Options.Port}
            Banner: {Options.Banner}
            MaxConnections: {Options.MaxConnections}
            AllowAnonymous: {Options.AllowAnonymous}
            LogLevel: {Options.LogLevel}
            """;
    }

    private static string GetHelpText()
    {
        return """
            Admin Console Commands:
              users     - Show active connections
              logs      - Show recent log entries
              config    - Show server configuration
              help      - Show this help
            """;
    }
}
