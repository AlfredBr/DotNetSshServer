using System.Net;

namespace FxSsh
{
    public class StartingInfo
    {
        public const int DefaultPort = 22;
        public static readonly TimeSpan DefaultTransportOperationTimeout = TimeSpan.FromSeconds(30);

        public StartingInfo()
            : this(IPAddress.IPv6Any, DefaultPort, "SSH-2.0-FxSsh")
        {
        }

        public StartingInfo(IPAddress localAddress, int port, string serverBanner)
            : this(localAddress, port, serverBanner, DefaultTransportOperationTimeout)
        {
        }

        public StartingInfo(IPAddress localAddress, int port, string serverBanner, TimeSpan transportOperationTimeout)
        {
            if (transportOperationTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(transportOperationTimeout), "Transport operation timeout must be greater than zero.");

            LocalAddress = localAddress;
            Port = port;
            ServerBanner = serverBanner;
            TransportOperationTimeout = transportOperationTimeout;
        }

        public IPAddress LocalAddress { get; private set; }
        public int Port { get; private set; }

        public string ServerBanner { get; private set; }
        public TimeSpan TransportOperationTimeout { get; private set; }
    }
}
