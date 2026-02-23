using Spectre.Console;

namespace SshServer.Demo;

/// <summary>
/// Demo SSH shell application showcasing Spectre.Console features.
/// Use this as a reference for building your own SSH applications.
/// </summary>
public class DemoApp : SshShellApplication
{
    protected override string Prompt => "demo> ";

    protected override IEnumerable<string> Completions =>
    [
        "help", "status", "whoami", "config", "clear",
        "menu", "select", "multi", "confirm", "ask", "demo",
        "progress", "spinner", "live", "tree", "chart",
        "quit", "exit"
    ];

    protected override void OnWelcome()
    {
        Console.Write(new Rule("[green]Welcome to SshServer[/]").RuleStyle("dim"));
        WriteLine("Type [blue]help[/] for available commands.");
    }

    protected override string? OnExec(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "";

        return parts[0].ToLowerInvariant() switch
        {
            "status" => GetStatusText(),
            "whoami" => GetWhoamiText(),
            "config" => GetConfigText(),
            "help" or "?" => GetHelpText(),
            _ => $"Unknown command: {parts[0]}\nAvailable commands: help, status, whoami, config\n"
        };
    }

    private string GetStatusText()
    {
        var processId = Environment.ProcessId;
        return $"Server is running (PID {processId})\n";
    }

