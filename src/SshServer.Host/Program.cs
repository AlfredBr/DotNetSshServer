using System.Net;
using System.Text;

using FxSsh;
using FxSsh.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SshServer;
using SshServer.Host;
using SshServer.Host.Tui;

const int MaxDataLogLength = 128;

// ── configuration ──────────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("SSHSERVER_")
    .AddCommandLine(args)
    .Build();

var options = new SshServerOptions();
configuration.GetSection(SshServerOptions.SectionName).Bind(options);

// Parse log level from config
var logLevel = Enum.TryParse<LogLevel>(options.LogLevel, ignoreCase: true, out var level)
    ? level
    : LogLevel.Debug;

// ── logging ────────────────────────────────────────────────────────────────────

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(opts =>
    {
        opts.SingleLine = true;
        opts.TimestampFormat = "HH:mm:ss.fff ";
    });
    builder.SetMinimumLevel(logLevel);
});

var logger = loggerFactory.CreateLogger("SshServer");

// ── graceful shutdown ──────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    logger.LogInformation("Shutdown requested (Ctrl+C)");
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (!cts.IsCancellationRequested)
    {
        logger.LogInformation("Process exit requested");
        cts.Cancel();
    }
};

// ── server startup ─────────────────────────────────────────────────────────────

var server = new global::FxSsh.SshServer(new StartingInfo(IPAddress.IPv6Any, options.Port, options.Banner));

HostKeyStore.EnsureAndRegister(server, options.HostKeyPath);

server.ConnectionAccepted += OnConnectionAccepted;
server.ExceptionRaised += (_, ex) => logger.LogError(ex, "Server exception");

server.Start();
logger.LogInformation("SSH server listening on port {Port}. Press Ctrl+C to stop.", options.Port);

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Expected on shutdown
}

// ── graceful shutdown sequence ─────────────────────────────────────────────────

logger.LogInformation("Stopping server...");
server.Stop();
logger.LogInformation("Server stopped");

// ── helpers ────────────────────────────────────────────────────────────────────

string GenerateConnectionId(Session session)
{
    var endpoint = session.RemoteEndPoint as IPEndPoint;
    var ip = endpoint?.Address.ToString() ?? "unknown";
    var port = endpoint?.Port ?? 0;
    var shortGuid = Guid.NewGuid().ToString("N")[..4];
    return $"{ip}:{port}-{shortGuid}";
}

string FormatByte(byte b) => b switch
{
    0x00 => "\\0",
    0x04 => "\\x04",
    0x07 => "\\a",
    0x08 => "\\b",
    0x09 => "\\t",
    0x0A => "\\n",
    0x0D => "\\r",
    0x1B => "\\e",
    0x7F => "\\x7F",
    _ when b < 0x20 => $"\\x{b:X2}",
    _ when b < 0x7F => ((char)b).ToString(),
    _ => $"\\x{b:X2}"
};

string FormatData(byte[] data)
{
    var sb = new StringBuilder();
    var limit = Math.Min(data.Length, MaxDataLogLength);

    for (int i = 0; i < limit; i++)
    {
        var b = data[i];
        sb.Append(b switch
        {
            0x00 => "\\0",
            0x04 => "\\x04",
            0x07 => "\\a",
            0x08 => "\\b",
            0x09 => "\\t",
            0x0A => "\\n",
            0x0D => "\\r",
            0x1B => "\\e",
            0x7F => "\\x7F",
            _ when b < 0x20 => $"\\x{b:X2}",
            _ when b < 0x7F => (char)b,
            _ => $"\\x{b:X2}"
        });
    }

    if (data.Length > MaxDataLogLength)
        sb.Append($"[...+{data.Length - MaxDataLogLength} bytes]");

    return sb.ToString();
}

// ── event handlers ─────────────────────────────────────────────────────────────

void OnConnectionAccepted(object? sender, Session session)
{
    var connId = GenerateConnectionId(session);
    logger.LogInformation("[{ConnId}] Connection accepted", connId);

    session.ServiceRegistered += (_, service) => OnServiceRegistered(service, connId);
    session.Disconnected += (_, _) => logger.LogInformation("[{ConnId}] Disconnected", connId);
}

