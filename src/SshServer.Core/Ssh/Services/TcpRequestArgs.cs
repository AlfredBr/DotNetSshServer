using System.Diagnostics.Contracts;

namespace FxSsh.Services
{
    public class TcpRequestArgs
    {
        public TcpRequestArgs(SessionChannel channel, string host, int port, string originatorIP, int originatorPort, UserAuthArgs userAuthArgs)
        {
            Contract.Requires(channel != null);
            Contract.Requires(host != null);
            Contract.Requires(originatorIP != null);

            Channel = channel!;
            Host = host!;
            Port = port;
            OriginatorIP = originatorIP!;
            OriginatorPort = originatorPort;
            AttachedUserAuthArgs = userAuthArgs!;
        }

        public SessionChannel Channel { get; private set; } = null!;
        public string Host { get; private set; } = null!;
        public int Port { get; private set; }
        public string OriginatorIP { get; private set; } = null!;
        public int OriginatorPort { get; private set; }
        public UserAuthArgs AttachedUserAuthArgs { get; private set; } = null!;
    }
}
