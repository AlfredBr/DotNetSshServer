using Spectre.Console;
using Spectre.Console.Rendering;

namespace AlfredBr.SshServer.Core.Demo;

/// <summary>
/// Full-screen dashboard application similar to htop.
/// </summary>
public class DashboardApp : SshShellApplication
{
    private static readonly Random _random = new();
    private static readonly string[] _processNames =
    [
        "sshd", "systemd", "docker", "nginx", "postgres", "redis-server",
        "node", "python3", "dotnet", "java", "go", "rustc", "cargo",
        "vim", "tmux", "bash", "zsh", "fish", "git", "npm", "yarn",
        "webpack", "eslint", "prettier", "jest", "cargo-watch", "ghc"
    ];

    private static readonly string[] _userNames = ["root", "admin", "www-data", "postgres", "redis", "node", "deploy"];
    private static readonly DateTime _startTime = DateTime.Now;

    private bool _isRunning;
    private string _lastAction = "";

    protected override string Prompt => "[magenta]dashboard[/]> ";

    protected override IEnumerable<string> Completions =>
        ["help", "dashboard", "top", "processes", "memory", "cpu", "network", "disk", "clear", "exit"];

    protected override void OnWelcome()
    {
        WriteLine("[bold magenta]Dashboard TUI[/]");
        WriteLine("[dim]Type 'dashboard' or 'top' for full-screen view, 'help' for commands.[/]");
        WriteLine();
    }

    protected override string? OnExec(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";

        return parts[0].ToLowerInvariant() switch
        {
            "help" => GetHelpText(),
            "processes" or "ps" => GetProcessList(),
            "cpu" => GetCpuInfo(),
            "memory" or "mem" => GetMemoryInfo(),
            "disk" or "df" => GetDiskInfo(),
            "network" or "net" => GetNetworkInfo(),
            _ => $"Unknown command: {parts[0]}\nUse 'help' for available commands.\n"
        };
    }

    protected override bool OnCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
            case "?":
                ShowHelp();
                break;

            case "dashboard":
            case "top":
            case "htop":
                ShowFullDashboard();
                break;

            case "processes":
            case "ps":
                ShowProcessTable();
                break;

            case "cpu":
                ShowCpuMetrics();
                break;

            case "memory":
            case "mem":
                ShowMemoryMetrics();
                break;

            case "disk":
            case "df":
                ShowDiskMetrics();
                break;

            case "network":
            case "net":
                ShowNetworkMetrics();
                break;

            case "clear":
                Clear();
                break;

            case "exit":
            case "quit":
            case "q":
                return false;

