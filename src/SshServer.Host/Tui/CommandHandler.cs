using Spectre.Console;

namespace SshServer.Host.Tui;

/// <summary>
/// Processes shell commands and renders output using Spectre.Console.
/// </summary>
public class CommandHandler
{
    private readonly IAnsiConsole _console;
    private readonly string _connId;

    public CommandHandler(IAnsiConsole console, string connId)
    {
        _console = console;
        _connId = connId;
    }

    /// <summary>
    /// Execute a command and render output.
    /// </summary>
    /// <returns>True if the session should continue, false to disconnect.</returns>
    public bool Execute(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return true;

        try
        {
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
                    _console.MarkupLine($"Connection ID: [yellow]{_connId}[/]");
                    break;

                case "clear":
                case "cls":
                    _console.Clear();
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
                    _console.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(parts[0])}");
                    _console.MarkupLine("[dim]Type 'help' for available commands.[/]");
                    break;
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
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
        table.AddRow("whoami", "Show your connection ID");
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

        _console.Write(table);
        _console.MarkupLine("\n[dim]Yellow = interactive prompts, Green = live displays[/]");
    }

    private void ShowStatus()
    {
        var panel = new Panel(
            new Markup("[green]●[/] Server is [green]running[/]"))
        {
            Header = new PanelHeader("[blue]Server Status[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0),
        };

        _console.Write(panel);
    }

    private void ShowMenu()
    {
        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .PageSize(5)
                .AddChoices([
                    "View server status",
                    "Show connection info",
                    "Clear screen",
                    "Cancel"
                ]));

        switch (choice)
        {
            case "View server status":
                ShowStatus();
                break;
            case "Show connection info":
                _console.MarkupLine($"Connection ID: [yellow]{_connId}[/]");
                break;
            case "Clear screen":
                _console.Clear();
                break;
            case "Cancel":
                _console.MarkupLine("[dim]Cancelled.[/]");
                break;
        }
    }

    private void ShowSelect()
    {
        var fruit = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What's your [green]favorite fruit[/]?")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more fruits)[/]")
                .AddChoices([
                    "Apple", "Banana", "Orange", "Mango",
                    "Strawberry", "Blueberry", "Grape", "Watermelon",
                    "Pineapple", "Kiwi", "Peach", "Plum"
                ]));

