# Sparky VDM Reverse SSH Transport Design

**Status:** Draft for user review
**Date:** 2026-10-11

## Purpose

Provide a direct, authenticated connection from the Perpetuum game server running on VDM to the Sparky item-answer service on the always-on AI PC. The AI PC remains behind its home router and accepts no inbound connection. The connection uses only the two existing machines and Windows/OpenSSH; it does not depend on a hosted relay, Cloudflare, another third-party service, or a VPN.

This stage proves transport and unattended service operation. It does not add game chat, Discord, live database access, player identifiers, or persistent conversation context.

## Agreed constraints

- Sparky and its offline item/recipe export remain on the AI PC.
- The AI PC is behind NAT; do not add a home-router port-forward or public listener.
- The game server is on VDM under Windows. One inbound TCP port can be opened on VDM for SSH.
- No third-party relay or VPN is acceptable.
- Keep the Sparky HTTP API bound to `127.0.0.1` on the AI PC.
- Use the existing API bearer token in addition to SSH authentication.
- Use a self-contained Windows x64 Sparky build so the AI PC does not require a .NET runtime installation.
- Install Sparky in an auto-starting Windows service using the existing WiX service-installer pattern. The Windows OpenSSH Client and Server are separate Windows components.
- Do not wire the game server or Discord to Sparky until the transport is validated.
- Do not commit without an explicit user request.

## Chosen architecture

The AI PC initiates a persistent outbound SSH connection to the OpenSSH Server on VDM. The SSH client requests one remote TCP forward bound only to VDM loopback. Requests from the game server on VDM will later call that loopback endpoint; OpenSSH carries the byte stream through the encrypted SSH connection to the Sparky API on the AI PC.

```text
VDM game server (future client)
  -> https://127.0.0.1:<VDM-forward-port>/v1/answer
  -> VDM OpenSSH Server
  => encrypted SSH connection initiated outbound by AI PC
  -> AI PC OpenSSH Client
  -> https://127.0.0.1:5080/v1/answer
  -> Sparky local service
  -> LM Studio
```

The SSH listener is the only new public-facing endpoint, on VDM. The forwarded Sparky port is explicitly bound to VDM loopback and must not be reachable from the internet or LAN. No inbound traffic reaches the AI PC. The SSH forward carries only one TCP port; it does not route either machine's network or create a VPN.

The SSH connection encrypts the network path. HTTPS from the VDM caller to Sparky adds end-to-end server authentication across the forwarded port, so a substituted SSH forward cannot impersonate Sparky and capture the API bearer token. Sparky still requires `SPARKY_API_TOKEN`. The VDM client must validate the server certificate normally; certificate validation bypasses are prohibited. Sparky's existing request size, question length, answer length, inference timeout, concurrency, and privacy-safe logging limits continue to apply. Do not put tokens in URLs or log headers, request bodies, answers, private keys, or bearer tokens.

## SSH identity and restrictions

- Install or enable Windows OpenSSH Server on VDM and OpenSSH Client on the AI PC.
- Create a dedicated VDM account for the tunnel, separate from game-server and administrator accounts.
- Configure public-key authentication for the tunnel account and disable password authentication for that account.
- Restrict the tunnel account to remote TCP forwarding on the single approved VDM loopback address and port. Disable interactive shell, PTY, agent forwarding, X11 forwarding, and unrelated forwarding for that account.
- Keep `GatewayPorts` disabled so a remote forward cannot bind to a public address.
- Validate that the installed Windows OpenSSH version supports the required per-user/per-key restrictions. Validate the final `sshd_config` before restarting `sshd`; fail closed if it cannot enforce the loopback-only, single-port policy.
- Generate a dedicated key pair for Sparky. Store the private key on the AI PC with NTFS permissions limited to the Sparky service identity and administrators. Place only the public key in the VDM tunnel account's authorized keys.
- Pin the VDM SSH host key on the AI PC. Do not disable host-key checking.
- If the AI PC has a stable public source address, restrict VDM's SSH firewall rule to that address. If it does not, rely on the dedicated key-only account and the SSH restrictions; do not weaken authentication to compensate.

The required SSH server port must be allowed by Windows Firewall and any VDM/provider firewall. No firewall change is made on the AI PC's home router. Use a dedicated port if VDM's existing SSH policy requires one; the chosen port is configuration, not a source-code constant.

## HTTPS identity for the Sparky API

- Configure the loopback Sparky API to serve HTTPS only for this transport profile, using `https://127.0.0.1:5080` on the AI PC.
- Generate a machine-specific self-signed server certificate with an IP subject alternative name for `127.0.0.1`. Do not use a certificate from a third-party provider.
- Install the certificate's public portion in the Local Machine Trusted Root Certification Authorities store on both the AI PC and VDM. The certificate must be valid for the loopback IP used by the VDM caller.
- Store the certificate with its private key on the AI PC, outside the installer package, and ACL it to the Sparky service identity and administrators. Do not copy the private key to VDM.
- The VDM HTTP client connects to `https://127.0.0.1:<VDM-forward-port>` and relies on normal Windows certificate validation. Do not use `-SkipCertificateCheck`, permissive validation callbacks, or equivalent bypasses.
- Certificate replacement requires distributing and trusting the new public certificate on both machines before switching Sparky to it; remove the old trusted certificate after successful rotation.

