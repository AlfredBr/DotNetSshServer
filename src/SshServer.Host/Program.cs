using System.Net;
using System.Text;
using FxSsh;
using FxSsh.Services;
using SshServer;

const int Port = 2222;
const string Banner = "SSH-2.0-SshServer";

var server = new global::FxSsh.SshServer(new StartingInfo(IPAddress.IPv6Any, Port, Banner));

HostKeyStore.EnsureAndRegister(server);

server.ConnectionAccepted += OnConnectionAccepted;
server.ExceptionRaised += (_, ex) => Console.Error.WriteLine($"[error] {ex.Message}");

server.Start();
Console.WriteLine($"SSH server listening on port {Port}. Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite);

// ── event handlers ──────────────────────────────────────────────────────────

void OnConnectionAccepted(object? sender, Session session)
{
    Console.WriteLine($"[+] Connection accepted");
    session.ServiceRegistered += OnServiceRegistered;
}

void OnServiceRegistered(object? sender, SshService service)
{
    if (service is ConnectionService connection)
    {
        connection.PtyReceived += (_, pty) =>
            Console.WriteLine($"    PTY: {pty.Terminal} {pty.WidthChars}x{pty.HeightRows}");

        connection.WindowChange += (_, wc) =>
            Console.WriteLine($"    Window change: {wc.WidthColumns}x{wc.HeightRows}");

        connection.CommandOpened += OnCommandOpened;
    }
}

void OnCommandOpened(object? sender, CommandRequestedArgs e)
{
    Console.WriteLine($"    Shell type: {e.ShellType}");

    if (e.ShellType != "shell")
        return;  // only handle interactive shells for now

    e.Agreed = true;

    var channel = e.Channel;
    var welcome = Encoding.UTF8.GetBytes("Welcome to SshServer PoC. Your input is echoed back.\r\n> ");
    channel.SendData(welcome);

    channel.DataReceived += (_, data) =>
    {
        // Echo every byte back so the terminal feels responsive, then re-show prompt.
        channel.SendData(data);

        // When the user presses Enter, resend the prompt.
        if (data.Contains((byte)'\r') || data.Contains((byte)'\n'))
            channel.SendData("> "u8.ToArray());
    };

    channel.CloseReceived += (_, _) =>
    {
        Console.WriteLine("[-] Channel closed by client");
        channel.SendClose(0);
    };
}
