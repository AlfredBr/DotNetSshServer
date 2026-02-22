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
        table.AddRow("quit", "Disconnect from server");
        table.AddRow("[dim]Ctrl-C[/]", "[dim]Disconnect (shortcut)[/]");

        _console.Write(table);
        _console.MarkupLine("\n[dim]Yellow commands are interactive - use arrow keys to navigate.[/]");
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

    /// <summary>
    /// Render the welcome message.
    /// </summary>
    public void ShowWelcome()
    {
        _console.Write(new Rule("[green]Welcome to SshServer[/]").RuleStyle("dim"));
        _console.MarkupLine("Type [blue]help[/] for available commands.");
    }
}