void OnServiceRegistered(SshService service, string connId)
{
    if (service is not ConnectionService connection)
        return;

    // Track terminal dimensions from PTY request
    int termWidth = 80;
    int termHeight = 24;
    SshAnsiConsoleOutput? consoleOutput = null;

    connection.PtyReceived += (_, pty) =>
    {
        termWidth = (int)pty.WidthChars;
        termHeight = (int)pty.HeightRows;
        logger.LogInformation("[{ConnId}] PTY: {Terminal} {Width}x{Height}", connId, pty.Terminal, termWidth, termHeight);
    };

    connection.WindowChange += (_, wc) =>
    {
        logger.LogDebug("[{ConnId}] Window: {Width}x{Height}", connId, wc.WidthColumns, wc.HeightRows);
        consoleOutput?.UpdateSize((int)wc.WidthColumns, (int)wc.HeightRows);
    };

    connection.CommandOpened += (_, e) =>
    {
        consoleOutput = OnCommandOpened(e, connId, termWidth, termHeight);
    };
}

SshAnsiConsoleOutput? OnCommandOpened(CommandRequestedArgs e, string connId, int termWidth, int termHeight)
{
    logger.LogInformation("[{ConnId}] Shell: {ShellType}", connId, e.ShellType);

    if (e.ShellType != "shell")
        return null;

    e.Agreed = true;

    var channel = e.Channel;
    var lineBuffer = new StringBuilder();
    var cursorPos = 0;
    var shouldDisconnect = false;

    // Command history
    var history = new List<string>();
    var historyIndex = 0;
    var savedLine = ""; // Saves current line when navigating history

    // Input mode: false = line editing, true = Spectre prompt
    var inPromptMode = false;

    // Create Spectre console for this connection
    var ctx = SshConsoleFactory.Create(channel, termWidth, termHeight);
    var console = ctx.Console;
    var consoleOutput = ctx.Output;
    var consoleInput = ctx.Input;
    var commandHandler = new CommandHandler(console, connId);

    // Helper: send escape sequence to move cursor left N positions
    void CursorLeft(int n)
    {
        if (n > 0)
            channel.SendData(Encoding.ASCII.GetBytes($"\x1b[{n}D"));
    }

    // Helper: send escape sequence to move cursor right N positions
    void CursorRight(int n)
    {
        if (n > 0)
            channel.SendData(Encoding.ASCII.GetBytes($"\x1b[{n}C"));
    }

    // Helper: redraw from cursor to end, then reposition cursor
    void RedrawFromCursor()
    {
        // Write everything from cursorPos to end
        var tail = lineBuffer.ToString(cursorPos, lineBuffer.Length - cursorPos);
        channel.SendData(Encoding.UTF8.GetBytes(tail));
        // Clear any garbage after
        channel.SendData("\x1b[K"u8.ToArray());
        // Move cursor back to cursorPos
        CursorLeft(lineBuffer.Length - cursorPos);
    }

    // Helper: clear line and redraw entirely (for Ctrl-L or after kill)
    void RedrawLine()
    {
        // Move to beginning, clear line, write buffer, reposition
        channel.SendData(Encoding.ASCII.GetBytes($"\r> {lineBuffer}"));
        channel.SendData("\x1b[K"u8.ToArray());
        CursorLeft(lineBuffer.Length - cursorPos);
    }

    // Helper: replace current line with text and move cursor to end
    void ReplaceLine(string text)
    {
        // Clear current line visually
        channel.SendData("\r> "u8.ToArray());
        channel.SendData("\x1b[K"u8.ToArray());

        // Update buffer and cursor
        lineBuffer.Clear();
        lineBuffer.Append(text);
        cursorPos = lineBuffer.Length;

        // Display new content
        if (text.Length > 0)
            channel.SendData(Encoding.UTF8.GetBytes(text));
    }

    void Disconnect()
    {
        logger.LogInformation("[{ConnId}] Disconnecting", connId);
        channel.SendData("\r\nGoodbye!\r\n"u8.ToArray());
        channel.SendEof();
        channel.SendClose(0);
        shouldDisconnect = true;
    }

    // Welcome message via Spectre
    commandHandler.ShowWelcome();
    channel.SendData("> "u8.ToArray());

    channel.DataReceived += (_, data) =>
    {
        // If in prompt mode, route all input to Spectre
        if (inPromptMode)
        {
            consoleInput.EnqueueData(data);
            return;
        }

        foreach (var b in data)
        {
            logger.LogTrace("[{ConnId}] Char: {Char}", connId, FormatByte(b));

            switch (b)
            {
                case 0x03: // Ctrl-C - disconnect
                    Disconnect();
                    return;

                case 0x04: // Ctrl-D - delete char under cursor, or disconnect if empty
                    if (lineBuffer.Length == 0)
                    {
                        Disconnect();
                        return;
                    }
                    if (cursorPos < lineBuffer.Length)
                    {
                        lineBuffer.Remove(cursorPos, 1);
                        RedrawFromCursor();
                    }
                    break;

                case 0x01: // Ctrl-A - beginning of line
                    CursorLeft(cursorPos);
                    cursorPos = 0;
                    break;

                case 0x05: // Ctrl-E - end of line
                    CursorRight(lineBuffer.Length - cursorPos);
                    cursorPos = lineBuffer.Length;
                    break;

                case 0x02: // Ctrl-B - back one char
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        CursorLeft(1);
                    }
                    break;

                case 0x06: // Ctrl-F - forward one char
                    if (cursorPos < lineBuffer.Length)
                    {
                        cursorPos++;
                        CursorRight(1);
                    }
                    break;

                case 0x10: // Ctrl-P - previous command in history
                    if (history.Count > 0)
                    {
                        if (historyIndex == history.Count)
                        {
                            // Save current line before navigating
                            savedLine = lineBuffer.ToString();
                        }
                        if (historyIndex > 0)
                        {
                            historyIndex--;
                            ReplaceLine(history[historyIndex]);
                        }
                    }
                    break;

                case 0x0E: // Ctrl-N - next command in history
                    if (historyIndex < history.Count)
                    {
                        historyIndex++;
                        if (historyIndex == history.Count)
                        {
                            // Restore saved line
                            ReplaceLine(savedLine);
                        }
                        else
                        {
                            ReplaceLine(history[historyIndex]);
                        }
                    }
                    break;

                case 0x0B: // Ctrl-K - kill to end of line
                    if (cursorPos < lineBuffer.Length)
                    {
                        lineBuffer.Length = cursorPos;
                        channel.SendData("\x1b[K"u8.ToArray());
                    }
                    break;

                case 0x15: // Ctrl-U - kill to beginning of line
                    if (cursorPos > 0)
                    {
                        lineBuffer.Remove(0, cursorPos);
                        cursorPos = 0;
                        RedrawLine();
                    }
                    break;

                case 0x0C: // Ctrl-L - clear screen, redraw
                    channel.SendData("\x1b[2J\x1b[H"u8.ToArray()); // Clear screen, cursor home
                    channel.SendData("> "u8.ToArray());
                    if (lineBuffer.Length > 0)
                    {
                        channel.SendData(Encoding.UTF8.GetBytes(lineBuffer.ToString()));
                        CursorLeft(lineBuffer.Length - cursorPos);
                    }
                    break;

                case 0x08: // Ctrl-H (Backspace)
                case 0x7F: // DEL
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        lineBuffer.Remove(cursorPos, 1);
                        channel.SendData("\b"u8.ToArray());
                        RedrawFromCursor();
                    }
                    break;

                case (byte)'\r':
                case (byte)'\n':
                    channel.SendData("\r\n"u8.ToArray());
                    if (lineBuffer.Length > 0)
                    {
                        var command = lineBuffer.ToString();
                        logger.LogDebug("[{ConnId}] Command: {Command}", connId, command);

                        // Add to history (avoid duplicates of last command)
                        if (history.Count == 0 || history[^1] != command)
                        {
                            history.Add(command);
                        }
                        historyIndex = history.Count; // Reset to end
                        savedLine = "";

                        lineBuffer.Clear();
                        cursorPos = 0;

                        // Execute command asynchronously to allow input routing during prompts
                        inPromptMode = true;
                        Task.Run(() =>
                        {
                            try
                            {
                                if (!commandHandler.Execute(command))
                                {
                                    Disconnect();
                                    return;
                                }
                            }
                            finally
                            {
                                inPromptMode = false;
                                consoleInput.Clear(); // Clear any leftover input
                                channel.SendData("> "u8.ToArray());
                            }
                        });
                        return; // Exit the foreach - we're now in prompt mode
                    }
                    channel.SendData("> "u8.ToArray());
                    break;

                default:
                    if (b >= 0x20 && b < 0x7F)
                    {
                        if (cursorPos == lineBuffer.Length)
                        {
                            // Append at end - simple case
                            lineBuffer.Append((char)b);
                            cursorPos++;
                            channel.SendData([b]);
                        }
                        else
                        {
                            // Insert in middle
                            lineBuffer.Insert(cursorPos, (char)b);
                            RedrawFromCursor(); // Redraw from cursorPos (shows new char + rest)
                            cursorPos++;
                            CursorRight(1); // Move cursor past the inserted char
                        }
                    }
                    break;
            }
        }
    };

    channel.CloseReceived += (_, _) =>
    {
        logger.LogInformation("[{ConnId}] Channel closed by client", connId);
        channel.SendClose(0);
    };

    return consoleOutput;
}
