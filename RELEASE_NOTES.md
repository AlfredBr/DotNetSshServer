# Release Notes

## 2026-02-25 (19)

### Changed
- Renamed NuGet package from `SshServer` to **`AlfredBr.SshServer.Core`** (more specific, follows NuGet naming conventions)
- Reset package version to `1.0.0` under new package ID
- Added `MIT LICENSE` file to repository root
- Fixed `.snupkg` symbol package generation — added `<IncludeSymbols>` and `<SymbolPackageFormat>snupkg</SymbolPackageFormat>` to project
- Added NuGet version, download, and CI status badges to README

---

## 2026-02-24 (18)

### Added
- **DashboardApp** — new full-screen htop-style TUI dashboard:
  - `dashboard` or `top` command launches live updating full-screen view
  - Layouts with CPU meters, memory usage, load averages, process list, and stats sidebar
  - Individual metric views: `cpu`, `memory`, `disk`, `network`, `processes`
  - Progress bars with color-coded thresholds (green/yellow/red)
  - Process table with PID, USER, CPU%, MEM%, STAT, TIME+, COMMAND columns
  - Network stats showing interface status, RX/TX bytes, connection summary
  - Disk usage with filesystem, size, used, avail, and mount point
  - Bar charts for CPU breakdown (user/system/idle) and memory allocation
  - Function key handling with action notification panel (F1-F9 show messages, F10/q to exit)
  - Simulated metrics for demonstration purposes
  - Accessible via `ssh dashboard@host` or from the application menu

---

## 2026-02-25 (18)

### Changed
- Improved NuGet packaging readiness for `SshServer`:
  - Added deterministic pack defaults and symbol package generation (`.snupkg`)
  - Added SourceLink support via `Microsoft.SourceLink.GitHub`
  - Marked `SshServer.Demo` as non-packable
  - Added README instructions for pack, smoke test, and publish to nuget.org
  - Added GitHub Actions workflow `.github/workflows/publish-nuget.yml` for tag-driven and manual NuGet publishing
  - Added GitHub Actions workflow `.github/workflows/pack-validation.yml` to validate pack output on pull requests and `main`

---

## 2026-02-23 (17)

### Changed
- Added GitHub links and Acknowledgements section to README.md for proper attribution to FxSsh, Spectre.Console, NSec.Cryptography, and Charmbracelet Wish

---

## 2026-02-23 (16)

### Fixed
- **Prompt markup rendering** — colored prompts (e.g., `[red]admin[/]>`) now render correctly instead of showing raw markup tags
- **Menu keyboard input** — interactive menu selection now responds to arrow keys properly; fixed by wiring up input handler before displaying prompts
- **Spectre markup escaping** — fixed "malformed markup tag" error in AdminApp help by escaping square brackets in `logs [n]` as `logs [[n]]`
- **Exec output newlines** — all exec command outputs now include proper trailing newlines

### Changed
- Removed `menu` command from AdminApp and MonitoringApp when accessed via direct username mapping (command only applies when using app launcher menu)
- Updated DEVELOPERS.md with multi-application support documentation

---

## 2026-02-23 (15)

### Added
- **Multi-application support** with two routing strategies:

  **Username-based routing** — map usernames directly to apps:
  ```csharp
  .MapUser<DemoApp>("demo")      // ssh demo@host → DemoApp
  .MapUser<AdminApp>("admin")    // ssh admin@host → AdminApp
  ```

  **Interactive menu** — for unmapped usernames:
  ```csharp
  .UseApplicationMenu(menu => menu
      .Add<DemoApp>("Demo", "Spectre.Console showcase")
      .Add<AdminApp>("Admin", "Server administration")
      .ReturnToMenuOnExit(true))
  ```

- **AppLauncherApplication** — internal app that displays selection menu
- **AppMenuConfiguration** — fluent configuration for menu apps
- **Exec command routing** with `appname:command` prefix syntax:
  ```bash
  ssh guest@host "admin:users"   # Run 'users' in AdminApp
  ssh guest@host "demo:status"   # Run 'status' in DemoApp
  ```

- **New demo applications**:
  - `AdminApp` — server administration with users, logs, config commands
  - `MonitoringApp` — live metrics, health checks, alerts, charts

### Changed
- Demo Program.cs now showcases multi-app configuration
- Users can type `menu` or `apps` to return to app selection

### Usage
```bash
# Direct access via username mapping
ssh demo@localhost -p 2222      # → DemoApp
ssh admin@localhost -p 2222     # → AdminApp
ssh monitor@localhost -p 2222   # → MonitoringApp

# Menu access for other usernames
ssh localhost -p 2222           # → Shows app selection menu
ssh guest@localhost -p 2222     # → Shows app selection menu

# Exec with app prefix
ssh guest@localhost "admin:users"
```

---

## 2026-02-23 (14)

### Added
- **NuGet package structure**:
  - Single `SshServer` package containing all functionality
  - Package metadata added to Core project (PackageId, Version, Description, etc.)
  - README.md included in package

