# Release Notes

## 2026-02-23 (12)

### Added
- **Ed25519 host key algorithm support**:
  - New `Ed25519Key` class using NSec.Cryptography
  - Server can now accept clients offering ssh-ed25519 keys
  - Added NSec.Cryptography package dependency to SshServer.Core
- **Enhanced `config` command**:
  - Now displays server hostname, process ID, and IP addresses
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
