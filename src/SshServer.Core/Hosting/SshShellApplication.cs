using FxSsh.Services;

using Microsoft.Extensions.Logging;

using Spectre.Console;
using Spectre.Console.Rendering;

using SshServer.Tui;

using static SshServer.Tui.SshConsoleFactory;

namespace SshServer;

/// <summary>
/// Abstract base class for SSH shell applications.
/// Inherit from this class and override <see cref="OnCommand"/> to handle commands.
/// </summary>
public abstract class SshShellApplication
{
    private IAnsiConsole _console = null!;
    private LineEditor _lineEditor = null!;
    private Action<string>? _disconnect;

    /// <summary>
    /// The Spectre.Console instance for this connection.
    /// Use this for advanced rendering (tables, panels, trees, etc.)
    /// </summary>
    protected IAnsiConsole Console => _console;

    /// <summary>
    /// Information about the current connection and authenticated user.
    /// </summary>
    protected ConnectionInfo Connection { get; private set; } = null!;

    /// <summary>
    /// Server configuration options.
    /// </summary>
    protected SshServerOptions Options { get; private set; } = null!;

    #region Abstract and Virtual Members

    /// <summary>
    /// Handle a command entered by the user.
    /// </summary>
    /// <param name="command">The command line entered by the user.</param>
    /// <returns>True to continue the session, false to disconnect.</returns>
    protected abstract bool OnCommand(string command);

    /// <summary>
    /// The prompt string displayed before user input. Default: "&gt; "
    /// </summary>
    protected virtual string Prompt => "> ";

    /// <summary>
    /// Command names for tab completion. Override to provide your own.
    /// </summary>
    protected virtual IEnumerable<string> Completions => [];

    /// <summary>
    /// Called when a new connection is established, before the welcome message.
    /// </summary>
    protected virtual void OnConnect() { }

    /// <summary>
    /// Called to display the welcome message. Override to customize.
    /// </summary>
    protected virtual void OnWelcome()
    {
        WriteLine("Type [blue]help[/] for available commands.");
    }

    /// <summary>
    /// Called when the session is ending (user quit or timeout).
    /// </summary>
    protected virtual void OnDisconnect() { }

    /// <summary>
    /// Handle a single command from an exec channel (non-interactive).
    /// Override to support scripted SSH commands like: ssh user@host "status"
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The output to send back to the client, or null to use OnCommand instead.</returns>
    protected virtual string? OnExec(string command) => null;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Write a line with Spectre markup.
    /// </summary>
    protected void WriteLine(string markup) => _console.MarkupLine(markup);

    /// <summary>
    /// Write text with Spectre markup (no newline).
    /// </summary>
    protected void Write(string markup) => _console.Markup(markup);

    /// <summary>
    /// Write a blank line.
    /// </summary>
    protected void WriteLine() => _console.WriteLine();

    /// <summary>
    /// Write a renderable object (Table, Panel, Tree, etc.)
    /// </summary>
    protected void Write(IRenderable renderable) => _console.Write(renderable);

    /// <summary>
    /// Clear the terminal screen.
    /// </summary>
    protected void Clear() => _console.Clear();

    /// <summary>
    /// Ask for text input.
    /// </summary>
    protected string Ask(string prompt) => _console.Ask<string>(prompt);

    /// <summary>
    /// Ask for typed input.
    /// </summary>
    protected T Ask<T>(string prompt) => _console.Ask<T>(prompt);

    /// <summary>
    /// Ask for confirmation (yes/no).
    /// </summary>
    protected bool Confirm(string prompt, bool defaultValue = true)
        => _console.Confirm(prompt, defaultValue);

    /// <summary>
    /// Show a selection prompt.
    /// </summary>
    protected T Select<T>(string title, IEnumerable<T> choices) where T : notnull
        => _console.Prompt(new SelectionPrompt<T>().Title(title).AddChoices(choices));

    /// <summary>
    /// Show a multi-selection prompt.
    /// </summary>
    protected IReadOnlyList<T> MultiSelect<T>(string title, IEnumerable<T> choices) where T : notnull
        => _console.Prompt(new MultiSelectionPrompt<T>().Title(title).AddChoices(choices));

    /// <summary>
    /// Show a status spinner while performing work.
    /// </summary>
    protected void Status(string message, Action work)
        => _console.Status().Start(message, _ => work());

    /// <summary>
    /// Show progress bars for multiple tasks.
    /// </summary>
    protected void Progress(Action<ProgressContext> work)
        => _console.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .Start(work);

    /// <summary>
    /// Programmatically disconnect the session with an optional message.
    /// </summary>
    protected void Disconnect(string? message = null)
    {
        _disconnect?.Invoke(message ?? "");
    }

    /// <summary>
    /// Escape text for safe use in Spectre markup.
    /// </summary>
    protected static string Escape(string text) => Markup.Escape(text);

    #endregion

    #region Internal Framework Methods

