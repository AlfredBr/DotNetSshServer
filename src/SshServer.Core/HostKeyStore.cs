using FxSsh;

namespace AlfredBr.SshServer.Core;

/// <summary>
/// Loads or generates the server host key, persisting it to disk so the
/// client's known_hosts entry survives restarts.
/// </summary>
public static class HostKeyStore
{
    private const string DefaultPath = "hostkey_ecdsa_nistp256.pem";
    private const string KeyType = "ecdsa-sha2-nistp256";

    /// <summary>
    /// Ensures a host key file exists at <paramref name="path"/> (generating one if absent),
    /// then registers it with the supplied <see cref="global::FxSsh.SshServer"/>.
    /// </summary>
    public static void EnsureAndRegister(global::FxSsh.SshServer server, string path = DefaultPath)
    {
        ArgumentNullException.ThrowIfNull(server);

        var pem = LoadOrGenerate(path);
        server.AddHostKey(KeyType, pem);
    }

    /// <summary>Returns the PEM for the host key, generating and saving it if the file does not exist.</summary>
    public static string LoadOrGenerate(string path = DefaultPath)
    {
        if (File.Exists(path))
            return File.ReadAllText(path);

        var pem = KeyGenerator.GenerateECDsaKeyPem("nistp256");
        File.WriteAllText(path, pem);
        return pem;
    }
}
