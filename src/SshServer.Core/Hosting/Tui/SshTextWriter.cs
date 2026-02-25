using System.Text;

using FxSsh.Services;

namespace AlfredBr.SshServer.Core.Tui;

/// <summary>
/// A TextWriter that sends output to an SSH channel.
/// Translates LF to CRLF for proper terminal rendering.
/// </summary>
public class SshTextWriter : TextWriter
{
    private readonly Channel _channel;

    public SshTextWriter(Channel channel)
    {
        _channel = channel;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n')
        {
            // Translate LF to CRLF
            _channel.SendData("\r\n"u8.ToArray());
        }
        else
        {
            _channel.SendData(Encoding.UTF8.GetBytes([value]));
        }
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        // Translate standalone LF to CRLF (but don't double up existing CRLF)
        var translated = value.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _channel.SendData(Encoding.UTF8.GetBytes(translated));
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Write(new string(buffer, index, count));
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        Write("\r\n");
    }

    public override void WriteLine()
    {
        Write("\r\n");
    }
}
