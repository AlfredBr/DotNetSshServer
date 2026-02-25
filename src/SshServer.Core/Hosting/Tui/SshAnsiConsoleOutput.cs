using System.Text;

using FxSsh.Services;

using Spectre.Console;

namespace AlfredBr.SshServer.Core.Tui;

/// <summary>
/// Implements IAnsiConsoleOutput to direct Spectre.Console rendering to an SSH channel.
/// </summary>
public class SshAnsiConsoleOutput : IAnsiConsoleOutput
{
    private readonly SshTextWriter _writer;
    private int _width;
    private int _height;

    public SshAnsiConsoleOutput(Channel channel, int width, int height)
    {
        _writer = new SshTextWriter(channel);
        _width = width;
        _height = height;
    }

    public TextWriter Writer => _writer;

    public bool IsTerminal => true;

    public int Width => _width;

    public int Height => _height;

    public void SetEncoding(Encoding encoding)
    {
        // SSH uses UTF-8, ignore encoding changes
    }

    /// <summary>
    /// Update terminal dimensions (call when window-change event received).
    /// </summary>
    public void UpdateSize(int width, int height)
    {
        _width = width;
        _height = height;
    }
}
