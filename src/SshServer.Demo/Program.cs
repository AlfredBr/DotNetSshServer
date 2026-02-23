using SshServer;
using SshServer.Demo;

// ── graceful shutdown ──────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (!cts.IsCancellationRequested)
    {
        cts.Cancel();
    }
};

// ── server startup ─────────────────────────────────────────────────────────────

await using var host = SshServerHost.CreateBuilder()
    .UseDefaultConfiguration(args)
    .UseApplication<DemoApp>()
    .Build();

await host.RunAsync(cts.Token);
