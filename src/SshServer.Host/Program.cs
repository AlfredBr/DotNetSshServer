using System.Net;
using System.Text;

using FxSsh;
using FxSsh.Services;

using Microsoft.Extensions.Logging;

using SshServer;

const int Port = 2222;
const string Banner = "SSH-2.0-SshServer";
const int MaxDataLogLength = 128;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss.fff ";
    });
    builder.SetMinimumLevel(LogLevel.Trace);
});

var logger = loggerFactory.CreateLogger("SshServer");

var server = new global::FxSsh.SshServer(new StartingInfo(IPAddress.IPv6Any, Port, Banner));

HostKeyStore.EnsureAndRegister(server);

server.ConnectionAccepted += OnConnectionAccepted;
server.ExceptionRaised += (_, ex) => logger.LogError(ex, "Server exception");

server.Start();
logger.LogInformation("SSH server listening on port {Port}", Port);
await Task.Delay(Timeout.Infinite);

// ── helpers ────────────────────────────────────────────────────────────────────

string GenerateConnectionId(Session session)
{
    var endpoint = session.RemoteEndPoint as IPEndPoint;
    var ip = endpoint?.Address.ToString() ?? "unknown";
    var port = endpoint?.Port ?? 0;
    var shortGuid = Guid.NewGuid().ToString("N")[..4];
    return $"{ip}:{port}-{shortGuid}";
}

string FormatByte(byte b) => b switch
{
    0x00 => "\\0",
    0x04 => "\\x04",
    0x07 => "\\a",
    0x08 => "\\b",
    0x09 => "\\t",
    0x0A => "\\n",
    0x0D => "\\r",
    0x1B => "\\e",
    0x7F => "\\x7F",
    _ when b < 0x20 => $"\\x{b:X2}",
    _ when b < 0x7F => ((char)b).ToString(),
    _ => $"\\x{b:X2}"
};

string FormatData(byte[] data)
{
    var sb = new StringBuilder();
    var limit = Math.Min(data.Length, MaxDataLogLength);

    for (int i = 0; i < limit; i++)
    {
        var b = data[i];
        sb.Append(b switch
        {
            0x00 => "\\0",
            0x04 => "\\x04",
            0x07 => "\\a",
            0x08 => "\\b",
            0x09 => "\\t",
            0x0A => "\\n",
            0x0D => "\\r",
            0x1B => "\\e",
            0x7F => "\\x7F",
            _ when b < 0x20 => $"\\x{b:X2}",
            _ when b < 0x7F => (char)b,
            _ => $"\\x{b:X2}"
        });
    }

    if (data.Length > MaxDataLogLength)
        sb.Append($"[...+{data.Length - MaxDataLogLength} bytes]");

    return sb.ToString();
}

// ── event handlers ─────────────────────────────────────────────────────────────

void OnConnectionAccepted(object? sender, Session session)
{
    var connId = GenerateConnectionId(session);
    logger.LogInformation("[{ConnId}] Connection accepted", connId);

    session.ServiceRegistered += (_, service) => OnServiceRegistered(service, connId);
    session.Disconnected += (_, _) => logger.LogInformation("[{ConnId}] Disconnected", connId);
}

void OnServiceRegistered(SshService service, string connId)
{
    if (service is not ConnectionService connection)
        return;

    connection.PtyReceived += (_, pty) =>
        logger.LogInformation("[{ConnId}] PTY: {Terminal} {Width}x{Height}", connId, pty.Terminal, pty.WidthChars, pty.HeightRows);

    connection.WindowChange += (_, wc) =>
        logger.LogDebug("[{ConnId}] Window: {Width}x{Height}", connId, wc.WidthColumns, wc.HeightRows);

    connection.CommandOpened += (_, e) => OnCommandOpened(e, connId);
}

void OnCommandOpened(CommandRequestedArgs e, string connId)
{
    logger.LogInformation("[{ConnId}] Shell: {ShellType}", connId, e.ShellType);

    if (e.ShellType != "shell")
        return;

    e.Agreed = true;

    var channel = e.Channel;
    var lineBuffer = new StringBuilder();

    channel.SendData("Welcome to SshServer PoC. Your input is echoed back.\r\n> "u8.ToArray());

    channel.DataReceived += (_, data) =>
    {
        foreach (var b in data)
        {
            logger.LogTrace("[{ConnId}] Char: {Char}", connId, FormatByte(b));

            if (b == 0x04)
            {
                logger.LogInformation("[{ConnId}] Ctrl+D, closing", connId);
                channel.SendData("\r\nGoodbye!\r\n"u8.ToArray());
                channel.SendEof();
                channel.SendClose(0);
                return;
            }

            if (b == '\r' || b == '\n')
            {
                if (lineBuffer.Length > 0)
                {
                    logger.LogDebug("[{ConnId}] Line: {Line}", connId, lineBuffer.ToString());
                    lineBuffer.Clear();
                }
            }
            else if (b >= 0x20 && b < 0x7F)
            {
                lineBuffer.Append((char)b);
            }
        }

        channel.SendData(data);

        if (data.Contains((byte)'\r') || data.Contains((byte)'\n'))
            channel.SendData("\n> "u8.ToArray());
    };

    channel.CloseReceived += (_, _) =>
    {
        logger.LogInformation("[{ConnId}] Channel closed by client", connId);
        channel.SendClose(0);
    };
}
