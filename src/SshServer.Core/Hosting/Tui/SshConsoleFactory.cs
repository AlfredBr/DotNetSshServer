using FxSsh.Services;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace SshServer.Tui;

/// <summary>
/// Factory for creating per-connection IAnsiConsole instances.
/// </summary>
public static class SshConsoleFactory
{
    /// <summary>
    /// Result of creating an SSH console with all components.
    /// </summary>
    public record SshConsoleContext(
        IAnsiConsole Console,
        SshAnsiConsoleOutput Output,
        SshAnsiConsoleInput Input);

    /// <summary>
    /// Creates an IAnsiConsole that renders to the specified SSH channel.
    /// </summary>
    /// <param name="channel">The SSH channel to write output to.</param>
    /// <param name="width">Terminal width in characters.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <returns>Console context with console, output, and input components.</returns>
    public static SshConsoleContext Create(Channel channel, int width, int height)
    {
        var output = new SshAnsiConsoleOutput(channel, width, height);
        var input = new SshAnsiConsoleInput();

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = output,
            Interactive = InteractionSupport.Yes,
        });

        // Inject our custom input handler
        return new SshConsoleContext(
            new SshAnsiConsoleWrapper(console, input),
            output,
            input);
    }
}

/// <summary>
/// Wraps an IAnsiConsole to provide custom input handling.
/// </summary>
internal class SshAnsiConsoleWrapper : IAnsiConsole
{
    private readonly IAnsiConsole _inner;
    private readonly SshAnsiConsoleInput _input;

    public SshAnsiConsoleWrapper(IAnsiConsole inner, SshAnsiConsoleInput input)
    {
        _inner = inner;
        _input = input;
    }

    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input => _input;
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;

    public void Clear(bool home) => _inner.Clear(home);
    public void Write(IRenderable renderable) => _inner.Write(renderable);
}
