namespace SshServer.Host;

/// <summary>
/// Configuration options for the SSH server.
/// </summary>
public class SshServerOptions
{
    /// <summary>
    /// The section name in appsettings.json.
    /// </summary>
    public const string SectionName = "SshServer";

    /// <summary>
    /// Port to listen on. Default: 2222.
    /// </summary>
    public int Port { get; set; } = 2222;

    /// <summary>
    /// SSH protocol banner. Default: "SSH-2.0-SshServer".
    /// </summary>
    public string Banner { get; set; } = "SSH-2.0-SshServer";

    /// <summary>
    /// Path to host key file. Default: "hostkey_ecdsa_nistp256.pem".
    /// </summary>
    public string HostKeyPath { get; set; } = "hostkey_ecdsa_nistp256.pem";

    /// <summary>
    /// Maximum number of concurrent connections. 0 = unlimited. Default: 100.
    /// </summary>
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    /// Minimum log level. Default: "Debug".
    /// </summary>
    public string LogLevel { get; set; } = "Debug";

    /// <summary>
    /// Allow anonymous connections (no authentication required).
    /// Default: true for development, set to false in production.
    /// </summary>
    public bool AllowAnonymous { get; set; } = true;

    /// <summary>
    /// Path to authorized_keys file for public key authentication.
    /// If not set or file doesn't exist, public key auth is disabled.
    /// </summary>
    public string? AuthorizedKeysPath { get; set; }

    /// <summary>
    /// Session idle timeout in minutes. 0 = no timeout (default).
    /// Sessions with no activity for this duration will be disconnected.
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 0;
}