- **Fluent builder API** for server configuration:
  - `SshServerHost.CreateBuilder()` — entry point
  - `UsePort()`, `UseBanner()`, `UseHostKeyPath()` — network config
  - `AllowAnonymous()`, `UseAuthorizedKeysFile()` — authentication
  - `UseSessionTimeout()`, `UseMaxConnections()` — session management
  - `UseLogLevel()`, `ConfigureLogging()` — logging
  - `UseApplication<TApp>()` — register shell application
  - `UseDefaultConfiguration()` — load appsettings.json/env/CLI
  - `Build()` returns `SshServerHost` with `RunAsync()`

- **SshServerHost class**:
  - Main server lifecycle management
  - Implements `IAsyncDisposable`
  - `RunAsync()` starts server and waits for cancellation
  - `StopAsync()` gracefully stops the server
  - Internal connection handling extracted from Program.cs

### Changed
- **Project reorganization**:
  - `SshServer.Host` renamed to `SshServer.Demo`
  - TUI infrastructure moved from Host to Core (`SshServer.Core/Hosting/`)
  - Namespaces changed from `SshServer.Host` to `SshServer`
  - All dependencies consolidated in Core package

- **Simplified demo Program.cs**:
  - Reduced from 335 lines to 25 lines
  - Now uses builder API exclusively

### Migration Guide
Before:
```csharp
// Complex setup in Program.cs with manual event wiring
var server = new FxSsh.SshServer(...);
server.ConnectionAccepted += OnConnectionAccepted;
// ... 300+ lines of event handling
```

After:
```csharp
await SshServerHost.CreateBuilder()
    .UseDefaultConfiguration(args)
    .UseApplication<DemoApp>()
    .Build()
    .RunAsync();
```

---

## 2026-02-23 (13)

### Added
- **Exec channel support** for scripted SSH commands:
  - Run single commands via `ssh -p 2222 localhost <command>`
  - Supported commands: `status`, `whoami`, `config`, `help`
  - `OnExec(string command)` virtual method in `SshShellApplication`
  - Enables scripting and automation scenarios

---

## 2026-02-23 (12)

### Added
- **Ed25519 host key algorithm support**:
  - New `Ed25519Key` class using NSec.Cryptography
  - Server can now accept clients offering ssh-ed25519 keys
  - Added NSec.Cryptography package dependency to SshServer.Core
- **Enhanced `config` command**:
  - Now displays server hostname, OS version, process ID, and IP addresses
- **Enhanced `status` command**:
  - Now displays server process ID
- **Session timeout**:
  - `SessionTimeoutMinutes` config option (0 = disabled)
  - Automatically disconnects idle sessions after configured duration
  - Displays timeout message before disconnecting
- **SshShellApplication abstract base class**:
  - New foundation for building SSH TUI applications
  - Handles all SSH plumbing (PTY, line editing, Spectre.Console setup)
  - Override `OnCommand()` to handle commands
  - Virtual `Prompt`, `Completions`, `OnWelcome()`, `OnConnect()`, `OnDisconnect()`
  - Built-in helpers: `WriteLine()`, `Ask()`, `Confirm()`, `Select()`, `MultiSelect()`, `Status()`, `Progress()`
- **DemoApp** — refactored demo as `SshShellApplication` subclass
- **Updated documentation**:
  - README.md with comprehensive feature list
  - DEVELOPERS.md guide for building custom applications

---

## 2026-02-22 (11)

### Added
- **Public key authentication**:
  - `AuthorizedKeysPath` config option for authorized_keys file
  - Supports ssh-rsa, ssh-ed25519, ecdsa-sha2-nistp256/384/521
  - Standard OpenSSH authorized_keys format
- **Anonymous access control**:
  - `AllowAnonymous` config option (default: true for dev)
  - Set to false in production to require authentication
- **AuthorizedKeysStore** class for parsing and validating public keys
- **New commands**:
  - `config`: Display current server configuration
  - `whoami`: Now shows username, auth method, and key fingerprint

### Changed
- Anonymous auth now fires UserAuth event (previously auto-accepted)
- UserAuthArgs.Result defaults to false for anonymous (must be explicitly allowed)
- CommandHandler now receives ConnectionInfo and SshServerOptions

---

## 2026-02-22 (10)

### Changed
- **Major refactoring**: Extracted `LineEditor` class from Program.cs
  - Program.cs reduced from 714 to 261 lines
  - LineEditor is now a reusable, testable component (554 lines)
  - Clear separation: Program.cs handles server orchestration, LineEditor handles input

---

## 2026-02-22 (9)

### Added
- **New Spectre.Console commands**:
  - `tree`: Hierarchical tree display (project structure demo)
  - `chart`: Bar chart visualization (language popularity, server metrics)
- **Enhanced line editing**:
  - Home/End keys: move to beginning/end of line
  - Delete key: delete character under cursor
  - Alt-B: move back one word
  - Alt-F: move forward one word
  - Alt-D: delete word forward
  - Ctrl-Y: yank (paste) from kill ring
- **Kill ring**: Ctrl-K and Ctrl-U now save deleted text for Ctrl-Y to paste

