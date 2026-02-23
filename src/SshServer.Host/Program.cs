using System.Net;

using FxSsh;
using FxSsh.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SshServer;
using SshServer.Host;
using SshServer.Host.Tui;

// ── configuration ──────────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("SSHSERVER_")
    .AddCommandLine(args)
    .Build();

var options = new SshServerOptions();
configuration.GetSection(SshServerOptions.SectionName).Bind(options);

// Parse log level from config
var logLevel = Enum.TryParse<LogLevel>(options.LogLevel, ignoreCase: true, out var level)
    ? level
    : LogLevel.Debug;

// ── logging ────────────────────────────────────────────────────────────────────

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(opts =>
    {
        opts.SingleLine = true;
        opts.TimestampFormat = "HH:mm:ss.fff ";
    });
    builder.SetMinimumLevel(logLevel);
});

var logger = loggerFactory.CreateLogger("SshServer");

// ── graceful shutdown ──────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    logger.LogInformation("Shutdown requested (Ctrl+C)");
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (!cts.IsCancellationRequested)
    {
        logger.LogInformation("Process exit requested");
        cts.Cancel();
    }
};

// ── authentication setup ──────────────────────────────────────────────────────

var authorizedKeys = new AuthorizedKeysStore(logger);

if (!string.IsNullOrEmpty(options.AuthorizedKeysPath))
{
    // Resolve relative paths against the application directory
    var keysPath = Path.IsPathRooted(options.AuthorizedKeysPath)
        ? options.AuthorizedKeysPath
        : Path.Combine(AppContext.BaseDirectory, options.AuthorizedKeysPath);
    authorizedKeys.LoadFromFile(keysPath);
}

// Log authentication mode
if (options.AllowAnonymous)
{
    logger.LogInformation("Anonymous access enabled (AllowAnonymous=true)");
}
else if (authorizedKeys.Count == 0)
{
    logger.LogWarning("AllowAnonymous=false but no authorized keys loaded - all connections will be rejected");
}
else
{
    logger.LogInformation("Public key authentication required ({Count} keys loaded)", authorizedKeys.Count);
}

// ── server startup ─────────────────────────────────────────────────────────────

var server = new global::FxSsh.SshServer(new StartingInfo(IPAddress.IPv6Any, options.Port, options.Banner));

HostKeyStore.EnsureAndRegister(server, options.HostKeyPath);

server.ConnectionAccepted += OnConnectionAccepted;
server.ExceptionRaised += (_, ex) => logger.LogError(ex, "Server exception");

server.Start();
logger.LogInformation("SSH server listening on port {Port}. Press Ctrl+C to stop.", options.Port);

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Expected on shutdown
}

// ── graceful shutdown sequence ─────────────────────────────────────────────────

logger.LogInformation("Stopping server...");
server.Stop();
logger.LogInformation("Server stopped");

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

// ── event handlers ─────────────────────────────────────────────────────────────

void OnConnectionAccepted(object? sender, Session session)
{
    var connId = GenerateConnectionId(session);
    var connInfo = new ConnectionInfo(connId);
    logger.LogInformation("[{ConnId}] Connection accepted", connId);

    session.ServiceRegistered += (_, service) => OnServiceRegistered(service, connId, connInfo);
    session.Disconnected += (_, _) => logger.LogInformation("[{ConnId}] Disconnected", connId);
}

void OnServiceRegistered(SshService service, string connId, ConnectionInfo connInfo)
{
    // Handle authentication service
    if (service is UserAuthService authService)
    {
        authService.UserAuth += (_, args) =>
        {
            switch (args.AuthMethod)
            {
                case "none":
                    // Anonymous access
                    if (options.AllowAnonymous)
                    {
                        logger.LogInformation("[{ConnId}] Anonymous auth accepted for user '{User}'", connId, args.Username);
                        args.Result = true;
                        connInfo.SetAuth(args.Username, "none", null);
                    }
                    else
                    {
                        logger.LogDebug("[{ConnId}] Anonymous auth rejected for user '{User}'", connId, args.Username);
                        args.Result = false;
                    }
                    break;

                case "publickey":
                    // Public key authentication
                    if (args.Key != null && authorizedKeys.IsAuthorized(args.KeyAlgorithm!, args.Key))
                    {
                        logger.LogInformation("[{ConnId}] Public key auth accepted for user '{User}' ({KeyType})",
                            connId, args.Username, args.KeyAlgorithm);
                        args.Result = true;
                        connInfo.SetAuth(args.Username, "publickey", args.Fingerprint);
                    }
                    else
                    {
                        logger.LogDebug("[{ConnId}] Public key auth rejected for user '{User}' ({KeyType}, fingerprint: {Fingerprint})",
                            connId, args.Username, args.KeyAlgorithm, args.Fingerprint);
                        args.Result = false;
                    }
                    break;

                default:
                    logger.LogDebug("[{ConnId}] Unsupported auth method '{Method}' for user '{User}'",
                        connId, args.AuthMethod, args.Username);
                    args.Result = false;
                    break;
            }
        };
        return;
    }

    if (service is not ConnectionService connection)
        return;

    // Track terminal dimensions from PTY request
    int termWidth = 80;
    int termHeight = 24;
    SshAnsiConsoleOutput? consoleOutput = null;

    connection.PtyReceived += (_, pty) =>
    {
        termWidth = (int)pty.WidthChars;
        termHeight = (int)pty.HeightRows;
        logger.LogInformation("[{ConnId}] PTY: {Terminal} {Width}x{Height}", connId, pty.Terminal, termWidth, termHeight);
    };

    connection.WindowChange += (_, wc) =>
    {
        logger.LogDebug("[{ConnId}] Window: {Width}x{Height}", connId, wc.WidthColumns, wc.HeightRows);
        consoleOutput?.UpdateSize((int)wc.WidthColumns, (int)wc.HeightRows);
    };

    connection.CommandOpened += (_, e) =>
    {
        consoleOutput = OnCommandOpened(e, connInfo, termWidth, termHeight);
    };
}

