using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace FxSsh.Services
{
    public class KeyExchangeArgs
    {
        public KeyExchangeArgs(Session s)
        {
            this.Session = s;
        }

        public Session Session { get; private set; }

        public byte[]? Cookie { get; private set; }

        public string[] KeyExchangeAlgorithms { get; set; } = null!;

        public string[] ServerHostKeyAlgorithms { get; set; } = null!;

        public string[] EncryptionAlgorithmsClientToServer { get; set; } = null!;

        public string[] EncryptionAlgorithmsServerToClient { get; set; } = null!;

        public string[] MacAlgorithmsClientToServer { get; set; } = null!;

        public string[] MacAlgorithmsServerToClient { get; set; } = null!;

        public string[] CompressionAlgorithmsClientToServer { get; set; } = null!;

        public string[] CompressionAlgorithmsServerToClient { get; set; } = null!;

        public string[] LanguagesClientToServer { get; set; } = null!;

        public string[] LanguagesServerToClient { get; set; } = null!;
    }
}
