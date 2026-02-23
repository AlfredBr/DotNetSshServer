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

// ── event handlers ─────────────────────────────────────────────────────────────

void OnConnectionAccepted(object? sender, Session session)
{
    var connId = GenerateConnectionId(session);
    var connInfo = new MutableConnectionInfo(connId);
    logger.LogInformation("[{ConnId}] Connection accepted", connId);

    session.ServiceRegistered += (_, service) => OnServiceRegistered(service, connId, connInfo);
    session.Disconnected += (_, _) => logger.LogInformation("[{ConnId}] Disconnected", connId);
}

void OnServiceRegistered(SshService service, string connId, MutableConnectionInfo connInfo)
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

SshAnsiConsoleOutput? OnCommandOpened(CommandRequestedArgs e, MutableConnectionInfo connInfo, int termWidth, int termHeight)
{
    logger.LogInformation("[{ConnId}] Channel: {ShellType}", connInfo.ConnectionId, e.ShellType);

    // Handle exec channel (single command execution)
    if (e.ShellType == "exec")
    {
        e.Agreed = true;
        var execChannel = e.Channel;
        var execCommand = e.CommandText ?? "";

        logger.LogInformation("[{ConnId}] Exec: {Command}", connInfo.ConnectionId, execCommand);

        // Run command and send output
        var execApp = new DemoApp();
        var output = execApp.RunExec(connInfo.ToRecord(), options, execCommand, logger);

        // Send output with proper line endings
        var outputBytes = System.Text.Encoding.UTF8.GetBytes(output.Replace("\n", "\r\n"));
        execChannel.SendData(outputBytes);
        execChannel.SendEof();
        execChannel.SendClose(0);

        return null;
    }

    if (e.ShellType != "shell")
        return null;

    e.Agreed = true;

    var channel = e.Channel;

    // Session state
    var lastActivity = DateTime.UtcNow;
    var sessionClosed = false;
    Timer? timeoutTimer = null;

    // Create Spectre console for this connection
    var consoleContext = SshConsoleFactory.Create(channel, termWidth, termHeight);

    // Disconnect helper
    void Disconnect(string? reason)
    {
        if (sessionClosed) return;
        sessionClosed = true;
        timeoutTimer?.Dispose();

        var message = reason != null ? $"\r\n{reason}\r\nGoodbye!\r\n" : "\r\nGoodbye!\r\n";
        logger.LogInformation("[{ConnId}] Disconnecting{Reason}", connInfo.ConnectionId,
            reason != null ? $": {reason}" : "");
        channel.SendData(System.Text.Encoding.UTF8.GetBytes(message));
        channel.SendEof();
        channel.SendClose(0);
    }

    // Setup session timeout if configured
    if (options.SessionTimeoutMinutes > 0)
    {
        var timeoutMs = options.SessionTimeoutMinutes * 60 * 1000;
        timeoutTimer = new Timer(_ =>
        {
            var idle = DateTime.UtcNow - lastActivity;
            if (idle.TotalMinutes >= options.SessionTimeoutMinutes)
            {
                Disconnect($"Session timed out after {options.SessionTimeoutMinutes} minute(s) of inactivity");
            }
        }, null, timeoutMs, timeoutMs);

        logger.LogDebug("[{ConnId}] Session timeout set to {Minutes} minute(s)", connInfo.ConnectionId, options.SessionTimeoutMinutes);
    }

    // Create and run the application
    var app = new DemoApp();
    app.Run(
        channel,
        consoleContext,
        connInfo.ToRecord(),
        options,
        Disconnect,
        () => lastActivity = DateTime.UtcNow,
        logger);

    // Handle channel close
    channel.CloseReceived += (_, _) =>
    {
        sessionClosed = true;
        timeoutTimer?.Dispose();
        logger.LogInformation("[{ConnId}] Channel closed by client", connInfo.ConnectionId);
        channel.SendClose(0);
    };

    return consoleContext.Output;
}

// ── types ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Mutable connection info populated during authentication.
/// </summary>
class MutableConnectionInfo(string connectionId)
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

    public ConnectionInfo ToRecord() =>
        new(ConnectionId, Username, AuthMethod, KeyFingerprint);
}
