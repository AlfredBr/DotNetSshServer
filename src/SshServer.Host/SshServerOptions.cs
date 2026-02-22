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
}
