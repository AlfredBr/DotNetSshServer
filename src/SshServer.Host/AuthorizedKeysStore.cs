using Microsoft.Extensions.Logging;

namespace SshServer.Host;

/// <summary>
/// Stores and validates authorized public keys from an authorized_keys file.
/// </summary>
public class AuthorizedKeysStore
{
    private readonly Dictionary<string, AuthorizedKey> _keys = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Number of loaded keys.
    /// </summary>
    public int Count => _keys.Count;

    /// <summary>
    /// Creates an empty store.
    /// </summary>
    public AuthorizedKeysStore(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load keys from an authorized_keys file.
    /// </summary>
    /// <param name="path">Path to authorized_keys file.</param>
    /// <returns>True if file was loaded successfully.</returns>
    public bool LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            _logger?.LogWarning("Authorized keys file not found: {Path}", path);
            return false;
        }

        var lines = File.ReadAllLines(path);
        var loadedCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var key = ParseLine(trimmed);
            if (key != null)
            {
                // Use base64 key data as the dictionary key
                _keys[key.KeyData] = key;
                loadedCount++;
                _logger?.LogDebug("Loaded key: {KeyType} {Comment}", key.KeyType, key.Comment ?? "(no comment)");
            }
        }

        _logger?.LogInformation("Loaded {Count} authorized keys from {Path}", loadedCount, path);
        return loadedCount > 0;
    }

    /// <summary>
    /// Check if a public key is authorized.
    /// </summary>
    /// <param name="keyType">Key algorithm (e.g., "ssh-rsa", "ssh-ed25519").</param>
    /// <param name="keyData">Raw key bytes.</param>
    /// <returns>True if the key is authorized.</returns>
    public bool IsAuthorized(string keyType, byte[] keyData)
    {
        // Convert to base64 for comparison
        var base64 = Convert.ToBase64String(keyData);

        if (_keys.TryGetValue(base64, out var key))
        {
            // Verify key type matches
            if (key.KeyType.Equals(keyType, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("Key authorized: {KeyType} {Comment}", keyType, key.Comment ?? "(no comment)");
                return true;
            }

            _logger?.LogWarning("Key type mismatch: expected {Expected}, got {Actual}", key.KeyType, keyType);
        }

        return false;
    }

    /// <summary>
    /// Parse a single line from authorized_keys file.
    /// Format: [options] keytype base64-key [comment]
    /// </summary>
    private AuthorizedKey? ParseLine(string line)
    {
        // Split by whitespace
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return null;

        // Determine which part is the key type
        // Standard key types start with "ssh-" or "ecdsa-"
        int keyTypeIndex = 0;

        // Check if first part looks like options (contains '=' or starts with known option)
        if (parts[0].Contains('=') || parts[0].StartsWith("command=") ||
            parts[0].StartsWith("no-") || parts[0].StartsWith("restrict"))
        {
            keyTypeIndex = 1;
        }

        if (parts.Length <= keyTypeIndex + 1)
            return null;

        var keyType = parts[keyTypeIndex];
        var keyData = parts[keyTypeIndex + 1];

        // Validate key type
        if (!IsValidKeyType(keyType))
        {
            _logger?.LogWarning("Skipping unrecognized key type: {KeyType}", keyType);
            return null;
        }

        // Validate base64
        try
        {
            Convert.FromBase64String(keyData);
        }
        catch (FormatException)
        {
            _logger?.LogWarning("Skipping invalid base64 key data");
            return null;
        }

        // Extract comment (everything after the key data)
        string? comment = null;
        if (parts.Length > keyTypeIndex + 2)
        {
            comment = string.Join(' ', parts.Skip(keyTypeIndex + 2));
        }

        return new AuthorizedKey(keyType, keyData, comment);
    }

    private static bool IsValidKeyType(string keyType) => keyType switch
    {
        "ssh-rsa" => true,
        "ssh-ed25519" => true,
        "ecdsa-sha2-nistp256" => true,
        "ecdsa-sha2-nistp384" => true,
        "ecdsa-sha2-nistp521" => true,
        "ssh-dss" => true, // Legacy DSA
        _ => false
    };
}

/// <summary>
/// Represents a single authorized public key.
/// </summary>
public record AuthorizedKey(string KeyType, string KeyData, string? Comment);