    private string GetWhoamiText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Connection ID: {Connection.ConnectionId}");
        sb.AppendLine($"Username: {Connection.Username}");
        sb.AppendLine($"Auth Method: {Connection.AuthMethod}");
        if (Connection.KeyFingerprint != null)
            sb.AppendLine($"Key Fingerprint: {Connection.KeyFingerprint}");
        return sb.ToString();
    }

    private string GetConfigText()
    {
        var sb = new System.Text.StringBuilder();
        var hostname = System.Net.Dns.GetHostName();
        var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        sb.AppendLine($"Hostname: {hostname}");
        sb.AppendLine($"OS: {osDescription}");
        sb.AppendLine($"Process ID: {Environment.ProcessId}");
        sb.AppendLine($"Port: {Options.Port}");
        sb.AppendLine($"Banner: {Options.Banner}");
        sb.AppendLine($"MaxConnections: {Options.MaxConnections}");
        sb.AppendLine($"AllowAnonymous: {Options.AllowAnonymous}");
        sb.AppendLine($"SessionTimeoutMinutes: {Options.SessionTimeoutMinutes}");
        return sb.ToString();
    }

    private static string GetHelpText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Available commands (exec mode):");
        sb.AppendLine("  help    - Show this help");
        sb.AppendLine("  status  - Show server status");
        sb.AppendLine("  whoami  - Show connection info");
        sb.AppendLine("  config  - Show server configuration");
        return sb.ToString();
    }

    protected override bool OnCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return true;

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
            case "?":
                ShowHelp();
                break;

            case "status":
                ShowStatus();
                break;

            case "whoami":
                ShowWhoami();
                break;

            case "config":
                ShowConfig();
                break;

            case "clear":
            case "cls":
                Clear();
                break;

            case "menu":
                ShowMenu();
                break;

            case "select":
                ShowSelect();
                break;

            case "multi":
                ShowMultiSelect();
                break;

            case "confirm":
                ShowConfirm();
                break;

            case "ask":
                ShowAsk();
                break;

            case "demo":
                ShowDemo();
                break;

            case "progress":
                ShowProgress();
                break;

            case "spinner":
                ShowSpinner();
                break;

            case "live":
                ShowLive();
                break;

            case "tree":
                ShowTree();
                break;

            case "chart":
                ShowBarChart();
                break;

            case "quit":
            case "exit":
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
            .AddColumn("[blue]Command[/]")
            .AddColumn("[blue]Description[/]");

        table.AddRow("help", "Show this help message");
        table.AddRow("status", "Show server status");
        table.AddRow("whoami", "Show connection and auth info");
        table.AddRow("config", "Show server configuration");
        table.AddRow("clear", "Clear the screen");
        table.AddRow("[yellow]menu[/]", "[yellow]Interactive menu selection[/]");
        table.AddRow("[yellow]select[/]", "[yellow]Select from a list[/]");
        table.AddRow("[yellow]multi[/]", "[yellow]Multi-select from a list[/]");
        table.AddRow("[yellow]confirm[/]", "[yellow]Yes/No confirmation[/]");
        table.AddRow("[yellow]ask[/]", "[yellow]Text input prompt[/]");
        table.AddRow("[yellow]demo[/]", "[yellow]Run all interactive demos[/]");
        table.AddRow("[green]progress[/]", "[green]Progress bar demo[/]");
        table.AddRow("[green]spinner[/]", "[green]Status spinner demo[/]");
        table.AddRow("[green]live[/]", "[green]Live updating display[/]");
        table.AddRow("[cyan]tree[/]", "[cyan]Hierarchical tree display[/]");
        table.AddRow("[cyan]chart[/]", "[cyan]Bar chart visualization[/]");
        table.AddRow("quit", "Disconnect from server");
        table.AddRow("[dim]Ctrl-C[/]", "[dim]Disconnect (shortcut)[/]");

        Write(table);
        WriteLine("\n[dim]Yellow = interactive prompts, Green = live displays[/]");
    }

    private void ShowStatus()
    {
        var processId = Environment.ProcessId;
        var panel = new Panel(
            new Markup($"[green]●[/] Server is [green]running[/] [dim](PID {processId})[/]"))
        {
            Header = new PanelHeader("[blue]Server Status[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0),
        };

        Write(panel);
    }

    private void ShowWhoami()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .HideHeaders()
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Connection ID", $"[yellow]{Connection.ConnectionId}[/]");
        table.AddRow("Username", $"[blue]{Escape(Connection.Username)}[/]");

        if (Connection.AuthMethod == "none")
        {
            table.AddRow("Auth Method", "[dim]anonymous[/]");
        }
        else if (Connection.AuthMethod == "publickey")
        {
            table.AddRow("Auth Method", "[green]public key[/]");
            if (Connection.KeyFingerprint != null)
            {
                table.AddRow("Key Fingerprint", $"[dim]{Escape(Connection.KeyFingerprint)}[/]");
            }
        }
        else
        {
            table.AddRow("Auth Method", Escape(Connection.AuthMethod));
        }

        Write(table);
    }

    private void ShowConfig()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[blue]Setting[/]")
            .AddColumn("[blue]Value[/]");

        // Server identity
        var hostname = System.Net.Dns.GetHostName();
        var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var processId = Environment.ProcessId;
        table.AddRow("Hostname", $"[yellow]{Escape(hostname)}[/]");
        table.AddRow("OS", $"[yellow]{Escape(osDescription)}[/]");
        table.AddRow("Process ID", $"[yellow]{processId}[/]");

        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(hostname)
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                            a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                .Select(a => a.ToString());
            table.AddRow("IP Addresses", $"[yellow]{Escape(string.Join(", ", addresses))}[/]");
        }
        catch
        {
            table.AddRow("IP Addresses", "[dim](unavailable)[/]");
        }

        table.AddRow("Port", Options.Port.ToString());
        table.AddRow("Banner", Escape(Options.Banner));
        table.AddRow("HostKeyPath", Escape(Options.HostKeyPath));
        table.AddRow("MaxConnections", Options.MaxConnections.ToString());
        table.AddRow("LogLevel", Options.LogLevel);
        table.AddRow("AllowAnonymous", Options.AllowAnonymous ? "[green]true[/]" : "[red]false[/]");
        table.AddRow("AuthorizedKeysPath", Options.AuthorizedKeysPath ?? "[dim](not set)[/]");
        table.AddRow("SessionTimeoutMinutes", Options.SessionTimeoutMinutes == 0
            ? "[dim]disabled[/]"
            : $"{Options.SessionTimeoutMinutes} min");

        Write(table);
    }

    private void ShowMenu()
    {
        var choice = Select("What would you like to do?",
            ["View server status", "Show connection info", "Clear screen", "Cancel"]);

        switch (choice)
        {
            case "View server status":
                ShowStatus();
                break;
            case "Show connection info":
                ShowWhoami();
                break;
            case "Clear screen":
                Clear();
                break;
            case "Cancel":
                WriteLine("[dim]Cancelled.[/]");
                break;
        }
    }

    private void ShowSelect()
    {
        var fruit = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("What's your [green]favorite fruit[/]?")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more fruits)[/]")
                .AddChoices([
                    "Apple", "Banana", "Orange", "Mango",
                    "Strawberry", "Blueberry", "Grape", "Watermelon",
                    "Pineapple", "Kiwi", "Peach", "Plum"
                ]));

        WriteLine($"You selected: [green]{fruit}[/]");
    }

    private void ShowMultiSelect()
    {
        var toppings = MultiSelect("Select your [green]pizza toppings[/]:",
            ["Pepperoni", "Mushrooms", "Onions", "Sausage",
             "Bacon", "Extra cheese", "Black olives", "Green peppers",
             "Pineapple", "Spinach"]);

        if (toppings.Count == 0)
        {
            WriteLine("You selected: [yellow]Plain cheese pizza[/]");
        }
        else
        {
            WriteLine($"You selected: [green]{string.Join(", ", toppings)}[/]");
        }
    }

    private void ShowConfirm()
    {
        var confirmed = Confirm("Do you want to continue?");

        if (confirmed)
        {
            WriteLine("[green]You confirmed![/]");
        }
        else
        {
            WriteLine("[yellow]You declined.[/]");
        }
    }

    private void ShowAsk()
    {
        var name = Ask("What's your [green]name[/]?");
        WriteLine($"Hello, [blue]{Escape(name)}[/]!");

        var age = Console.Prompt(
            new TextPrompt<int>("What's your [green]age[/]?")
                .ValidationErrorMessage("[red]Please enter a valid number[/]")
                .Validate(age => age switch
                {
                    < 0 => ValidationResult.Error("[red]Age cannot be negative[/]"),
                    > 150 => ValidationResult.Error("[red]That seems unlikely![/]"),
                    _ => ValidationResult.Success(),
                }));

        WriteLine($"You are [blue]{age}[/] years old.");
    }

    private void ShowDemo()
    {
        WriteLine("[bold]Running interactive demo...[/]\n");

        // Selection
        var color = Select("Pick a [green]color[/]:",
            ["Red", "Green", "Blue", "Yellow"]);

        // Multi-selection
        var features = MultiSelect("Select [green]features[/] to enable:",
            ["Logging", "Metrics", "Tracing", "Alerts"]);

        // Confirmation
        var proceed = Confirm("Apply these settings?");

        // Summary
        WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Color", $"[{color.ToLower()}]{color}[/]");
        table.AddRow("Features", string.Join(", ", features));
        table.AddRow("Applied", proceed ? "[green]Yes[/]" : "[red]No[/]");

        Write(table);
    }

    private void ShowProgress()
    {
        Console.Progress()
            .AutoClear(false)
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            ])
            .Start(ctx =>
            {
                var task1 = ctx.AddTask("[green]Downloading files[/]");
                var task2 = ctx.AddTask("[blue]Processing data[/]");
                var task3 = ctx.AddTask("[yellow]Uploading results[/]");

                while (!ctx.IsFinished)
                {
                    task1.Increment(3.5);
                    if (task1.Value > 30)
                        task2.Increment(2.5);
                    if (task2.Value > 50)
                        task3.Increment(4.0);

                    Thread.Sleep(50);
                }
            });

        WriteLine("\n[green]All tasks completed![/]");
    }

    private void ShowSpinner()
    {
        Console.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Processing...[/]", ctx =>
            {
                Thread.Sleep(1000);

                ctx.Status("[yellow]Loading configuration...[/]");
                ctx.Spinner(Spinner.Known.Star);
                Thread.Sleep(1000);

                ctx.Status("[yellow]Connecting to database...[/]");
                ctx.Spinner(Spinner.Known.Aesthetic);
                Thread.Sleep(1000);

                ctx.Status("[yellow]Fetching data...[/]");
                ctx.Spinner(Spinner.Known.Arrow);
                Thread.Sleep(1000);

                ctx.Status("[green]Finalizing...[/]");
                ctx.Spinner(Spinner.Known.Bounce);
                Thread.Sleep(500);
            });

        WriteLine("[green]Done![/]");
    }

    private void ShowLive()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddColumn("Status");

        Console.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                var random = new Random();

                for (int i = 0; i < 10; i++)
                {
                    table.Rows.Clear();

                    var cpu = random.Next(10, 90);
                    var memory = random.Next(30, 80);
                    var connections = random.Next(1, 50);
                    var requests = random.Next(100, 1000);

                    table.AddRow(
                        "CPU Usage",
                        $"{cpu}%",
                        cpu > 70 ? "[red]High[/]" : cpu > 40 ? "[yellow]Normal[/]" : "[green]Low[/]");
                    table.AddRow(
                        "Memory",
                        $"{memory}%",
                        memory > 70 ? "[red]High[/]" : memory > 40 ? "[yellow]Normal[/]" : "[green]Low[/]");
                    table.AddRow(
                        "Connections",
                        connections.ToString(),
                        connections > 30 ? "[yellow]Busy[/]" : "[green]OK[/]");
                    table.AddRow(
                        "Requests/sec",
                        requests.ToString(),
                        "[blue]Active[/]");

                    ctx.Refresh();
                    Thread.Sleep(500);
                }
            });

        WriteLine("\n[dim]Live display finished.[/]");
    }

    private void ShowTree()
    {
        var root = new Tree("[yellow]Project[/]")
            .Style("dim");

        // Source folder
        var src = root.AddNode("[blue]src[/]");
        var host = src.AddNode("[blue]SshServer.Host[/]");
        host.AddNode("[green]Program.cs[/]");
        host.AddNode("[green]SshShellApplication.cs[/]");
        host.AddNode("[green]DemoApp.cs[/]");
        host.AddNode("[green]appsettings.json[/]");
        var tui = host.AddNode("[blue]Tui[/]");
        tui.AddNode("[green]LineEditor.cs[/]");
        tui.AddNode("[green]SshConsoleFactory.cs[/]");
        tui.AddNode("[green]SshTextWriter.cs[/]");

        var core = src.AddNode("[blue]SshServer.Core[/]");
        var ssh = core.AddNode("[blue]Ssh[/]");
        ssh.AddNode("[green]Session.cs[/]");
        ssh.AddNode("[green]Channel.cs[/]");

        // Config files
        var config = root.AddNode("[magenta]Configuration[/]");
        config.AddNode("[dim]README.md[/]");
        config.AddNode("[dim]DEVELOPERS.md[/]");
        config.AddNode("[dim]RELEASE_NOTES.md[/]");

        Write(root);
    }

    private void ShowBarChart()
    {
        Write(new BarChart()
            .Width(60)
            .Label("[green bold underline]Language Popularity[/]")
            .CenterLabel()
            .AddItem("C#", 85, Color.Green)
            .AddItem("Python", 92, Color.Yellow)
            .AddItem("JavaScript", 78, Color.Blue)
            .AddItem("Rust", 45, Color.Red)
            .AddItem("Go", 67, Color.Aqua)
            .AddItem("F#", 28, Color.Magenta1));

        WriteLine();

        Write(new BarChart()
            .Width(60)
            .Label("[blue bold underline]Server Metrics[/]")
            .CenterLabel()
            .AddItem("CPU", 45, Color.Green)
            .AddItem("Memory", 72, Color.Yellow)
            .AddItem("Disk I/O", 30, Color.Blue)
            .AddItem("Network", 58, Color.Aqua));
    }
}