    /// <summary>
    /// Initialize the application for delegation (used by AppLauncherApplication).
    /// </summary>
    internal void InitializeForDelegation(IAnsiConsole console, ConnectionInfo connInfo, SshServerOptions options, Action<string>? disconnect)
    {
        _console = console;
        Connection = connInfo;
        Options = options;
        _disconnect = disconnect;
    }

    /// <summary>
    /// Invoke OnWelcome. Used by AppLauncherApplication.
    /// </summary>
    internal void InvokeOnWelcome() => OnWelcome();

    /// <summary>
    /// Invoke OnCommand. Used by AppLauncherApplication.
    /// </summary>
    internal bool InvokeOnCommand(string command) => OnCommand(command);

    /// <summary>
    /// Invoke OnConnect. Used by AppLauncherApplication.
    /// </summary>
    internal void InvokeOnConnect() => OnConnect();

    /// <summary>
    /// Invoke OnDisconnect. Used by AppLauncherApplication.
    /// </summary>
    internal void InvokeOnDisconnect() => OnDisconnect();

    /// <summary>
    /// Get the prompt. Used by AppLauncherApplication.
    /// </summary>
    internal string GetPrompt() => Prompt;

    /// <summary>
    /// Get completions. Used by AppLauncherApplication.
    /// </summary>
    internal IEnumerable<string> GetCompletions() => Completions;

    /// <summary>
    /// Invoke OnExec. Used by AppLauncherApplication.
    /// </summary>
    internal string? InvokeOnExec(string command) => OnExec(command);

    /// <summary>
    /// Initialize and run the application. Called by the framework.
    /// </summary>
    internal void Run(
        SessionChannel channel,
        SshConsoleContext consoleContext,
        ConnectionInfo connInfo,
        SshServerOptions options,
        Action<string?> disconnect,
        Action updateActivity,
        ILogger logger)
    {
        _console = consoleContext.Console;
        Connection = connInfo;
        Options = options;
        _disconnect = disconnect;

        // Setup line editor
        _lineEditor = new LineEditor(data => channel.SendData(data))
        {
            Completions = Completions.ToArray(),
            Prompt = Prompt
        };

        // Track prompt mode for input routing
        var inPromptMode = false;

        // Lifecycle: connect
        OnConnect();
        OnWelcome();
        _lineEditor.ShowPrompt();

        // Wire up data received handler
        channel.DataReceived += (_, data) =>
        {
            // Update activity timestamp for session timeout
            updateActivity();

            // Route to Spectre prompts if in prompt mode
            if (inPromptMode)
            {
                consoleContext.Input.EnqueueData(data);
                return;
            }

            foreach (var b in data)
            {
                var result = _lineEditor.ProcessByte(b);

                switch (result)
                {
                    case LineEditorResult.Disconnect:
                        OnDisconnect();
                        disconnect(null);
                        return;

                    case LineEditorResult.LineSubmitted:
                        var command = _lineEditor.SubmittedLine;
                        logger.LogDebug("[{ConnId}] Command: {Command}", connInfo.ConnectionId, command);

                        // Execute command asynchronously for prompt support
                        inPromptMode = true;
                        Task.Run(() =>
                        {
                            try
                            {
                                if (!OnCommand(command))
                                {
                                    OnDisconnect();
                                    disconnect(null);
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteLine($"[red]Error:[/] {Escape(ex.Message)}");
                                logger.LogError(ex, "[{ConnId}] Command error", connInfo.ConnectionId);
                            }
                            finally
                            {
                                inPromptMode = false;
                                consoleContext.Input.Clear();

                                // Update prompt and completions in case they changed
                                _lineEditor.Prompt = Prompt;
                                _lineEditor.Completions = Completions.ToArray();
                                _lineEditor.ShowPrompt();
                            }
                        });
                        return;

                    case LineEditorResult.Continue:
                    default:
                        break;
                }
            }
        };
    }

    /// <summary>
    /// Execute a single command (exec channel). Called by the framework.
    /// </summary>
    /// <returns>The output to send to the client.</returns>
    internal string RunExec(
        ConnectionInfo connInfo,
        SshServerOptions options,
        string command,
        ILogger logger)
    {
        Connection = connInfo;
        Options = options;

        logger.LogDebug("[{ConnId}] Exec: {Command}", connInfo.ConnectionId, command);

        // Try the exec handler first
        var execResult = OnExec(command);
        if (execResult != null)
        {
            return execResult;
        }

        // Fall back to OnCommand with a StringWriter to capture output
        using var sw = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new AnsiConsoleOutput(sw)
        });
        _console = console;

        try
        {
            OnCommand(command);
        }
        catch (Exception ex)
        {
            sw.WriteLine($"Error: {ex.Message}");
            logger.LogError(ex, "[{ConnId}] Exec error", connInfo.ConnectionId);
        }

        return sw.ToString();
    }

    #endregion
}
