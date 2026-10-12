# Sparky VDM Reverse SSH Implementation Plan

> **For agentic implementers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Follow the approved design in `docs/superpowers/specs/2026-10-11-sparky-vdm-reverse-ssh-design.md` as the source of truth.

**Goal:** Run Sparky as an auto-starting Windows service on the AI PC and maintain one restricted reverse SSH forward from VDM to its HTTPS loopback API, ready for later game-server integration.

**Architecture:** Extend the existing `Perpetuum.ItemStatsExporter --serve` host rather than creating a second API. Keep Kestrel on IPv4 loopback, add HTTPS with a machine-specific certificate, and add a hosted tunnel supervisor that starts and monitors the Windows `ssh.exe` client. Package the self-contained `win-x64` application as a separate WiX installer modeled on `Perpetuum.ServerService2Installer`; keep machine settings, tokens, keys, and certificates out of the installer.

**Tech Stack:** .NET 8 / C# 12, ASP.NET Core Kestrel, Windows Service hosting, Windows OpenSSH, WiX Toolset 5, PowerShell for machine setup and manual verification.

**Approved design:** `docs/superpowers/specs/2026-10-11-sparky-vdm-reverse-ssh-design.md`.

## Scope boundaries

- No game-server or Discord integration, live DB access, player identity, conversation state, lore, VPN, relay service, or home-router port forward.
- Do not add or run automated tests unless the user asks. Use a Release build and the design's focused manual checks.
- Do not commit changes unless the user explicitly asks.
- Keep the AI PC's API listener on `127.0.0.1`; the VDM forward listener must be bound to VDM loopback only.

## File and subsystem map

- `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`: validated HTTPS listener, certificate, SSH target/identity, VDM loopback-forward, and service settings.
- `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`: HTTPS Kestrel binding and Windows Service lifetime integration, retaining current routes, bearer authentication, limits, and privacy-safe logging.
- `src/Perpetuum.ItemStatsExporter/SparkyTunnelSupervisor.cs` (new): launch/restart/stop the constrained `ssh.exe` process, with cancellation, bounded backoff, safe diagnostics, and no HTTP request replay.
- `src/Perpetuum.ItemStatsExporter/Program.cs`: preserve existing exporter, `--answer`, `--ask`, and `--serve` CLI behavior while passing the configured service arguments through correctly.
- `src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj`: add only the Windows Service host dependency needed by this executable.
- `src/Perpetuum.ItemStatsExporterInstaller/` (new): standalone WiX project and service package based on `src/Perpetuum.ServerService2Installer/`; package a self-contained x64 publish, not machine-specific secrets/configuration.
- `docs/superpowers/`: add operator setup, configuration, certificate/key handling, installation, validation, rotation, and rollback instructions alongside the approved spec.
- `PerpetuumServer2.sln`: include the new installer project if the repository's existing installer projects are solution members.

## Task 1: Define and validate transport configuration

**Files:** `SparkyServiceOptions.cs`, `Program.cs`.

1. Extend service settings for the HTTPS API certificate and reverse tunnel: VDM host, SSH port, tunnel account, private-key path, pinned host key, VDM loopback forward port, SSH executable, and Sparky's HTTPS loopback destination.
2. Preserve existing environment-variable configuration conventions and document the exact names/defaults. Keep secrets outside source and MSI. Do not put tokens, key material, or certificate private keys in arguments or logs.
3. Parse and validate configuration at startup. Require HTTPS for the Sparky API; require literal IPv4 loopback for its bind/destination and the VDM forward bind; reject wildcard/LAN/public listen addresses, invalid ports, malformed host-key pins, and missing key/certificate files.
4. Keep the API bearer token requirement and existing lower bounds, body/question/answer limits, concurrency and inference timeout behavior.
5. Fail before opening the listener if a required setting is missing or unsafe. Error text may identify the setting but must not include secret values.

**Checkpoint:** Run the exporter help path and local `--serve` startup with deliberately incomplete and invalid configuration; confirm clear safe startup errors and confirm existing non-service CLI modes remain available. Do not contact VDM yet.

## Task 2: Serve the existing API over authenticated HTTPS

**Files:** `SparkyHttpService.cs`, `SparkyServiceOptions.cs`, project file.

1. Configure Kestrel to listen only on `127.0.0.1` using HTTPS and the configured machine-specific certificate with an IP SAN for `127.0.0.1`.
2. Enable Windows Service lifetime integration while preserving console execution for local diagnostics. Have Windows SCM stop flow cancel the web host cleanly.
3. Retain `/health`, `/v1/answer`, bearer-token validation, request limits, response schema, inference cancellation, and safe logging. Do not add a public or LAN listener.
4. Add a clear startup diagnostic for certificate loading/bind failures without logging private certificate data or API credentials.

**Checkpoint:** Release build; run locally using the configured HTTPS certificate. Manually verify `/health`, a valid stats/recipe answer, and unauthorized response for a missing/bad bearer token. Verify plain HTTP does not serve the API and the listener is on loopback only.

## Task 3: Supervise one restricted reverse SSH tunnel

**Files:** new `SparkyTunnelSupervisor.cs`, `SparkyHttpService.cs`, `SparkyServiceOptions.cs`.