## Service lifecycle and configuration

The AI PC runs Sparky's `--serve` mode as a Windows service. The service hosts the existing loopback API over HTTPS and supervises the Windows OpenSSH Client process that maintains the reverse forward. The service starts the tunnel after validating local configuration and certificate state, restarts it with bounded backoff after a disconnect, and shuts it down when the service stops. SSH keepalives and `ExitOnForwardFailure` detect dead links and bind failures. The tunnel is considered ready only after the requested VDM loopback forward has been established.

The Sparky service installer follows the existing WiX installer structure and packages a self-contained `win-x64` publish. It must not embed machine-specific settings, API tokens, or SSH private keys. Keep runtime configuration and the private key outside the MSI and apply restrictive NTFS ACLs. Do not place secrets on a command line, in source control, or in logs. The VDM game-server service's future copy of the Sparky API bearer token must be readable only by its service identity and administrators.

Tunnel settings are configurable: VDM hostname/address, SSH port, tunnel account, private-key path, host-key pin, VDM loopback port, Sparky HTTPS loopback URL, and certificate path. Validate all values at startup; reject non-loopback forward listen addresses, non-HTTPS Sparky API URLs, and invalid ports. Do not accept a tunnel destination or listen address from an HTTP request.

## Request flow and failure behavior

1. A local VDM process connects to `127.0.0.1:<VDM-forward-port>` and sends the existing authenticated Sparky HTTP request.
2. VDM OpenSSH forwards the TCP connection through the already-established SSH session.
3. The AI PC OpenSSH Client connects the forwarded stream to Sparky at `127.0.0.1:5080`.
4. Sparky validates its bearer token and request limits, performs lookup from the offline export, and calls the configured local model as it does today.
5. Sparky's JSON response returns through the SSH forward.

If VDM's SSH port is unreachable, SSH authentication fails, the forward cannot bind, the tunnel drops, Sparky is stopped, or the model is unavailable, the request fails within configured timeouts. The tunnel supervisor retries connection establishment; it does not replay HTTP requests. The future game-server integration must treat transport/model failure as a normal unavailable response, use asynchronous work outside hot update loops, and avoid holding game-server threads while waiting for inference. That future behavior is out of scope for this transport stage.

## Validation before game integration

Validation is manual and does not use live game traffic:

1. Confirm the AI PC can establish SSH to the VDM public host and dedicated port.
2. Confirm VDM shows a listener on `127.0.0.1:<VDM-forward-port>` only; verify the port is not bound to `0.0.0.0` or a public interface.
3. From VDM, call the forwarded HTTPS `/health` endpoint and confirm `status: ok`; verify the certificate is accepted without a validation bypass.
4. From VDM, submit a valid bearer-authenticated stats or recipe request and confirm `status: answered`, `item`, and non-empty `answer`.
5. Submit a bad/missing bearer token and confirm HTTP 401; confirm an invalid SSH key cannot establish the tunnel and an untrusted API certificate is rejected.
6. From a separate external network, confirm the VDM-forward port is unreachable. Confirm only the dedicated SSH port is externally reachable.
7. Stop the SSH client or interrupt connectivity; confirm the tunnel supervisor reconnects and the forwarded port disappears/reappears without exposing Sparky publicly.
8. Reboot the AI PC and VDM in a controlled order; confirm both services start and the tunnel becomes ready without an interactive login.
9. Inspect logs and confirm they contain no question, answer, API token, or private key.

Do not run automated tests unless requested. Use a Release build, focused manual checks, and `git diff --check` when implementation begins.

## Rollback

Stop and uninstall the Sparky service/tunnel supervisor on the AI PC, remove the VDM authorized key and dedicated tunnel account, disable the VDM SSH firewall rule, and disable/remove the `sshd` listener if it was enabled only for Sparky. No home-router or public DNS changes need reverting.

## Out of scope

- Any game-server code, gameplay command, chat channel, or in-game mail change.
- Discord bot or Discord API credentials.
- Live database access or copying account/character data.
- Player identity, origin metadata, context persistence, or follow-up memory.
- Lore or knowledge outside the verified item/recipe export.
- VPNs, Cloudflare, managed tunnels, public Sparky HTTP endpoints, home-router port forwarding, or a separate relay host.
- Automated export updates or deployment of the AI model.

## References

- [Microsoft: Get started with OpenSSH Server for Windows](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse)
- [Microsoft: OpenSSH Server configuration for Windows](https://learn.microsoft.com/en-us/windows-server/administration/OpenSSH/openssh-server-configuration)
- [Microsoft: Troubleshoot OpenSSH communication through Windows Firewall](https://learn.microsoft.com/en-us/troubleshoot/windows-server/system-management-components/troubleshoot-openssh-windows-firewall-port22)
- Existing local service contract: `docs/superpowers/specs/2026-10-11-sparky-local-service-design.md`
- Existing installer pattern: `src/Perpetuum.ServerService2Installer/`
