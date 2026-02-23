using FxSsh.Services;

using Microsoft.Extensions.Logging;

using Spectre.Console;
using Spectre.Console.Rendering;

using SshServer.Host.Tui;

using static SshServer.Host.Tui.SshConsoleFactory;

namespace SshServer.Host;

/// <summary>
/// Connection information passed to the application.
/// </summary>
public record ConnectionInfo(
    string ConnectionId,
    string Username,
    string AuthMethod,
    string? KeyFingerprint = null
);

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
    /// Initialize and run the application. Called by the framework.
    /// </summary>
    internal void Run(
        SessionChannel channel,
        SshConsoleContext consoleContext,
        ConnectionInfo connInfo,
        SshServerOptions options,
        Action<string?> disconnect,
        Action updateActivity,
        Microsoft.Extensions.Logging.ILogger logger)
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

    #endregion
}
