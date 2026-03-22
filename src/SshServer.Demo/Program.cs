using AlfredBr.SshServer.Core;

namespace AlfredBr.SshServer.Core.Demo;

public class Program
{
    public static async Task Main(string[] args)
    {
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
            .UseMaxConnections(100)

            // Map specific usernames directly to apps (no menu)
            .MapUser<DemoApp>("demo")
            .MapUser<AdminApp>("admin")
            .MapUser<MonitoringApp>("monitor")
            .MapUser<DashboardApp>("dashboard")

            // Unknown usernames get the app selection menu
            .UseApplicationMenu(menu => menu
                .WithTitle("Select an application:")
                .Add<DemoApp>("Demo", "Spectre.Console showcase with tables, charts, and prompts")
                .Add<AdminApp>("Admin", "Server administration and log viewer")
                .Add<MonitoringApp>("Monitor", "Live system metrics and health dashboard")
                .Add<DashboardApp>("Dashboard", "Full-screen htop-style system dashboard")
                .SetDefaultForExec("Demo")
                .ReturnToMenuOnExit(true))

            .Build();

        await host.RunAsync(cts.Token);
    }
}
