using Spectre.Console;

namespace SshServer;

/// <summary>
/// Application that displays a menu for selecting among multiple registered apps.
/// Used internally when UseApplicationMenu() is configured.
/// </summary>
internal class AppLauncherApplication : SshShellApplication
{
    private readonly AppMenuConfiguration _config;
    private SshShellApplication? _currentApp;
    private string _currentAppName = "";

    public AppLauncherApplication(AppMenuConfiguration config)
    {
        _config = config;
    }

    protected override string Prompt => _currentApp?.GetPrompt() ?? "> ";

    protected override IEnumerable<string> Completions => _currentApp?.GetCompletions() ?? [];

    protected override void OnWelcome()
    {
        ShowMenu();
    }

    protected override void OnDisconnect()
    {
        _currentApp?.InvokeOnDisconnect();
    }

    private void ShowMenu()
    {
        _currentApp = null;
        _currentAppName = "";

        WriteLine();
        Console.Write(new Rule("[blue]Welcome[/]").RuleStyle("dim"));
        WriteLine();

        // Build selection with descriptions
        var prompt = new SelectionPrompt<string>()
            .Title(_config.MenuTitle)
            .PageSize(10)
            .HighlightStyle(new Style(Color.Green));

        foreach (var app in _config.Apps)
        {
            var label = string.IsNullOrEmpty(app.Description)
                ? app.Name
                : $"{app.Name} - [dim]{app.Description}[/]";
            prompt.AddChoice(label);
        }

        prompt.AddChoice("[red]Exit[/]");

        var choice = Console.Prompt(prompt);

        // Handle exit
        if (choice == "[red]Exit[/]")
        {
            Disconnect();
            return;
        }

        // Find selected app
        var selectedName = choice.Split(" - ")[0]; // Extract name before description
        var registration = _config.Apps.FirstOrDefault(a => a.Name == selectedName);

        if (registration == null)
        {
            WriteLine("[red]App not found.[/]");
            ShowMenu();
            return;
        }

        // Create and initialize the selected app
        _currentApp = registration.Factory();
        _currentAppName = registration.Name;
        _currentApp.InitializeForDelegation(Console, Connection, Options, Disconnect);

        WriteLine();
        Console.Write(new Rule($"[green]{Escape(registration.Name)}[/]").RuleStyle("dim"));
        WriteLine();

        _currentApp.InvokeOnConnect();
        _currentApp.InvokeOnWelcome();
    }

    protected override bool OnCommand(string command)
    {
        if (_currentApp == null)
        {
            ShowMenu();
            return true;
        }

        // Handle special "menu" command to return to menu
        if (command.Equals("menu", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("apps", StringComparison.OrdinalIgnoreCase))
        {
            _currentApp.InvokeOnDisconnect();
            ShowMenu();
            return true;
        }

        var continueSession = _currentApp.InvokeOnCommand(command);

        if (!continueSession)
        {
            _currentApp.InvokeOnDisconnect();

            if (_config.ReturnToMenu)
            {
                ShowMenu();
                return true;
            }

            return false; // Disconnect
        }

        return true;
    }

    protected override string? OnExec(string command)
    {
        // Parse "appname:command" syntax
        var colonIndex = command.IndexOf(':');
        string? targetAppName = null;
        string actualCommand = command;

        if (colonIndex > 0)
        {
            targetAppName = command[..colonIndex].Trim();
            actualCommand = command[(colonIndex + 1)..].Trim();
        }

        // Find the target app
        AppMenuConfiguration.AppRegistration? registration = null;

        if (targetAppName != null)
        {
            registration = _config.Apps.FirstOrDefault(a =>
                a.Name.Equals(targetAppName, StringComparison.OrdinalIgnoreCase));

            if (registration == null)
            {
                return $"Unknown app: {targetAppName}\nAvailable apps: {string.Join(", ", _config.Apps.Select(a => a.Name))}\n";
            }
        }
        else if (_config.DefaultAppName != null)
        {
            registration = _config.Apps.FirstOrDefault(a =>
                a.Name.Equals(_config.DefaultAppName, StringComparison.OrdinalIgnoreCase));
        }
        else if (_config.Apps.Count > 0)
        {
            // Use first app as default
            registration = _config.Apps[0];
        }

        if (registration == null)
        {
            return "No apps configured.\n";
        }

        // Create app and run exec
        var app = registration.Factory();
        app.InitializeForDelegation(Console, Connection, Options, null);

        // Try the app's OnExec handler first
        var result = app.InvokeOnExec(actualCommand);
        if (result != null)
            return result;

        // Fall back to OnCommand with captured output
        using var sw = new StringWriter();
        var tempConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new AnsiConsoleOutput(sw)
        });
        app.InitializeForDelegation(tempConsole, Connection, Options, null);

        try
        {
            app.InvokeOnCommand(actualCommand);
        }
        catch (Exception ex)
        {
            sw.WriteLine($"Error: {ex.Message}");
        }

        return sw.ToString();
    }
}
