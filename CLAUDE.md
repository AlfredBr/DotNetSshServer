# Claude Working Notes — SSH Server Project

## Project Context

This project builds a .NET 10 SSH server whose ultimate purpose is to serve a TUI application
over SSH. The end goal is something similar to how Charmbracelet's Wish library works in Go,
but in C#.

## Key Decisions & Rationale

### Use FxSsh as the base
FxSsh (https://github.com/Aimeast/FxSsh) is the only maintained, MIT-licensed, .NET 8 SSH
server implementation found. It has no external dependencies and already handles the hardest
parts (transport layer crypto, key exchange, PTY negotiation). We will either vendor its source
or fork it to add our extensions.

### Anonymous auth via `none` method
RFC 4252 §5.2 defines a `none` authentication method that clients can use for anonymous
access. FxSsh does not implement this out of the box, but the auth layer is modular enough
that we can add a handler that accepts `SSH_USERAUTH_REQUEST` with method `none` and
immediately responds with `SSH_USERAUTH_SUCCESS`.

### Vendor source vs. use NuGet package
Preference: **vendor the source** (copy FxSsh source files into this repo). This gives us full
control to modify auth logic, add anonymous access, and adjust channel handling without
maintaining a fork. NuGet package is fine for evaluation but not for long-term customisation.

### Host keys
For PoC: auto-generate an RSA or ECDSA key on first run and save to disk. .NET 8 has built-in
support for `ECDsa.Create(ECCurve.NamedCurves.nistP256)` and RSA. Ed25519 host keys are
preferred by modern clients but require .NET 7+ (`ECDiffieHellmanCng` + manual curve) or
BouncyCastle — evaluate later.

## SSH Protocol Quick Reference

### Handshake sequence
1. TCP connect
2. Version string exchange (`SSH-2.0-...`)
3. `SSH_MSG_KEXINIT` — algorithm negotiation
4. Key exchange messages (ECDH or DH)
5. `SSH_MSG_NEWKEYS` — switch to encrypted transport
6. `SSH_MSG_SERVICE_REQUEST` (auth service)
7. `SSH_MSG_USERAUTH_REQUEST` with method `none`
8. `SSH_MSG_USERAUTH_SUCCESS`
9. `SSH_MSG_SERVICE_REQUEST` (connection service)
10. `SSH_MSG_CHANNEL_OPEN` (session)
11. `SSH_MSG_CHANNEL_REQUEST` pty-req
12. `SSH_MSG_CHANNEL_REQUEST` shell
13. Data flows over channel

### Message types relevant to TUI
- `SSH_MSG_CHANNEL_DATA` (94) — terminal input/output
- `SSH_MSG_CHANNEL_REQUEST` with `window-change` — terminal resize
- `SSH_MSG_CHANNEL_EOF` / `SSH_MSG_CHANNEL_CLOSE` — session end

## Open Questions

- Should the TUI run in-process (one instance per connection) or out-of-process?
  - In-process is simpler for PoC; out-of-process is safer for production (isolation).
- How do we virtualise `Console.In` / `Console.Out` per SSH connection?
  - `Console.SetIn` / `Console.SetOut` are global — need per-connection abstraction.
  - Spectre.Console supports custom `IAnsiConsole` instances — this may be the right hook.
- What port? Default `2222` for dev (avoids needing admin rights); `22` for production.

## Approach When Writing Code

- Keep the SSH layer and the TUI layer loosely coupled via a simple stream abstraction
  (`Stream` or a custom `ITerminalChannel` interface).
- Prioritise readability over cleverness — this codebase is exploratory.
- Add XML doc comments to public API surface; inline comments for non-obvious protocol logic.
- Use `Microsoft.Extensions.Logging` from the start so logging is not bolted on later.

## Useful Links

- FxSsh source: https://github.com/Aimeast/FxSsh
- RFC 4252 (auth, includes `none` method): https://datatracker.ietf.org/doc/html/rfc4252
- RFC 4254 (channels, PTY): https://datatracker.ietf.org/doc/html/rfc4254
- Spectre.Console custom console: https://spectreconsole.net/best-practices

## Build tips
- dotnet clean; dotnet build /p:Platform="Any CPU"
- Always update RELEASENOTES.md after every significant change
- Always update README.md as we add dependencies, the strategy or architecture changes significantly or new features are added.

