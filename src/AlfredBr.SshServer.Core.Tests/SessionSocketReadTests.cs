using System.Net;
using System.Net.Sockets;

using FxSsh;

namespace AlfredBr.SshServer.Core.Tests;

public class SessionSocketReadTests
{
    [Fact]
    public async Task SocketRead_TimesOutWhenInitialDataIsRequired()
    {
        using var sockets = await ConnectedSocketPair.CreateAsync();
        var session = CreateSession(sockets.Server, TimeSpan.FromMilliseconds(75));

        var readTask = Task.Run(() => session.SocketRead(1));

        var exception = await Assert.ThrowsAsync<SshConnectionException>(async () => await readTask);

        Assert.Equal(DisconnectReason.ConnectionLost, exception.DisconnectReason);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SocketRead_WaitsForInitialDataWhenIdlePacketWaitIsAllowed()
    {
        using var sockets = await ConnectedSocketPair.CreateAsync();
        var session = CreateSession(sockets.Server, TimeSpan.FromMilliseconds(75));

        var readTask = Task.Run(() => session.SocketRead(1, waitForInitialDataIndefinitely: true));
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromMilliseconds(250)));

        Assert.NotSame(readTask, completed);

        sockets.Client.Send(new byte[] { 42 });

        var result = await readTask;
        Assert.Equal(new byte[] { 42 }, result);
    }

    [Fact]
    public async Task SocketRead_TimesOutAfterPartialPacketDataArrives()
    {
        using var sockets = await ConnectedSocketPair.CreateAsync();
        var session = CreateSession(sockets.Server, TimeSpan.FromMilliseconds(75));

        var readTask = Task.Run(() => session.SocketRead(2, waitForInitialDataIndefinitely: true));
        sockets.Client.Send(new byte[] { 42 });

        var exception = await Assert.ThrowsAsync<SshConnectionException>(async () => await readTask);

        Assert.Equal(DisconnectReason.ConnectionLost, exception.DisconnectReason);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Session CreateSession(Socket socket, TimeSpan transportOperationTimeout) =>
        new(socket, [], "SSH-2.0-test", transportOperationTimeout);

    private sealed class ConnectedSocketPair : IDisposable
    {
        private ConnectedSocketPair(Socket server, Socket client)
        {
            Server = server;
            Client = client;
        }

        public Socket Server { get; }
        public Socket Client { get; }

        public static async Task<ConnectedSocketPair> CreateAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            Socket? client = null;

            listener.Start();
            try
            {
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var acceptTask = listener.AcceptSocketAsync();
                await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
                var server = await acceptTask;
                return new ConnectedSocketPair(server, client);
            }
            catch
            {
                client?.Dispose();
                throw;
            }
            finally
            {
                listener.Stop();
            }
        }

        public void Dispose()
        {
            Server.Dispose();
            Client.Dispose();
        }
    }
}