            default:
                WriteLine($"[red]Unknown command:[/] {Escape(parts[0])}");
                WriteLine("[dim]Type 'help' for available commands.[/]");
                break;
        }

        return true;
    }

    private void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[magenta]Command[/]")
            .AddColumn("[magenta]Description[/]");

        table.AddRow("[cyan]dashboard[/], [cyan]top[/]", "Show full-screen dashboard (like htop)");
        table.AddRow("[cyan]processes[/], [cyan]ps[/]", "Show process list");
        table.AddRow("[cyan]cpu[/]", "Show CPU usage details");
        table.AddRow("[cyan]memory[/], [cyan]mem[/]", "Show memory usage");
        table.AddRow("[cyan]disk[/], [cyan]df[/]", "Show disk usage");
        table.AddRow("[cyan]network[/], [cyan]net[/]", "Show network statistics");
        table.AddRow("[cyan]clear[/]", "Clear the screen");
        table.AddRow("[cyan]exit[/], [cyan]q[/]", "Exit dashboard");

        Write(table);
    }

    private void ShowFullDashboard()
    {
        _isRunning = true;
        _lastAction = "";
        Clear();

        var layout = CreateDashboardLayout();

        Console.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                while (_isRunning)
                {
                    UpdateDashboardLayout(layout);
                    ctx.Refresh();

                    for (int i = 0; i < 10 && _isRunning; i++)
                    {
                        Thread.Sleep(100);

                        if (Console.Input.IsKeyAvailable())
                        {
                            var key = Console.Input.ReadKey(intercept: true);
                            HandleDashboardKey(key);
                        }
                    }
                }
            });

        WriteLine();
        WriteLine("[dim]Dashboard stopped.[/]");
    }

    private void HandleDashboardKey(ConsoleKeyInfo? keyInfo)
    {
        if (keyInfo == null) return;

        var key = keyInfo.Value;

        switch (key.Key)
        {
            case ConsoleKey.F1:
                _lastAction = "[cyan]F1[/] Help: Press F10 or 'q' to exit dashboard";
                break;
            case ConsoleKey.F2:
                _lastAction = "[dim]F2[/] Setup: Configuration not implemented";
                break;
            case ConsoleKey.F3:
                _lastAction = "[dim]F3[/] Search: Process search not implemented";
                break;
            case ConsoleKey.F4:
                _lastAction = "[dim]F4[/] Filter: Process filtering not implemented";
                break;
            case ConsoleKey.F5:
                _lastAction = "[dim]F5[/] Tree: Tree view not implemented";
                break;
            case ConsoleKey.F6:
                _lastAction = "[dim]F6[/] Sort: Column sorting not implemented";
                break;
            case ConsoleKey.F7:
                _lastAction = "[dim]F7[/] Nice-: Decrease priority not implemented";
                break;
            case ConsoleKey.F8:
                _lastAction = "[dim]F8[/] Nice+: Increase priority not implemented";
                break;
            case ConsoleKey.F9:
                _lastAction = "[yellow]F9[/] Kill: Process termination not implemented";
                break;
            case ConsoleKey.F11:
                _lastAction = "[dim]F11[/] (often captured by terminal for fullscreen)";
                break;
            case ConsoleKey.F12:
                _lastAction = "[dim]F12[/] (often captured by terminal/OS)";
                break;
            case ConsoleKey.F10:
                _lastAction = "[red]F10[/] Quit: Exiting dashboard...";
                _isRunning = false;
                break;
            case ConsoleKey.Q:
                _lastAction = "[red]q[/] Quit: Exiting dashboard...";
                _isRunning = false;
                break;
            case ConsoleKey.Spacebar:
                _lastAction = "[dim]Space[/] Tag: Process tagging not implemented";
                break;
            case ConsoleKey.U:
                _lastAction = "[dim]u[/] Untag: Process untagging not implemented";
                break;
            case ConsoleKey.S:
                _lastAction = "[dim]s[/] Setup: Configuration not implemented";
                break;
            case ConsoleKey.H:
                _lastAction = "[cyan]h[/] Help: Press F10 or 'q' to exit dashboard";
                break;
            default:
                if (char.IsLetterOrDigit(key.KeyChar))
                {
                    _lastAction = $"[dim]'{key.KeyChar}'[/] Unknown command";
                }
                break;
        }
    }

    private Layout CreateDashboardLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Body").SplitRows(
                    new Layout("Top").Size(6).SplitColumns(
                        new Layout("CPU"),
                        new Layout("Memory"),
                        new Layout("LoadAvg")
                    ),
                    new Layout("Middle").SplitColumns(
                        new Layout("Processes").Ratio(3),
                        new Layout("Stats").Ratio(1)
                    )
                ),
                new Layout("Action").Size(3),
                new Layout("Footer").Size(3)
            );

        return layout;
    }

    private void UpdateDashboardLayout(Layout layout)
    {
        var now = DateTime.Now;
        var uptime = now - _startTime;

        // Header
        layout["Header"].Update(
            new Panel(new Markup(
                $"[bold magenta]Dashboard[/] │ " +
                $"[cyan]{Environment.MachineName}[/] │ " +
                $"[dim]Uptime:[/] {FormatUptime(uptime)} │ " +
                $"[dim]Time:[/] {now:HH:mm:ss}"))
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0)
            });

        // CPU meters
        var cpuUsages = Enumerable.Range(0, 4).Select(_ => _random.Next(5, 95)).ToList();
        layout["CPU"].Update(CreateCpuPanel(cpuUsages));

        // Memory
        var memUsed = _random.Next(4000, 12000);
        var memTotal = 16384;
        var swapUsed = _random.Next(0, 2048);
        var swapTotal = 4096;
        layout["Memory"].Update(CreateMemoryPanel(memUsed, memTotal, swapUsed, swapTotal));

        // Load Average
        var load1 = _random.NextDouble() * 4;
        var load5 = _random.NextDouble() * 3;
        var load15 = _random.NextDouble() * 2;
        layout["LoadAvg"].Update(CreateLoadPanel(load1, load5, load15));

        // Process table
        layout["Processes"].Update(CreateProcessPanel());

        // Stats sidebar
        layout["Stats"].Update(CreateStatsPanel());

        // Action notification
        var actionText = string.IsNullOrEmpty(_lastAction)
            ? "[dim]Press a key for action. F10 or q to quit.[/]"
            : _lastAction;
        layout["Action"].Update(
            new Panel(new Markup(actionText))
            {
                Header = new PanelHeader("[yellow]Action[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0)
            });

        // Footer
        layout["Footer"].Update(
            new Panel(new Markup(
                "[cyan]F1[/] Help  [dim]F2[/] Setup  [dim]F3[/] Search  [dim]F4[/] Filter  " +
                "[dim]F5[/] Tree  [dim]F6[/] Sort  [dim]F7[/] Nice-  [dim]F8[/] Nice+  " +
                "[yellow]F9[/] Kill  [red]F10[/] Quit  [red]q[/] Exit"))
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0)
            });
    }

    private Panel CreateCpuPanel(List<int> usages)
    {
        var rows = new List<IRenderable>();
        for (int i = 0; i < usages.Count; i++)
        {
            var usage = usages[i];
            var bar = CreateProgressBar(usage, 100, GetCpuColor(usage));
            rows.Add(new Markup($"[dim]CPU{i}[/] {bar} [bold]{usage,3}%[/]"));
        }

        return new Panel(new Rows(rows))
        {
            Header = new PanelHeader("[cyan]CPU[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
    }

    private Panel CreateMemoryPanel(int memUsed, int memTotal, int swapUsed, int swapTotal)
    {
        var memPercent = (int)((double)memUsed / memTotal * 100);
        var swapPercent = swapTotal > 0 ? (int)((double)swapUsed / swapTotal * 100) : 0;

        var memBar = CreateProgressBar(memPercent, 100, GetMemColor(memPercent));
        var swapBar = CreateProgressBar(swapPercent, 100, GetMemColor(swapPercent));

        return new Panel(new Rows(
            new Markup($"[dim]Mem[/]  {memBar} {FormatSize(memUsed)}/{FormatSize(memTotal)}"),
            new Markup($"[dim]Swap[/] {swapBar} {FormatSize(swapUsed)}/{FormatSize(swapTotal)}")))
        {
            Header = new PanelHeader("[yellow]Memory[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
    }

    private Panel CreateLoadPanel(double load1, double load5, double load15)
    {
        var cores = 4;
        var color1 = load1 > cores ? "red" : load1 > cores * 0.7 ? "yellow" : "green";
        var color5 = load5 > cores ? "red" : load5 > cores * 0.7 ? "yellow" : "green";
        var color15 = load15 > cores ? "red" : load15 > cores * 0.7 ? "yellow" : "green";

        return new Panel(new Rows(
            new Markup($"[dim]1 min:[/]  [{color1}]{load1:F2}[/]"),
            new Markup($"[dim]5 min:[/]  [{color5}]{load5:F2}[/]"),
            new Markup($"[dim]15 min:[/] [{color15}]{load15:F2}[/]")))
        {
            Header = new PanelHeader("[blue]Load Average[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
    }

    private Panel CreateProcessPanel()
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn(new TableColumn("[bold]PID[/]").RightAligned())
            .AddColumn("[bold]USER[/]")
            .AddColumn(new TableColumn("[bold]CPU%[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]MEM%[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]TIME+[/]").RightAligned())
            .AddColumn("[bold]COMMAND[/]");

        var processes = GenerateProcessList(12);
        foreach (var p in processes.OrderByDescending(p => p.Cpu))
        {
            var cpuColor = p.Cpu > 50 ? "red" : p.Cpu > 20 ? "yellow" : "green";
            var memColor = p.Mem > 50 ? "red" : p.Mem > 20 ? "yellow" : "dim";

            table.AddRow(
                $"[dim]{p.Pid}[/]",
                $"[cyan]{p.User}[/]",
                $"[{cpuColor}]{p.Cpu:F1}[/]",
                $"[{memColor}]{p.Mem:F1}[/]",
                $"[dim]{p.Time}[/]",
                p.Command);
        }

        return new Panel(table)
        {
            Header = new PanelHeader($"[green]Processes[/] [dim]({processes.Count})[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(0)
        };
    }

    private Panel CreateStatsPanel()
    {
        var tasks = _random.Next(100, 300);
        var running = _random.Next(1, 5);
        var sleeping = tasks - running - _random.Next(0, 10);
        var stopped = _random.Next(0, 3);
        var zombie = _random.Next(0, 2);

        var tcpConns = _random.Next(10, 100);
        var udpConns = _random.Next(5, 30);
        var rxBytes = _random.Next(1000, 50000);
        var txBytes = _random.Next(500, 25000);

        return new Panel(new Rows(
            new Markup("[bold]Tasks[/]"),
            new Markup($"  [green]{running}[/] running"),
            new Markup($"  [dim]{sleeping}[/] sleeping"),
            new Markup($"  [yellow]{stopped}[/] stopped"),
            new Markup($"  [red]{zombie}[/] zombie"),
            new Text(""),
            new Markup("[bold]Network[/]"),
            new Markup($"  TCP: [cyan]{tcpConns}[/]"),
            new Markup($"  UDP: [cyan]{udpConns}[/]"),
            new Markup($"  RX: [green]{FormatBytes(rxBytes)}/s[/]"),
            new Markup($"  TX: [yellow]{FormatBytes(txBytes)}/s[/]")))
        {
            Header = new PanelHeader("[magenta]Stats[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
    }

    private void ShowProcessTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]PID[/]").RightAligned())
            .AddColumn("[bold]USER[/]")
            .AddColumn(new TableColumn("[bold]CPU%[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]MEM%[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]VSZ[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]RSS[/]").RightAligned())
            .AddColumn("[bold]STAT[/]")
            .AddColumn("[bold]COMMAND[/]");

        var processes = GenerateProcessList(20);
        foreach (var p in processes.OrderByDescending(p => p.Cpu))
        {
            var cpuColor = p.Cpu > 50 ? "red" : p.Cpu > 20 ? "yellow" : "green";
            table.AddRow(
                p.Pid.ToString(),
                $"[cyan]{p.User}[/]",
                $"[{cpuColor}]{p.Cpu:F1}[/]",
                $"{p.Mem:F1}",
                FormatSize(p.Vsz),
                FormatSize(p.Rss),
                p.Stat,
                p.Command);
        }

        Write(table);
    }

    private void ShowCpuMetrics()
    {
        var cpuCount = 4;

        Write(new Rule("[cyan]CPU Usage[/]").RuleStyle("dim"));

        for (int i = 0; i < cpuCount; i++)
        {
            var usage = _random.Next(5, 95);
            var user = _random.Next(0, usage);
            var system = usage - user;
            var idle = 100 - usage;

            Write(new BarChart()
                .Width(60)
                .Label($"[bold]CPU {i}[/]")
                .AddItem("User", user, Color.Green)
                .AddItem("System", system, Color.Red)
                .AddItem("Idle", idle, Color.Grey));

            WriteLine();
        }

        // Overall stats
        var table = new Table()
            .Border(TableBorder.Rounded)
            .HideHeaders()
            .AddColumn("Key")
            .AddColumn("Value");

        var avgUsage = _random.Next(20, 70);
        table.AddRow("Average Usage", $"[bold]{avgUsage}%[/]");
        table.AddRow("Processes", $"{_random.Next(100, 300)}");
        table.AddRow("Threads", $"{_random.Next(500, 1500)}");
        table.AddRow("Context Switches", $"{_random.Next(10000, 100000)}/s");
        table.AddRow("Interrupts", $"{_random.Next(5000, 50000)}/s");

        Write(table);
    }

    private void ShowMemoryMetrics()
    {
        var memTotal = 16384;
        var memUsed = _random.Next(4000, 12000);
        var memFree = memTotal - memUsed;
        var memBuffers = _random.Next(500, 2000);
        var memCached = _random.Next(1000, 4000);

        var swapTotal = 4096;
        var swapUsed = _random.Next(0, 2048);
        var swapFree = swapTotal - swapUsed;

        Write(new Rule("[yellow]Memory Usage[/]").RuleStyle("dim"));

        Write(new BarChart()
            .Width(60)
            .Label("[bold]Physical Memory[/]")
            .AddItem("Used", memUsed / 100, Color.Green)
            .AddItem("Buffers", memBuffers / 100, Color.Blue)
            .AddItem("Cached", memCached / 100, Color.Aqua)
            .AddItem("Free", memFree / 100, Color.Grey));

        WriteLine();

        Write(new BarChart()
            .Width(60)
            .Label("[bold]Swap[/]")
            .AddItem("Used", swapUsed / 100, Color.Yellow)
            .AddItem("Free", swapFree / 100, Color.Grey));

        WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Type")
            .AddColumn(new TableColumn("Total").RightAligned())
            .AddColumn(new TableColumn("Used").RightAligned())
            .AddColumn(new TableColumn("Free").RightAligned())
            .AddColumn(new TableColumn("Usage").RightAligned());

        var memPercent = (int)((double)memUsed / memTotal * 100);
        var swapPercent = swapTotal > 0 ? (int)((double)swapUsed / swapTotal * 100) : 0;

        table.AddRow("Memory", FormatSize(memTotal), FormatSize(memUsed), FormatSize(memFree),
            $"[{GetMemColor(memPercent)}]{memPercent}%[/]");
        table.AddRow("Swap", FormatSize(swapTotal), FormatSize(swapUsed), FormatSize(swapFree),
            $"[{GetMemColor(swapPercent)}]{swapPercent}%[/]");

        Write(table);
    }

    private void ShowDiskMetrics()
    {
        Write(new Rule("[blue]Disk Usage[/]").RuleStyle("dim"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Filesystem")
            .AddColumn(new TableColumn("Size").RightAligned())
            .AddColumn(new TableColumn("Used").RightAligned())
            .AddColumn(new TableColumn("Avail").RightAligned())
            .AddColumn(new TableColumn("Use%").RightAligned())
            .AddColumn("Mounted on");

        var disks = new[]
        {
            ("/dev/sda1", 100 * 1024, "ext4", "/"),
            ("/dev/sda2", 500 * 1024, "ext4", "/home"),
            ("/dev/sdb1", 1000 * 1024, "xfs", "/data"),
            ("tmpfs", 8 * 1024, "tmpfs", "/tmp"),
        };

        foreach (var (dev, size, _, mount) in disks)
        {
            var used = _random.Next(size / 10, size * 9 / 10);
            var avail = size - used;
            var percent = (int)((double)used / size * 100);
            var color = percent > 90 ? "red" : percent > 70 ? "yellow" : "green";

            table.AddRow(
                $"[cyan]{dev}[/]",
                FormatSize(size),
                FormatSize(used),
                FormatSize(avail),
                $"[{color}]{percent}%[/]",
                mount);
        }

        Write(table);

        WriteLine();

        Write(new BarChart()
            .Width(60)
            .Label("[bold]Disk I/O[/]")
            .AddItem("Read", _random.Next(10, 100), Color.Green)
            .AddItem("Write", _random.Next(5, 80), Color.Yellow));
    }

    private void ShowNetworkMetrics()
    {
        Write(new Rule("[aqua]Network Statistics[/]").RuleStyle("dim"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Interface")
            .AddColumn(new TableColumn("RX bytes").RightAligned())
            .AddColumn(new TableColumn("TX bytes").RightAligned())
            .AddColumn(new TableColumn("RX pkts").RightAligned())
            .AddColumn(new TableColumn("TX pkts").RightAligned())
            .AddColumn("Status");

        var interfaces = new[] { "eth0", "lo", "docker0", "wlan0" };
        foreach (var iface in interfaces)
        {
            var rxBytes = _random.Next(1000000, 100000000);
            var txBytes = _random.Next(500000, 50000000);
            var rxPkts = _random.Next(10000, 1000000);
            var txPkts = _random.Next(5000, 500000);
            var isUp = iface != "wlan0" || _random.Next(2) == 0;

            table.AddRow(
                $"[cyan]{iface}[/]",
                FormatBytes(rxBytes),
                FormatBytes(txBytes),
                rxPkts.ToString("N0"),
                txPkts.ToString("N0"),
                isUp ? "[green]UP[/]" : "[red]DOWN[/]");
        }

        Write(table);

        WriteLine();

        // Connection stats
        var connPanel = new Panel(new Rows(
            new Markup($"[dim]TCP Established:[/] [green]{_random.Next(20, 100)}[/]"),
            new Markup($"[dim]TCP Time Wait:[/]   [yellow]{_random.Next(5, 30)}[/]"),
            new Markup($"[dim]TCP Close Wait:[/]  [yellow]{_random.Next(0, 10)}[/]"),
            new Markup($"[dim]UDP Sockets:[/]     [blue]{_random.Next(10, 50)}[/]"),
            new Markup($"[dim]Raw Sockets:[/]     [dim]{_random.Next(0, 5)}[/]")))
        {
            Header = new PanelHeader("[cyan]Connection Summary[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };

        Write(connPanel);
    }

    private static string CreateProgressBar(int value, int max, string color)
    {
        const int width = 20;
        var filled = (int)((double)value / max * width);
        var empty = width - filled;

        var filledStr = new string('█', filled);
        var emptyStr = new string('░', empty);

        return $"[{color}]{filledStr}[/][dim]{emptyStr}[/]";
    }

    private static string GetCpuColor(int usage) =>
        usage > 90 ? "red" : usage > 70 ? "yellow" : "green";

    private static string GetMemColor(int percent) =>
        percent > 90 ? "red" : percent > 70 ? "yellow" : "green";

    private static string FormatSize(int mb)
    {
        if (mb >= 1024)
            return $"{mb / 1024.0:F1}G";
        return $"{mb}M";
    }

    private static string FormatBytes(int bytes)
    {
        if (bytes >= 1073741824)
            return $"{bytes / 1073741824.0:F1}G";
        if (bytes >= 1048576)
            return $"{bytes / 1048576.0:F1}M";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1}K";
        return $"{bytes}B";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private List<ProcessInfo> GenerateProcessList(int count)
    {
        var processes = new List<ProcessInfo>();
        for (int i = 0; i < count; i++)
        {
            processes.Add(new ProcessInfo
            {
                Pid = _random.Next(1, 32768),
                User = _userNames[_random.Next(_userNames.Length)],
                Cpu = _random.NextDouble() * 100,
                Mem = _random.NextDouble() * 50,
                Vsz = _random.Next(10000, 500000),
                Rss = _random.Next(5000, 200000),
                Stat = GetRandomStat(),
                Time = FormatProcessTime(_random.Next(0, 86400)),
                Command = _processNames[_random.Next(_processNames.Length)]
            });
        }
        return processes;
    }

    private string GetRandomStat()
    {
        var states = new[] { "S", "R", "D", "Z", "T", "Ss", "Sl", "S+", "R+" };
        return states[_random.Next(states.Length)];
    }

    private static string FormatProcessTime(int seconds)
    {
        var hours = seconds / 3600;
        var mins = (seconds % 3600) / 60;
        var secs = seconds % 60;
        return $"{hours}:{mins:D2}:{secs:D2}";
    }

    private static string GetHelpText()
    {
        return """
            Dashboard Commands:
              dashboard, top  - Show full-screen dashboard
              processes, ps   - Show process list
              cpu             - Show CPU usage
              memory, mem     - Show memory usage
              disk, df        - Show disk usage
              network, net    - Show network stats
              help            - Show this help
              exit, q         - Exit dashboard

            """;
    }

    private string GetProcessList()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("  PID USER      CPU%  MEM%  COMMAND");
        sb.AppendLine("--------------------------------------");

        var processes = GenerateProcessList(10);
        foreach (var p in processes.OrderByDescending(p => p.Cpu).Take(10))
        {
            sb.AppendLine($"{p.Pid,5} {p.User,-8} {p.Cpu,5:F1} {p.Mem,5:F1}  {p.Command}");
        }

        return sb.ToString();
    }

    private string GetCpuInfo()
    {
        var usage = _random.Next(20, 70);
        return $"""
            CPU Usage: {usage}%
            Cores: 4
            Load: {_random.NextDouble() * 4:F2} {_random.NextDouble() * 3:F2} {_random.NextDouble() * 2:F2}

            """;
    }

    private string GetMemoryInfo()
    {
        var total = 16384;
        var used = _random.Next(4000, 12000);
        return $"""
            Memory: {used}M / {total}M ({used * 100 / total}% used)
            Swap: {_random.Next(0, 2048)}M / 4096M

            """;
    }

    private string GetDiskInfo()
    {
        return $"""
            Filesystem      Size  Used  Avail Use%
            /dev/sda1       100G  {_random.Next(20, 80)}G   {_random.Next(20, 80)}G  {_random.Next(30, 90)}%
            /dev/sda2       500G  {_random.Next(100, 400)}G  {_random.Next(100, 400)}G  {_random.Next(30, 80)}%

            """;
    }

    private string GetNetworkInfo()
    {
        return $"""
            Interface  RX bytes    TX bytes    Status
            eth0       {FormatBytes(_random.Next(1000000, 100000000))}     {FormatBytes(_random.Next(500000, 50000000))}     UP
            lo         {FormatBytes(_random.Next(10000, 1000000))}      {FormatBytes(_random.Next(10000, 1000000))}      UP

            """;
    }

    private class ProcessInfo
    {
        public int Pid { get; set; }
        public string User { get; set; } = "";
        public double Cpu { get; set; }
        public double Mem { get; set; }
        public int Vsz { get; set; }
        public int Rss { get; set; }
        public string Stat { get; set; } = "";
        public string Time { get; set; } = "";
        public string Command { get; set; } = "";
    }
}
