namespace SshServer;

/// <summary>
/// Connection information passed to the application.
/// </summary>
public record ConnectionInfo(
    string ConnectionId,
    string Username,
    string AuthMethod,
    string? KeyFingerprint = null
);