SshAnsiConsoleOutput? OnCommandOpened(CommandRequestedArgs e, ConnectionInfo connInfo, int termWidth, int termHeight)
{
    logger.LogInformation("[{ConnId}] Shell: {ShellType}", connInfo.ConnectionId, e.ShellType);

    if (e.ShellType != "shell")
        return null;

    e.Agreed = true;

    var channel = e.Channel;

    // Input mode: false = line editing, true = Spectre prompt
    var inPromptMode = false;

    // Create Spectre console for this connection
    var ctx = SshConsoleFactory.Create(channel, termWidth, termHeight);
    var consoleOutput = ctx.Output;
    var consoleInput = ctx.Input;
    var commandHandler = new CommandHandler(ctx.Console, connInfo.ToRecord(), options);

    // Create line editor
    var lineEditor = new LineEditor(data => channel.SendData(data))
    {
        Completions =
        [
            "help", "status", "whoami", "config", "clear", "menu", "select",
            "multi", "confirm", "ask", "demo", "progress", "spinner",
            "live", "tree", "chart", "quit", "exit"
        ]
    };

    void Disconnect()
    {
        logger.LogInformation("[{ConnId}] Disconnecting", connInfo.ConnectionId);
        channel.SendData("\r\nGoodbye!\r\n"u8.ToArray());
        channel.SendEof();
        channel.SendClose(0);
    }

    // Welcome message via Spectre
    commandHandler.ShowWelcome();
    lineEditor.ShowPrompt();

    channel.DataReceived += (_, data) =>
    {
        // If in prompt mode, route all input to Spectre
        if (inPromptMode)
        {
            consoleInput.EnqueueData(data);
            return;
        }

        foreach (var b in data)
        {
            logger.LogTrace("[{ConnId}] Char: {Char}", connInfo.ConnectionId, FormatByte(b));

            var result = lineEditor.ProcessByte(b);

            switch (result)
            {
                case LineEditorResult.Disconnect:
                    Disconnect();
                    return;

                case LineEditorResult.LineSubmitted:
                    var command = lineEditor.SubmittedLine;
                    logger.LogDebug("[{ConnId}] Command: {Command}", connInfo.ConnectionId, command);

                    // Execute command asynchronously to allow input routing during prompts
                    inPromptMode = true;
                    Task.Run(() =>
                    {
                        try
                        {
                            if (!commandHandler.Execute(command))
                            {
                                Disconnect();
                                return;
                            }
                        }
                        finally
                        {
                            inPromptMode = false;
                            consoleInput.Clear();
                            lineEditor.ShowPrompt();
                        }
                    });
                    return; // Exit the foreach - we're now in prompt mode

                case LineEditorResult.Continue:
                default:
                    break;
            }
        }
    };

    channel.CloseReceived += (_, _) =>
    {
        logger.LogInformation("[{ConnId}] Channel closed by client", connInfo.ConnectionId);
        channel.SendClose(0);
    };

    return consoleOutput;
}

// ── types ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Mutable connection info populated during authentication.
/// </summary>
class ConnectionInfo(string connectionId)
{
    public string ConnectionId { get; } = connectionId;
    public string Username { get; private set; } = "unknown";
    public string AuthMethod { get; private set; } = "none";
    public string? KeyFingerprint { get; private set; }

    public void SetAuth(string username, string authMethod, string? fingerprint)
    {
        Username = username;
        AuthMethod = authMethod;
        KeyFingerprint = fingerprint;
    }

    public SshServer.Host.Tui.ConnectionInfo ToRecord() =>
        new(ConnectionId, Username, AuthMethod, KeyFingerprint);
}
