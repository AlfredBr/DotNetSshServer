# .NET SSH Server — Project Plan

## Goal

Build a lightweight SSH server library in C# (.NET 10) that allows terminal clients to connect
via SSH and interact with a TUI application. This whole thing was inspired by charmbracelet's [wish](https://github.com/charmbracelet/wish) package in Go and the various TUI applications I have been enjoying recently.  The initial proof-of-concept uses anonymous
authentication; additional auth methods and features will be layered in over time.

## Intended Architecture

```
┌─────────────────────────────┐
│  TUI Application Layer      │  Spectre.Console or Terminal.Gui
├─────────────────────────────┤
│  SSH Shell/PTY Glue         │  maps SSH channels to TUI stdin/stdout
├─────────────────────────────┤
│  SSH Server Library         │  FxSsh (modernised) or custom build
├─────────────────────────────┤
│  TCP Listener               │  System.Net.Sockets.TcpListener
└─────────────────────────────┘
```

## Starting Point: FxSsh

Repository: https://github.com/Aimeast/FxSsh
NuGet: `FxSsh` v1.3.0
License: MIT
Last commit: January 2025

### Why FxSsh

- Targets .NET 8 exclusively — no legacy baggage
- Zero external NuGet dependencies (uses only BCL crypto)
- Implements PTY requests and shell channels — exactly what a TUI needs
- Small codebase (~51 KB), readable and forkable
- Tested against OpenSSH, PuTTY, and WinSCP clients
- RFC compliant: 4250–4254, 4344, 5656, 6668, 8332

### What FxSsh Already Provides

| Feature | Status |
|---|---|
| TCP socket handling | ✓ |
| SSH transport (key exchange, encryption, MAC) | ✓ |
| Key exchange: DH, ECDH | ✓ |
| Encryption: AES-CTR (128/192/256) | ✓ |
| MAC: HMAC-SHA2-256/512 | ✓ |
| Host keys: RSA (sha2-256/512), ECDSA (nistp256/384/521) | ✓ |
| Password authentication | ✓ |
| Public key authentication | ✓ |
| PTY requests + window resize | ✓ |
| Shell channels | ✓ |
| Exec channels | ✓ |
| SFTP subsystem | ✓ |
| Anonymous authentication | ✗ — needs custom extension |

### What Needs to Be Added / Changed

1. **Anonymous authentication** — extend `UserAuthService` to accept a connection without
   credentials. SSH protocol allows this via `none` auth method (RFC 4252 §5.2).
2. **TUI I/O bridge** — wire a shell channel's stdin/stdout streams to the TUI framework's
   console abstraction.
3. **Host key generation on first run** — auto-generate and persist an Ed25519 host key if
   none exists, so the server is zero-config for development.

## Phased Roadmap

### Phase 0 — Gather the parts
- [ ] Fork or vendor FxSsh source into this repo
- [ ] Upgrade framework to .net10
- [ ] Upgrade libraries to .net10 versions
- [ ] Modernize codebase, eliminate warnings in build

### Phase 1 — Proof of Concept (anonymous access + PTY shell)
- [ ] Extend auth layer to support `none` auth method
- [ ] Wire a shell channel to a simple echo TUI (proves the pipe works)
- [ ] Auto-generate Ed25519 host key on first start
- [ ] Connect successfully from OpenSSH client (`ssh -o StrictHostKeyChecking=no localhost -p 2222`)

### Phase 2 — TUI Integration
- [ ] Integrate Spectre.Console (or Terminal.Gui) rendering into the shell channel stream
- [ ] Handle PTY resize events (`SSH_MSG_CHANNEL_REQUEST` with `window-change`)
- [ ] Support multiple concurrent connections

### Phase 3 — Hardening
- [ ] Password authentication (simple username/password lookup)
- [ ] Public key authentication
- [ ] Rate limiting / connection limits
- [ ] Logging (Microsoft.Extensions.Logging)

### Phase 4 — Production Readiness
- [ ] Configuration via `IOptions<SshServerOptions>`
- [ ] Graceful shutdown (`CancellationToken` propagation)
- [ ] Unit tests for transport and auth layers
- [ ] NuGet package (optional)

## Key RFCs for Reference

| RFC | Topic |
|---|---|
| 4251 | SSH Protocol Architecture |
| 4252 | SSH Authentication Protocol (includes `none` auth) |
| 4253 | SSH Transport Layer Protocol |
| 4254 | SSH Connection Protocol (channels, PTY) |
| 5656 | ECDH key exchange |
| 8032 | Ed25519 signing |

## NuGet Packages to Evaluate

| Package | Purpose |
|---|---|
| `FxSsh` | Core SSH server (may vendor source instead) |
| `Microsoft.Extensions.Hosting` | Hosted service / DI integration |
| `Microsoft.Extensions.Logging` | Structured logging |
| `Spectre.Console` | TUI rendering (Phase 2) |
| `BouncyCastle.Cryptography` | Fallback crypto if BCL lacks something (e.g. Ed25519 host keys pre-.NET 8) |