1. Implement a hosted background service that starts the configured Windows OpenSSH Client with argument-list APIs (not shell command construction), using pinned host-key checking and the dedicated private key.
2. Request exactly one remote forward from VDM `127.0.0.1:<configured-port>` to AI PC `127.0.0.1:5080`. Include keepalive, batch/noninteractive behavior, `ExitOnForwardFailure`, and forwarding-only/no-shell options consistent with the approved SSH policy.
3. Start the child process without exposing credentials in command-line arguments. Keep stdout/stderr handling bounded and redact or omit content; log only connection lifecycle/outcome and safe process exit information.
4. Treat a successfully running SSH process with `ExitOnForwardFailure` as the forward-established signal. If it exits or cannot connect, retry with bounded backoff; cancel/kill the child process tree when the Windows service stops. Do not replay or retain API requests.
5. Keep the API health endpoint about local API readiness; report tunnel state only through privacy-safe service logs or a local operator-only diagnostic if needed. Do not expose SSH configuration publicly.

**Checkpoint:** Release build. With a temporary local SSH test endpoint/configuration, manually confirm start, forward bind failure handling, process-exit reconnect, and graceful service cancellation before using VDM. Confirm the supervisor never asks ssh.exe to bind outside VDM loopback.

## Task 4: Add the self-contained Windows Service installer

**Files:** new `src/Perpetuum.ItemStatsExporterInstaller/` project and WiX sources, `PerpetuumServer2.sln` if applicable.

1. Mirror the existing WiX installer layout and service registration pattern without modifying the game-server installer.
2. Publish `Perpetuum.ItemStatsExporter` as self-contained `win-x64`; ensure the MSI includes the published runtime payload rather than framework-dependent build output.
3. Register Sparky as an auto-starting Windows service with `--serve "C:\ProgramData\Sparky\output.json"`; keep that export path outside the install directory. Ensure service stop/uninstall behavior is configured. Use a dedicated low-privilege service identity where compatible with key/certificate access; document required ACL grants. Do not grant interactive logon or broad unrelated permissions.
4. Install only program files. Do not package the export JSON, API token, LM Studio token, SSH private key, host-specific address/pin, certificate private key, or operator config. The operator places the current export and protected configuration in the documented external data directory before starting the service.
5. Ensure upgrades/uninstall do not recursively delete externally managed config, export, key, certificate, or user data. Keep install and data directories distinct.
6. Build the installer and inspect its payload to confirm it is self-contained and contains no machine secrets.

**Checkpoint:** Produce the MSI from a Release self-contained publish and inspect the package contents. On a disposable local Windows service installation, verify install/start/stop/uninstall and confirm external config/data remain untouched.

## Task 5: Document VDM and AI-PC provisioning

**Files:** new operator guide under `docs/superpowers/` and relevant `--help` text in `Program.cs`.

1. Document Windows OpenSSH Client setup on the AI PC and OpenSSH Server setup on VDM, including the dedicated tunnel account, key-only auth, `authorized_keys` restrictions, `AllowTcpForwarding remote`, `PermitListen` where supported, `GatewayPorts no`, no shell/PTY/agent/X11, and `sshd -t` validation before restarting sshd.
2. Explain version compatibility checks for Windows OpenSSH per-user/per-key restrictions. If the installed server cannot enforce one loopback port and forwarding-only access, stop deployment until it can; do not fall back to a broad forward.
3. Document key generation, host-key pinning, ACLs, optional source-IP firewall restriction, and VDM/provider firewall rules. State explicitly that no home-router rule is needed and that only VDM's SSH port is public.
4. Provide certificate creation steps for an IP SAN of `127.0.0.1`, trust of the public certificate on both computers, private-key protection on the AI PC, and safe certificate rotation. Do not recommend TLS validation bypasses.
5. Document secure configuration provisioning without secrets in MSI/source/command history/logs; cover the service identity's read permissions for the export, configuration, SSH private key, and certificate. The service must not start until its external export and required settings are present.
6. Include installation, start/reboot behavior, recovery/retry expectations, manual validation, and rollback steps.

**Checkpoint:** Review instructions against the approved spec and ensure a reader can distinguish the VDM public SSH listener from the loopback-only forwarded API.

## Task 6: End-to-end manual validation before game integration

**Files:** implementation output and operator guide only.

1. Run the requested Release build and `git diff --check`; do not run automated tests unless requested.
2. Install/configure Sparky and establish the SSH connection from AI PC to VDM. Confirm VDM listens on `127.0.0.1:<forward-port>` only, never `0.0.0.0` or a public/LAN interface.
3. From VDM, call HTTPS `/health` and a valid authenticated item-stat and recipe question. Confirm certificate validation succeeds normally and answers are non-empty.
4. Confirm bad/missing bearer token returns 401; wrong SSH identity is rejected; an untrusted/replaced certificate fails validation.
5. From an external network, confirm the forward port is unreachable and only the dedicated VDM SSH port is exposed.
6. Interrupt SSH/connectivity and confirm recovery; stop/restart services and reboot both machines in a controlled order to confirm unattended startup.
7. Inspect logs for absence of question text, answers, API/model tokens, private keys, and caller identifiers.
8. Confirm no game-server or Discord code was changed and no live DB connection was introduced.

## Risks and implementation notes

- Windows OpenSSH features and accepted `authorized_keys` restrictions vary by installed version. Validate the actual VDM version before enabling the tunnel; fail closed if restrictions are not supported.
- HTTPS validation depends on using the exact loopback IP in the certificate SAN and trusting only the intended public certificate on both machines.
- A reverse forward is not a VPN. The only allowed forwarded socket is the Sparky HTTPS API on VDM loopback.
- Service account access to OpenSSH key files and certificate private key must be verified under the actual Windows identity; interactive-user success is not sufficient.
- Keep the installer separate from `Perpetuum.ServerService2Installer` to avoid changing the live game-server installation lifecycle.
- If build-time WiX integration cannot reliably consume the self-contained publish output without embedding secrets or deleting external data, stop and revise the packaging step before implementation continues.
