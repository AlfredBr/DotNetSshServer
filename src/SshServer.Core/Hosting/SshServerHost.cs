using System.Net;
using System.Text;

using FxSsh;
using FxSsh.Services;

using Microsoft.Extensions.Logging;

using AlfredBr.SshServer.Core.Tui;

namespace AlfredBr.SshServer.Core;

/// <summary>
/// Hosts an SSH server with a configured shell application.
/// </summary>
public sealed class SshServerHost : IAsyncDisposable
{
    private readonly SshServerOptions _options;
    private readonly Func<SshShellApplication> _defaultApplicationFactory;
    private readonly Dictionary<string, Func<SshShellApplication>> _userMappings;
    private readonly AppMenuConfiguration? _menuConfiguration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly AuthorizedKeysStore _authorizedKeys;
    private readonly ConnectionLimiter _connectionLimiter;
    private readonly CancellationTokenSource _cts = new();

    private global::FxSsh.SshServer? _server;
    private bool _disposed;

    internal SshServerHost(
        SshServerOptions options,
        Func<SshShellApplication> applicationFactory,
        Dictionary<string, Func<SshShellApplication>> userMappings,
        AppMenuConfiguration? menuConfiguration,
        Action<ILoggingBuilder>? loggingConfiguration)
    {
        _options = options;
        _defaultApplicationFactory = applicationFactory;
        _userMappings = new Dictionary<string, Func<SshShellApplication>>(userMappings, StringComparer.OrdinalIgnoreCase);
        _menuConfiguration = menuConfiguration;

        // Setup logging
        var logLevel = Enum.TryParse<LogLevel>(_options.LogLevel, ignoreCase: true, out var level)
            ? level
            : LogLevel.Debug;

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(opts =>
            {
                opts.SingleLine = true;
                opts.TimestampFormat = "HH:mm:ss.fff ";
            });
            builder.SetMinimumLevel(logLevel);

            loggingConfiguration?.Invoke(builder);
        });

        _logger = _loggerFactory.CreateLogger("SshServer");
        _connectionLimiter = new ConnectionLimiter(_options.MaxConnections);

        // Setup authorized keys
        _authorizedKeys = new AuthorizedKeysStore(_logger);
        if (!string.IsNullOrEmpty(_options.AuthorizedKeysPath))
        {
            var keysPath = Path.IsPathRooted(_options.AuthorizedKeysPath)
                ? _options.AuthorizedKeysPath
                : Path.Combine(AppContext.BaseDirectory, _options.AuthorizedKeysPath);
            _authorizedKeys.LoadFromFile(keysPath);
        }
    }

    /// <summary>
    /// Create a new builder for configuring an SSH server.
    /// </summary>
    public static SshServerBuilder CreateBuilder() => new();

    /// <summary>
    /// Start the SSH server and run until cancellation.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Link external cancellation
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        // Log authentication mode
        if (_options.AllowAnonymous)
        {
            _logger.LogInformation("Anonymous access enabled (AllowAnonymous=true)");
        }
        else if (_authorizedKeys.Count == 0)
        {
            _logger.LogWarning("AllowAnonymous=false but no authorized keys loaded - all connections will be rejected");
        }
        else
        {
            _logger.LogInformation("Public key authentication required ({Count} keys loaded)", _authorizedKeys.Count);
        }

        // Log user mappings
        if (_userMappings.Count > 0)
        {
            _logger.LogInformation("User mappings configured: {Users}", string.Join(", ", _userMappings.Keys));
        }

        // Log menu configuration
        if (_menuConfiguration != null)
        {
            _logger.LogInformation("Application menu configured with {Count} apps", _menuConfiguration.Apps.Count);
        }

        // Create and start server
        _server = new global::FxSsh.SshServer(new StartingInfo(IPAddress.IPv6Any, _options.Port, _options.Banner));
        HostKeyStore.EnsureAndRegister(_server, _options.HostKeyPath);

        _server.ConnectionAccepted += OnConnectionAccepted;
        _server.ExceptionRaised += (_, ex) => _logger.LogError(ex, "Server exception");

        _server.Start();
        _logger.LogInformation("SSH server listening on port {Port}. Press Ctrl+C to stop.", _options.Port);

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        // Stop server
        _logger.LogInformation("Stopping server...");
        _server.Stop();
        _logger.LogInformation("Server stopped");
    }

    /// <summary>
    /// Stop the SSH server gracefully.
    /// </summary>
    public Task StopAsync()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose the SSH server host.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _cts.CancelAsync();
        _cts.Dispose();
        _loggerFactory.Dispose();
    }

    #region Application Resolution

    /// <summary>
    /// Get the application factory for a given username.
    /// </summary>
    private Func<SshShellApplication> GetApplicationFactory(string username)
    {
        // Check user mappings first
        if (_userMappings.TryGetValue(username, out var factory))
        {
            _logger.LogDebug("Using mapped application for user '{Username}'", username);
            return factory;
        }

        // Fall back to default (which may be the menu launcher)
        _logger.LogDebug("Using default application for user '{Username}'", username);
        return _defaultApplicationFactory;
    }

    #endregion

    #region Event Handlers

    private void OnConnectionAccepted(object? sender, Session session)
    {
        if (!_connectionLimiter.TryAcquire(out var activeConnections))
        {
            var limit = _connectionLimiter.MaxConnections;
            _logger.LogWarning(
                "Rejecting connection from {RemoteEndPoint}: max connections reached ({Active}/{Limit})",
                session.RemoteEndPoint,
                activeConnections,
                limit);
            session.Disconnect(DisconnectReason.TooManyConnections,
                $"Too many connections. The server limit is {limit} concurrent connection(s).");
            return;
        }

        var connId = GenerateConnectionId(session);
        var connInfo = new MutableConnectionInfo(connId);
        _logger.LogInformation("[{ConnId}] Connection accepted", connId);

        session.ServiceRegistered += (_, service) => OnServiceRegistered(service, connId, connInfo);
        session.Disconnected += (_, _) =>
        {
            var remainingConnections = _connectionLimiter.Release();
            _logger.LogInformation("[{ConnId}] Disconnected ({ActiveConnections} active connection(s) remain)",
                connId,
                remainingConnections);
        };
    }

    private void OnServiceRegistered(SshService service, string connId, MutableConnectionInfo connInfo)
    {
        // Handle authentication service
        if (service is UserAuthService authService)
        {
            authService.UserAuth += (_, args) =>
            {
                var evaluation = AuthenticationEvaluator.Evaluate(args, _options.AllowAnonymous, _authorizedKeys);

                switch (args.AuthMethod)
                {
                    case "none":
                        if (evaluation.IsAccepted)
                        {
                            _logger.LogInformation("[{ConnId}] Anonymous auth accepted for user '{User}'", connId, args.Username);
                        }
                        else
                        {
                            _logger.LogDebug("[{ConnId}] Anonymous auth rejected for user '{User}'", connId, args.Username);
                        }
                        break;

                    case "publickey":
                        if (evaluation.IsAccepted)
                        {
                            _logger.LogInformation("[{ConnId}] Public key auth accepted for user '{User}' ({KeyType})",
                                connId, args.Username, args.KeyAlgorithm);
                        }
                        else
                        {
                            _logger.LogDebug("[{ConnId}] Public key auth rejected for user '{User}' ({KeyType}, fingerprint: {Fingerprint})",
                                connId, args.Username, args.KeyAlgorithm, args.Fingerprint);
                        }
                        break;

                    case "password":
                        _logger.LogDebug("[{ConnId}] Password auth rejected for user '{User}'",
                            connId, args.Username);
                        break;

                    default:
                        _logger.LogDebug("[{ConnId}] Unsupported auth method '{Method}' for user '{User}'",
                            connId, args.AuthMethod, args.Username);
                        break;
                }

                args.Result = evaluation.IsAccepted;
                if (evaluation.IsAccepted)
                {
                    connInfo.SetAuth(args.Username, evaluation.ConnectionAuthMethod, evaluation.KeyFingerprint);
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
            _logger.LogInformation("[{ConnId}] PTY: {Terminal} {Width}x{Height}", connId, pty.Terminal, termWidth, termHeight);
        };

        connection.WindowChange += (_, wc) =>
        {
            _logger.LogDebug("[{ConnId}] Window: {Width}x{Height}", connId, wc.WidthColumns, wc.HeightRows);
            consoleOutput?.UpdateSize((int)wc.WidthColumns, (int)wc.HeightRows);
        };

        connection.CommandOpened += (_, e) =>
        {
            consoleOutput = OnCommandOpened(e, connInfo, termWidth, termHeight);
        };
    }

    private SshAnsiConsoleOutput? OnCommandOpened(CommandRequestedArgs e, MutableConnectionInfo connInfo, int termWidth, int termHeight)
    {
        _logger.LogInformation("[{ConnId}] Channel: {ShellType}", connInfo.ConnectionId, e.ShellType);

        // Get the appropriate application factory based on username
        var appFactory = GetApplicationFactory(connInfo.Username);

        // Handle exec channel (single command execution)
        if (e.ShellType == "exec")
        {
            e.Agreed = true;
            var execChannel = e.Channel;
            var execCommand = e.CommandText ?? "";

            _logger.LogInformation("[{ConnId}] Exec: {Command}", connInfo.ConnectionId, execCommand);

            // Run command and send output
            var execApp = appFactory();
            var output = execApp.RunExec(connInfo.ToRecord(), _options, execCommand, _logger);

            // Send output with proper line endings
            var outputBytes = Encoding.UTF8.GetBytes(output.Replace("\n", "\r\n"));
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
            _logger.LogInformation("[{ConnId}] Disconnecting{Reason}", connInfo.ConnectionId,
                reason != null ? $": {reason}" : "");
            channel.SendData(Encoding.UTF8.GetBytes(message));
            channel.SendEof();
            channel.SendClose(0);
        }

        // Setup session timeout if configured
        if (_options.SessionTimeoutMinutes > 0)
        {
            var timeoutMs = _options.SessionTimeoutMinutes * 60 * 1000;
            timeoutTimer = new Timer(_ =>
            {
                var idle = DateTime.UtcNow - lastActivity;
                if (idle.TotalMinutes >= _options.SessionTimeoutMinutes)
                {
                    Disconnect($"Session timed out after {_options.SessionTimeoutMinutes} minute(s) of inactivity");
                }
            }, null, timeoutMs, timeoutMs);

            _logger.LogDebug("[{ConnId}] Session timeout set to {Minutes} minute(s)", connInfo.ConnectionId, _options.SessionTimeoutMinutes);
        }

        // Create and run the application
        var app = appFactory();
        app.Run(
            channel,
            consoleContext,
            connInfo.ToRecord(),
            _options,
            Disconnect,
            () => lastActivity = DateTime.UtcNow,
            _logger);

        // Handle channel close
        channel.CloseReceived += (_, _) =>
        {
            sessionClosed = true;
            timeoutTimer?.Dispose();
            _logger.LogInformation("[{ConnId}] Channel closed by client", connInfo.ConnectionId);
            channel.SendClose(0);
        };

        return consoleContext.Output;
    }

    #endregion

    #region Helpers

    private static string GenerateConnectionId(Session session)
    {
        var endpoint = session.RemoteEndPoint as IPEndPoint;
        var ip = endpoint?.Address.ToString() ?? "unknown";
        var port = endpoint?.Port ?? 0;
        var shortGuid = Guid.NewGuid().ToString("N")[..4];
        return $"{ip}:{port}-{shortGuid}";
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// Mutable connection info populated during authentication.
    /// </summary>
    private class MutableConnectionInfo(string connectionId)
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

    #endregion
}
