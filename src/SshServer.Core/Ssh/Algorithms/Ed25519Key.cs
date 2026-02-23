using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace FxSsh.Algorithms
{
    /// <summary>
    /// Ed25519 public key algorithm implementation for SSH.
    /// Uses NSec.Cryptography for Ed25519 operations.
    /// </summary>
    public class Ed25519Key : PublicKeyAlgorithm
    {
        private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;
        private Key? _privateKey;
        private PublicKey? _publicKey;

        public Ed25519Key(string? key)
            : base(key)
        {
            // Create a new Ed25519 key if none was provided
            _privateKey ??= Key.Create(Algorithm, new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            });
            _publicKey ??= _privateKey.PublicKey;
        }

        public override string Name => "ssh-ed25519";

        public override void ImportKey(string key)
        {
            // Parse PEM format
            // Ed25519 private keys in PEM are typically PKCS#8 format
            var pemBytes = DecodePem(key, "PRIVATE KEY");

            // PKCS#8 Ed25519 private key format:
            // SEQUENCE {
            //   INTEGER 0
            //   SEQUENCE { OID 1.3.101.112 }
            //   OCTET STRING { OCTET STRING { 32-byte private key } }
            // }
            // The actual private key is the last 32 bytes (inside nested OCTET STRING)
            if (pemBytes.Length >= 48 && pemBytes[0] == 0x30)
            {
                // Find the 32-byte seed at the end (it's inside an OCTET STRING wrapper)
                // Skip to the inner OCTET STRING containing the key
                var seed = pemBytes[^32..];
                _privateKey = Key.Import(Algorithm, seed, KeyBlobFormat.RawPrivateKey,
                    new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
                _publicKey = _privateKey.PublicKey;
            }
            else
            {
                throw new CryptographicException("Invalid Ed25519 private key format");
            }
        }

        public override string ExportKey()
        {
            if (_privateKey == null)
                throw new InvalidOperationException("No private key available to export");

            // Export as PKCS#8 PEM format
            var seed = _privateKey.Export(KeyBlobFormat.RawPrivateKey);
            var pkcs8 = CreatePkcs8PrivateKey(seed);
            return EncodePem(pkcs8, "PRIVATE KEY");
        }

        public override void LoadKeyAndCertificatesData(byte[] data)
        {
            var reader = new SshDataReader(data);
            var keyType = reader.ReadString(Encoding.ASCII);
            if (keyType != Name)
                throw new CryptographicException($"Key type mismatch: expected {Name}, got {keyType}");

            // Ed25519 public key is 32 bytes
            var publicKeyBytes = reader.ReadBinary();
            if (publicKeyBytes.Length != 32)
                throw new CryptographicException($"Invalid Ed25519 public key length: {publicKeyBytes.Length}");

            // Import the raw public key
            _publicKey = PublicKey.Import(Algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            _privateKey = null; // We only have the public key
        }

        public override byte[] CreateKeyAndCertificatesData()
        {
            if (_publicKey == null)
                throw new InvalidOperationException("No public key available");

            // Export the raw public key (32 bytes for Ed25519)
            var publicKeyBytes = _publicKey.Export(KeyBlobFormat.RawPublicKey);

            return new SshDataWriter(4 + Name.Length + 4 + publicKeyBytes.Length)
                .Write(Name, Encoding.ASCII)
                .WriteBinary(publicKeyBytes)
                .ToByteArray();
        }

        public override bool VerifyData(byte[] data, byte[] signature)
        {
            if (_publicKey == null)
                throw new InvalidOperationException("No public key available for verification");

            return Algorithm.Verify(_publicKey, data, signature);
        }

        public override bool VerifyHash(byte[] hash, byte[] signature)
        {
            // Ed25519 operates on the raw message, not a pre-hash
            // This method shouldn't be used for Ed25519, but we include it for interface compliance
            throw new NotSupportedException("Ed25519 does not support pre-hashed verification. Use VerifyData instead.");
        }

        public override byte[] SignData(byte[] data)
        {
            if (_privateKey == null)
                throw new InvalidOperationException("No private key available for signing");

            return Algorithm.Sign(_privateKey, data);
        }

        public override byte[] SignHash(byte[] hash)
        {
            // Ed25519 operates on the raw message, not a pre-hash
            throw new NotSupportedException("Ed25519 does not support pre-hashed signing. Use SignData instead.");
        }

        /// <summary>
        /// Decode a PEM-encoded blob.
        /// </summary>
        private static byte[] DecodePem(string pem, string expectedLabel)
        {
            var lines = pem.Split('\n');
            var base64 = new StringBuilder();
            var inBlock = false;
            var beginMarker = $"-----BEGIN {expectedLabel}-----";
            var endMarker = $"-----END {expectedLabel}-----";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == beginMarker)
                {
                    inBlock = true;
                }
                else if (trimmed == endMarker)
                {
                    break;
                }
                else if (inBlock)
                {
                    base64.Append(trimmed);
                }
            }

            return Convert.FromBase64String(base64.ToString());
        }

        /// <summary>
        /// Encode a blob as PEM.
        /// </summary>
        private static string EncodePem(byte[] data, string label)
        {
            var base64 = Convert.ToBase64String(data);
            var sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {label}-----");

            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }

            sb.AppendLine($"-----END {label}-----");
            return sb.ToString();
        }

        /// <summary>
        /// Create a PKCS#8 private key structure from a raw Ed25519 seed.
        /// </summary>
        private static byte[] CreatePkcs8PrivateKey(byte[] seed)
        {
            if (seed.Length != 32)
                throw new CryptographicException($"Invalid Ed25519 seed length: {seed.Length}");

            // PKCS#8 structure for Ed25519:
            // SEQUENCE {
            //   INTEGER 0 (version)
            //   SEQUENCE { OID 1.3.101.112 (Ed25519) }
            //   OCTET STRING { OCTET STRING { seed } }
            // }
            var innerOctetString = new byte[2 + seed.Length];
            innerOctetString[0] = 0x04; // OCTET STRING tag
            innerOctetString[1] = (byte)seed.Length;
            Array.Copy(seed, 0, innerOctetString, 2, seed.Length);

            // Build the full PKCS#8 structure
            // Version: INTEGER 0
            byte[] version = [0x02, 0x01, 0x00];
            // Algorithm: SEQUENCE { OID 1.3.101.112 }
            byte[] algorithm = [0x30, 0x05, 0x06, 0x03, 0x2b, 0x65, 0x70];
            // Outer OCTET STRING wrapper
            var outerOctetString = new byte[2 + innerOctetString.Length];
            outerOctetString[0] = 0x04; // OCTET STRING tag
            outerOctetString[1] = (byte)innerOctetString.Length;
            Array.Copy(innerOctetString, 0, outerOctetString, 2, innerOctetString.Length);

            // Total content length
            var contentLength = version.Length + algorithm.Length + outerOctetString.Length;
            var result = new byte[2 + contentLength];
            result[0] = 0x30; // SEQUENCE tag
            result[1] = (byte)contentLength;
            var offset = 2;
            Array.Copy(version, 0, result, offset, version.Length);
            offset += version.Length;
            Array.Copy(algorithm, 0, result, offset, algorithm.Length);
            offset += algorithm.Length;
            Array.Copy(outerOctetString, 0, result, offset, outerOctetString.Length);

            return result;
        }
    }
}