---

## 2026-02-22 (8)

### Added
- **Arrow key support** in line editor
  - Up/Down arrows: navigate command history (same as Ctrl-P/Ctrl-N)
  - Left/Right arrows: move cursor (same as Ctrl-B/Ctrl-F)
  - Implemented via ANSI escape sequence parsing

---

## 2026-02-22 (7)

### Added
- **Tab completion** for command names
  - Single match: auto-completes the command
  - Multiple matches: displays all matching commands
  - No match: does nothing
  - Only completes first word (command name)

---

## 2026-02-22 (6)

### Added
- **Configuration system** via `appsettings.json`:
  - `Port`: Server listening port (default: 2222)
  - `Banner`: SSH protocol banner
  - `HostKeyPath`: Path to host key file
  - `MaxConnections`: Connection limit (default: 100)
  - `LogLevel`: Minimum log level (default: Debug)
  - Environment variables supported with `SSHSERVER_` prefix
  - Command-line arguments supported
- **Graceful shutdown**:
  - Ctrl+C on server cleanly disconnects all sessions
  - Proper handling of SIGTERM/ProcessExit
  - Server.Stop() called with cleanup logging
- **Live display commands**:
  - `progress`: Multi-task progress bars with spinners
  - `spinner`: Status spinner with changing messages
  - `live`: Live-updating metrics table

---

## 2026-02-22 (5)

### Added
- **Command history** with Ctrl-P (previous) and Ctrl-N (next)
  - Per-connection history storage
  - Avoids duplicate consecutive commands
  - Saves current line when navigating, restores when returning to end

---

## 2026-02-22 (4)

### Added
- **Interactive Spectre.Console prompts**:
  - `SshAnsiConsoleInput`: Feeds SSH channel input to Spectre's ReadKey
  - `EscapeSequenceParser`: Parses arrow keys, Home, End, Delete, F-keys from ANSI sequences
  - `SshAnsiConsoleWrapper`: Injects custom input handler into IAnsiConsole
- **New interactive commands**:
  - `menu`: Selection prompt with actions
  - `select`: Choose from a list of items
  - `multi`: Multi-selection with space to toggle
  - `confirm`: Yes/No confirmation
  - `ask`: Text input with validation
  - `demo`: Runs all interactive demos
- **Input mode switching**: Seamlessly switches between line editor and Spectre prompts
- **Async command execution**: Commands run on background thread to allow input routing

---

## 2026-02-22 (3)

### Added
- **Spectre.Console integration** for rich terminal output
  - `SshTextWriter`: Custom TextWriter directing output to SSH channel with CRLF translation
  - `SshAnsiConsoleOutput`: IAnsiConsoleOutput implementation with terminal dimensions
  - `SshConsoleFactory`: Creates per-connection IAnsiConsole instances
  - `CommandHandler`: Processes commands with Spectre-rendered output
- **Commands**: `help`, `status`, `whoami`, `clear`, `quit`
- **Rich output**: Tables, panels, rules, and colored markup

### Changed
- Welcome message now rendered via Spectre.Console Rule widget
- Terminal resize events update Spectre console dimensions

---

## 2026-02-22 (2)

### Added
- **Emacs-style line editing** with cursor position tracking:
  - Ctrl-A: Beginning of line
  - Ctrl-E: End of line
  - Ctrl-B: Back one character
  - Ctrl-F: Forward one character
  - Ctrl-D: Delete character under cursor (or disconnect if line empty)
  - Ctrl-K: Kill from cursor to end of line
  - Ctrl-U: Kill from beginning of line to cursor
  - Ctrl-L: Clear screen and redraw
- **Mid-line editing** - Insert and delete anywhere in the line, not just at end

### Changed
- **Ctrl-C now disconnects** (was Ctrl-D)
- Backspace/DEL now work correctly with cursor in middle of line

### Fixed
- Mid-line character insertion now displays correctly (was showing duplicate of character under cursor)

---

## 2026-02-22

### Added
- **Structured logging with Microsoft.Extensions.Logging** - Console provider with configurable log levels
- **Unique connection identifiers** - Format: `{IP}:{Port}-{4-char-guid}` (e.g., `192.168.1.5:52341-a3f2`)
- **Logging scopes** - All log messages within a connection are tagged with its connection ID
- **Data logging** - Received data is logged with printable ASCII and escape sequences for control characters
- **Ctrl+D support** - Pressing Ctrl+D (ASCII 0x04 EOT) gracefully closes the SSH channel without terminating the server
- **Session.RemoteEndPoint property** - Exposed socket endpoint on Session class for connection identification

### Changed
- Replaced `Console.WriteLine` calls with structured `ILogger` calls throughout Program.cs
- Data truncation for logging: messages longer than 128 bytes are truncated with byte count

### Technical Details
- Added `Microsoft.Extensions.Logging.Console` package to SshServer.Host
- Connection ID generated at `ConnectionAccepted` event and propagated via closures
- Escape sequence formatting handles all ASCII control characters (0x00-0x1F) and DEL (0x7F)