        _console.MarkupLine($"You selected: [green]{fruit}[/]");
    }

    private void ShowMultiSelect()
    {
        var toppings = _console.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select your [green]pizza toppings[/]:")
                .PageSize(8)
                .Required(false)
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                .AddChoices([
                    "Pepperoni", "Mushrooms", "Onions", "Sausage",
                    "Bacon", "Extra cheese", "Black olives", "Green peppers",
                    "Pineapple", "Spinach"
                ]));

        if (toppings.Count == 0)
        {
            _console.MarkupLine("You selected: [yellow]Plain cheese pizza[/]");
        }
        else
        {
            _console.MarkupLine($"You selected: [green]{string.Join(", ", toppings)}[/]");
        }
    }

    private void ShowConfirm()
    {
        var confirmed = _console.Confirm("Do you want to continue?");

        if (confirmed)
        {
            _console.MarkupLine("[green]You confirmed![/]");
        }
        else
        {
            _console.MarkupLine("[yellow]You declined.[/]");
        }
    }

    private void ShowAsk()
    {
        var name = _console.Ask<string>("What's your [green]name[/]?");
        _console.MarkupLine($"Hello, [blue]{Markup.Escape(name)}[/]!");

        var age = _console.Prompt(
            new TextPrompt<int>("What's your [green]age[/]?")
                .ValidationErrorMessage("[red]Please enter a valid number[/]")
                .Validate(age => age switch
                {
                    < 0 => ValidationResult.Error("[red]Age cannot be negative[/]"),
                    > 150 => ValidationResult.Error("[red]That seems unlikely![/]"),
                    _ => ValidationResult.Success(),
                }));

        _console.MarkupLine($"You are [blue]{age}[/] years old.");
    }

    private void ShowDemo()
    {
        _console.MarkupLine("[bold]Running interactive demo...[/]\n");

        // Selection
        var color = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Pick a [green]color[/]:")
                .AddChoices(["Red", "Green", "Blue", "Yellow"]));

        // Multi-selection
        var features = _console.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select [green]features[/] to enable:")
                .AddChoices(["Logging", "Metrics", "Tracing", "Alerts"]));

        // Confirmation
        var proceed = _console.Confirm("Apply these settings?");

        // Summary
        _console.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Color", $"[{color.ToLower()}]{color}[/]");
        table.AddRow("Features", string.Join(", ", features));
        table.AddRow("Applied", proceed ? "[green]Yes[/]" : "[red]No[/]");

        _console.Write(table);
    }

    private void ShowProgress()
    {
        _console.Progress()
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

        _console.MarkupLine("\n[green]All tasks completed![/]");
    }

    private void ShowSpinner()
    {
        _console.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Processing...[/]", ctx =>
            {
                // Simulate work with different status messages
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

        _console.MarkupLine("[green]Done![/]");
    }

    private void ShowLive()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddColumn("Status");

        _console.Live(table)
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

        _console.MarkupLine("\n[dim]Live display finished.[/]");
    }

    private void ShowTree()
    {
        var root = new Tree("[yellow]Project[/]")
            .Style("dim");

        // Source folder
        var src = root.AddNode("[blue]src[/]");
        var host = src.AddNode("[blue]SshServer.Host[/]");
        host.AddNode("[green]Program.cs[/]");
        host.AddNode("[green]appsettings.json[/]");
        var tui = host.AddNode("[blue]Tui[/]");
        tui.AddNode("[green]CommandHandler.cs[/]");
        tui.AddNode("[green]SshConsoleFactory.cs[/]");
        tui.AddNode("[green]SshTextWriter.cs[/]");
        tui.AddNode("[green]SshAnsiConsoleInput.cs[/]");
        tui.AddNode("[green]EscapeSequenceParser.cs[/]");

        var core = src.AddNode("[blue]SshServer.Core[/]");
        var ssh = core.AddNode("[blue]Ssh[/]");
        ssh.AddNode("[green]Session.cs[/]");
        ssh.AddNode("[green]Channel.cs[/]");

        // Config files
        var config = root.AddNode("[magenta]Configuration[/]");
        config.AddNode("[dim]README.md[/]");
        config.AddNode("[dim]CLAUDE.md[/]");
        config.AddNode("[dim]RELEASE_NOTES.md[/]");

        _console.Write(root);
    }

    private void ShowBarChart()
    {
        _console.Write(new BarChart()
            .Width(60)
            .Label("[green bold underline]Language Popularity[/]")
            .CenterLabel()
            .AddItem("C#", 85, Color.Green)
            .AddItem("Python", 92, Color.Yellow)
            .AddItem("JavaScript", 78, Color.Blue)
            .AddItem("Rust", 45, Color.Red)
            .AddItem("Go", 67, Color.Aqua)
            .AddItem("F#", 28, Color.Magenta1));

        _console.WriteLine();

        _console.Write(new BarChart()
            .Width(60)
            .Label("[blue bold underline]Server Metrics[/]")
            .CenterLabel()
            .AddItem("CPU", 45, Color.Green)
            .AddItem("Memory", 72, Color.Yellow)
            .AddItem("Disk I/O", 30, Color.Blue)
            .AddItem("Network", 58, Color.Aqua));
    }

    /// <summary>
    /// Render the welcome message.
    /// </summary>
    public void ShowWelcome()
    {
        _console.Write(new Rule("[green]Welcome to SshServer[/]").RuleStyle("dim"));
        _console.MarkupLine("Type [blue]help[/] for available commands.");
    }
}
