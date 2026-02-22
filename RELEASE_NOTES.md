# Release Notes

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
