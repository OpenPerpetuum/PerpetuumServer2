# Sparky VDM Reverse SSH Operations

This guide provisions the approved Sparky transport between the AI PC and VDM. It does not connect the game server or Discord to Sparky. Perform the VDM steps in an elevated PowerShell session and the AI-PC steps as the account that will provision Sparky.

## Network layout

- VDM exposes only its SSH listener to the internet. Allow that TCP port in Windows Firewall and the VDM/provider firewall. Restrict the source to the AI PC's stable public IP when possible.
- Do not create a router port forward on the AI PC's network.
- Sparky listens at `https://127.0.0.1:5080` on the AI PC.
- The AI PC's SSH client requests a single VDM listener at `127.0.0.1:<forward-port>`. The forward must not bind to `0.0.0.0`, a public address, or a LAN address.

## Install OpenSSH

On the AI PC, make sure the Windows OpenSSH Client capability is installed. On VDM, install the Windows OpenSSH Server capability and verify the `sshd` service and existing firewall rules. Microsoft documents the Windows capability installation and service setup in [Get started with OpenSSH Server for Windows](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse).

The server config is a file named `%ProgramData%\ssh\sshd_config` (usually `C:\ProgramData\ssh\sshd_config`), not a folder. If it is absent, Windows OpenSSH creates a default file when `sshd` first starts. Before that first start, disable the automatically created `OpenSSH-Server-In-TCP` firewall rule so the default listener is not reachable from the LAN while it is unconfigured. On the main Windows 11 test machine, an elevated PowerShell session can do:

```powershell
Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue |
    Disable-NetFirewallRule
Set-Service sshd -StartupType Manual
Start-Service sshd
Stop-Service sshd
Test-Path "$env:ProgramData\ssh\sshd_config"
```

Once the file exists, edit and validate it before enabling the firewall rule. Later, restrict the inbound SSH rule to the AI machine's LAN IP. Do not create a router port-forward for this LAN test.

Before configuring forwarding, record the OpenSSH version on both machines and confirm that VDM's version supports the per-user and per-key restrictions below. Windows OpenSSH configuration support varies by Windows release; check [OpenSSH Server configuration for Windows](https://learn.microsoft.com/en-us/windows-server/administration/OpenSSH/openssh-server-configuration). If the installed version cannot enforce a remote-only single-port forward and deny interactive sessions, stop here and update OpenSSH before enabling the tunnel.

## Create and restrict the SSH identity

On the AI PC, create a dedicated key pair for Sparky. Keep the private key in the external Sparky data directory, not in the install directory or MSI. The unattended service cannot prompt for a key passphrase, so protect the key with NTFS permissions for `NT AUTHORITY\LOCAL SERVICE` and local administrators. Do not grant access to ordinary users. Use an empty passphrase only with these restrictive file permissions and a dedicated tunnel account on VDM.

```powershell
New-Item -ItemType Directory -Force -Path 'C:\ProgramData\Sparky\ssh' | Out-Null
ssh-keygen -t ed25519 -f 'C:\ProgramData\Sparky\ssh\id_ed25519' -C 'sparky-vdm-tunnel'
```

When prompted for a passphrase, press Enter twice to leave it empty. The unattended Windows service cannot answer a passphrase prompt; the key is instead protected by its restrictive NTFS permissions.

On VDM, create a standard, non-administrator account used only for this tunnel. Disable password authentication for the tunnel account and deny interactive local/RDP logon where possible. For a non-administrator account, keep `authorized_keys` owned by that user and accessible only to that user, Administrators, and SYSTEM; do not grant access to other users or groups. Place only the AI PC's public key in `authorized_keys`, with restrictions like this (replace the key data and port):

```text
restrict,port-forwarding,permitlisten="127.0.0.1:5080" ssh-ed25519 AAAA... sparky-vdm-tunnel
```

Restrict the VDM key and matching `Match User` block so they permit only remote TCP forwarding to `127.0.0.1:<forward-port>`, disallow local forwarding and agent forwarding, and reject shell/command/subsystem sessions. A modern OpenSSH setup can use the `restrict`, `port-forwarding`, and `permitlisten="127.0.0.1:<forward-port>"` authorized-key options together with a per-user `AuthenticationMethods publickey`, `PasswordAuthentication no`, `AllowTcpForwarding remote`, `PermitListen 127.0.0.1:<forward-port>`, `GatewayPorts no`, `AllowAgentForwarding no`, and `MaxSessions 0` policy. For example:

```text
Match User sparky-tunnel
    AuthenticationMethods publickey
    PasswordAuthentication no
    AllowTcpForwarding remote
    PermitListen 127.0.0.1:5080
    GatewayPorts no
    AllowAgentForwarding no
    PermitTTY no
    MaxSessions 0
```

`MaxSessions 0` prevents shell, command, and subsystem sessions while still permitting forwarding. `restrict` disables key forwarding features by default, and the explicit `port-forwarding` option re-enables the TCP forwarding needed here; `AllowTcpForwarding remote` and `PermitListen` keep it to the requested direction and listener. Check each directive against VDM's installed version before applying it. Windows OpenSSH does not support every upstream option (including `KbdInteractiveAuthentication` and `X11Forwarding`), so do not add those directives to its config. Validate the effective policy with both a successful forward and failed shell/unauthorized-forward attempts; do not assume a config line was accepted just because it is present.

Back up `%ProgramData%\ssh\sshd_config` before editing. Validate the effective configuration using the installed `sshd.exe` configuration-test option before restarting the service. Restart `sshd` only after validation succeeds. If validation fails, restore the backup and leave the Sparky SSH firewall rule disabled.

### Temporary two-machine Windows 11 LAN test

For the first integration test, run the Sparky API and SSH client on the AI machine; run `sshd` and the VDI/game-server-side caller on the main Windows 11 development machine. The AI machine connects to the main machine over the LAN and requests a reverse forward bound only to the main machine's loopback:

- On the main machine, leave `sshd` on its LAN interface. If you configure an explicit global `ListenAddress`, use the main machine's LAN IP, not `127.0.0.1`; remove conflicting active `ListenAddress` entries.
- Restrict the Windows Firewall inbound rule `OpenSSH-Server-In-TCP` to the AI machine's LAN IP (for example, elevated PowerShell: `Set-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -RemoteAddress '<AI-machine-LAN-IP>'`). Do not add a router port-forward.
- Set Sparky's AI-machine settings to `SPARKY_SSH_HOST=<main-machine-LAN-IP>`, `SPARKY_SSH_PORT=22`, and `SPARKY_VDM_FORWARD_PORT=5080`. The API remains at `https://127.0.0.1:5080` on the AI machine.
- Use `127.0.0.1:5080` in both the key's `permitlisten` option and `PermitListen` in the SSH server's `Match User` block. Port 5080 can be used on both machines: SSHD binds the forwarded port on the main machine, while Kestrel binds the API port on the separate AI machine.
- Pin the main machine's SSH host key on the AI machine. Trust the API's public TLS certificate on the main machine so VDI-side calls to `https://127.0.0.1:5080` validate normally.

This keeps the test traffic on the local network and the VDI-side listener loopback-only. After the flow works, the VDM deployment uses the same tunnel design with VDM's LAN/public SSH reachability and firewall policy.

## Findings from the Windows 11 LAN proof-of-concept

Recorded 11 October 2026 so the VDI setup can reuse the working sequence. These notes describe the tested two-PC setup, not the final VDI host.

### Test machines and confirmed behavior

| Role | Host | LAN address | Relevant software |
|---|---|---|---|
| Main/test VDI side | `DESKTOP-8LUE5OF` | `192.168.31.79` (DHCP) | Windows OpenSSH Server; tunnel account `Sparky` |
| AI side | `DESKTOP-VP6K17O` | `192.168.31.18` (DHCP) | Sparky service, LM Studio, Windows OpenSSH Client; service account `NT AUTHORITY\LOCAL SERVICE` |

Both addresses were DHCP leases. Reserve them or re-check them before repeating firewall and endpoint commands. The VDI deployment will use its actual reachable address and firewall rules; do not carry over these LAN addresses.

The following were confirmed during setup:

- `Test-NetConnection 192.168.31.79 -Port 22` succeeded from the AI PC.
- A manual SSH connection from the AI PC authenticated as `Sparky` with the dedicated Ed25519 key, passed strict host-key checking, and reported `remote forward success for: listen 127.0.0.1:5080, connect 127.0.0.1:5080`.
- Sparky ran as a Windows service under Local Service. Its local HTTPS endpoint listened on `https://127.0.0.1:5080`; a local health request returned `200` and `{"status":"ok"}`.
- From Main, the service-managed reverse tunnel created a listener on `127.0.0.1:5080`; HTTPS `/health` returned `status: ok` through that listener.
- From Main, an authenticated `/v1/answer` request returned `status: answered` for `Standard medium HCL laser`. A recipe question also passed through the API, and after restarting the Sparky service on the AI PC, the tunnel and answer request worked again.
- The AI machine's model endpoint was `http://127.0.0.1:1235/v1`, with model ID `p-e-w_qwen3-4b-instruct-2507-heretic`. LM Studio's default port `1234` was wrong for this setup.
- The service/tunnel path initially failed even though the interactive SSH test worked. The service uses files under `C:\ProgramData\Sparky`, while the successful manual test used `C:\Users\devours\.ssh`. Matching the test to the service's identity and exact configured files exposed the difference.
- The service could not read its ProgramData `known_hosts` file. The file ACL was corrected to grant read access to Local Service, Administrators, and SYSTEM. This resolved the remaining service-managed tunnel issue; end-to-end stats and recipe requests and post-restart reconnection then passed.

### Windows and PowerShell gotchas

- On this Windows 11 install, `Get-NetIPAddress` did not accept `-InterfaceOperationalStatus`. Find active adapters first, then query their interface indexes, or use `Get-NetIPConfiguration`.
- `%ProgramData%\ssh\sshd_config` is a **file**, not a folder. It was absent before the first `sshd` start; starting and stopping `sshd` created it for editing.
- Use the exact Windows account and file paths from the service configuration when checking ACLs. Localized group names can fail to resolve in `icacls`; well-known SIDs are reliable, e.g. `*S-1-5-19` for Local Service, `*S-1-5-32-544` for Administrators, and `*S-1-5-18` for SYSTEM.
- `RandomNumberGenerator.Fill` was unavailable in the Windows PowerShell/.NET combination used to generate a bearer token. For compatibility, use `RandomNumberGenerator.Create()` and its `GetBytes` method, or run the helper in a compatible PowerShell/.NET environment. Never paste the generated token into chat or save it in shell history.
- The settings helper once reported that it could not load `System.Security.Cryptography.ProtectedData`; it later created `C:\ProgramData\Sparky\service-settings.json`. The exact shell/runtime condition behind that first error was not isolated. If it recurs, capture `$PSVersionTable` and run the helper from the supported PowerShell host before changing the app configuration.
- An elevated PowerShell prompt is not the same identity as the Windows service. A private key intentionally readable only by Local Service can produce `Access denied` when an administrator runs `ssh-keygen` against that private-key file. Do not broaden its ACL just to make an interactive inspection succeed. Check the public `.pub` file for its fingerprint and validate that the service identity can read the private key.

### Failures and their resolutions

| Symptom | Finding / resolution |
|---|---|
| Sparky attempted `localhost:1234` | The configured LM Studio endpoint for this installation is port `1235`. Use the real endpoint in the service settings, not the OpenAI-compatible default shown in generic examples. |
| LM Studio returned an empty assistant answer, sometimes with `finish_reason=length` and all completion tokens spent on reasoning | One model produced reasoning tokens but no user-facing `content`; a different instruct model returned the answer. The API correctly treats empty assistant content as an error. Select a model that emits ordinary assistant text and leave enough completion budget. |
| AI machine said .NET 8 was missing even after the ASP.NET Core runtime was installed; `dotnet` was not on `PATH` | The deployed app was framework-dependent and the machine lacked the required .NET runtime/host. Publishing and installing the self-contained x64 build removed the runtime-install prerequisite. |
| Service reported a generic startup timeout or `Start-Service` failure | The service controller message hid the useful inner exception. Inspect the Sparky service logs and System event log, and verify read access to each configured file: settings, export JSON, PFX, SSH private key, and `known_hosts`. |
| Service could not load the PFX / Schannel closed the TLS handshake; Event 36869 mentioned no private-key property | The service needs a certificate private key available to Windows TLS. The certificate was reloaded with `X509KeyStorageFlags.MachineKeySet` instead of `EphemeralKeySet`; the PFX ACL also had to allow the service identity to read it. After republishing, Kestrel logged its HTTPS listener and `/health` succeeded locally. |
| Service started but could not read `C:\ProgramData\Sparky\output.json` | The export file ACL did not grant Local Service read access. Grant the service identity read access to the exported JSON and verify its parent directory allows traversal. |
| Service repeatedly logged that the SSH child exited with code `255`; Main logged `Connection reset ... [preauth]` | `255` is only the SSH failure code; it does not explain the cause. The interactive SSH success used the profile key and host pins, while the service used ProgramData files. The pre-auth reset showed that the service connection was closing before the SSH server logged successful authentication, but did not identify the client-side cause. The tunnel supervisor currently logs the exit code and retry delay, not SSH's stderr, so use a separate verbose SSH diagnostic if the cause remains unclear. Check access as the service identity, strict host-key pin path, and the SSH server's OpenSSH/Operational log. In this incident the ProgramData `known_hosts` ACL was a confirmed issue. |
| `ssh-keygen` or `Copy-Item` against files under `C:\ProgramData\Sparky\ssh` returned access denied | The ACL was intentionally restricted for the service. Stage files in a user directory only when necessary, then install them with an elevated prompt and apply the service ACL. Do not use a broad recursive grant that makes the private key readable by ordinary accounts. |
| The MSI could not be built in the current development environment | The environment did not have the Windows Installer service available. A self-contained release ZIP was used to update the AI PC. This was a packaging-environment limitation; the installed service and API then ran. |

### Safe ACL reminders

- The service must be able to read every configured input file and traverse its parent directories. Verify each file separately; a readable directory does not imply readable files.
- Keep the private SSH key restricted to the service identity (and only the necessary administrative recovery principals). Do not grant access to the interactive `devours` account merely to make manual SSH tests use the service key.
- `known_hosts` contains public host-key pins, not a private credential. It can be readable by Local Service and administrators; a restrictive ACL that accidentally denies Local Service prevents SSH from verifying the server and causes the tunnel to exit.
- When updating a host pin, the current elevated administrator also needs write access. In this VDI setup, `known_hosts` had `SYSTEM:F`, `Administrators:R`, and `Local Service:R`; that was sufficient for service reads but caused `Add-Content` to fail. A suitable managed-file ACL is `Administrators:F`, `SYSTEM:F`, and `Local Service:R`, with inheritance disabled and no ordinary-user access. Apply it only to the public `known_hosts` file, never to the private SSH key.
- If an administrator cannot read or repair the `known_hosts` file, the recovery used here was:

  ```powershell
  $knownHosts = 'C:\ProgramData\Sparky\ssh\known_hosts'
  takeown.exe /f $knownHosts
  icacls.exe $knownHosts /inheritance:r /grant:r `
      '*S-1-5-19:R' `
      '*S-1-5-32-544:R' `
      '*S-1-5-18:F'
  icacls.exe $knownHosts
  Restart-Service -Name 'Perpetuum.Sparky'
  ```

  This is for the public host-key pin file only. Do not apply this grant list to the private key.
- DPAPI-protected service settings are machine-bound. Recreate them on the VDI/AI machine where the service will run; do not copy the settings JSON between machines.
- A certificate PFX contains a private key. Keep it outside the application install folder and grant only the service identity and administrators the access needed to operate and rotate it.

### VDI readiness checks

Before calling the LAN test complete, verify all of these from the actual machine/context shown:

1. On the AI PC, verify the service is running, local `https://127.0.0.1:5080/health` succeeds with normal TLS validation, and the selected LM Studio model returns non-empty assistant content.
2. On the main/VDI side, verify SSHD accepted the service's key and the only reverse-forward listener is `127.0.0.1:5080`; it must not listen on a LAN or public address.
3. From the main/VDI side, verify `https://127.0.0.1:5080/health` returns `{"status":"ok"}` through the tunnel, then make one authenticated answer request.
4. Restart Sparky and confirm the tunnel returns. Then reboot the AI PC and verify unattended startup, file access, model availability, and reconnection.
5. For real VDI deployment, confirm the VDI-side SSH port is reachable from the AI PC, open only that inbound SSH port, and keep the AI PC behind NAT without an inbound router port-forward. Prefer a stable VDI endpoint and restrict SSH source addresses where the hosting firewall permits it.
6. Preserve the currently working ACLs and configuration path. If the tunnel fails again, compare the service's configured ProgramData key and `known_hosts` paths with the paths used by any successful manual SSH command before changing firewall rules or regenerating keys.

### VDI preflight status

- VDI host `NS556032` reports Windows Server 2016 Standard, version `10.0.14393` (build `14393`). Its `%ProgramData%\ssh\sshd_config` file was absent at inventory time.
- The user downloaded the official 64-bit Win32-OpenSSH `10.0.0.0p2-Preview` ZIP and confirmed its SHA-256 matches the release page. The binaries are installed under `C:\Program Files\OpenSSH`; `install-sshd.ps1` registered `sshd` and `ssh-agent`. Both `ssh.exe -V` and `sshd.exe -V` report `OpenSSH_for_Windows_10.0p2` with LibreSSL 4.2.0. The `sshd` service is `Stopped` with startup type `Manual`. The inherited install-directory ACL grants Users read/execute and grants SYSTEM, Administrators, and TrustedInstaller full control.
- `%ProgramData%\ssh\sshd_config` is still absent, and no TCP port 22 listener was reported. The existing inbound TCP 22 rule displayed as `SSH` (internal GUID `{07B03B04-15AC-4B91-AC58-F2F3FBFA3D8E}`) is disabled and allows any program/source. Leave it disabled; after configuring and validating `sshd`, create a separate source-restricted firewall rule. Looking it up with `-Name SSH` returned no row because `SSH` is its display name, not its internal name.
- Created enabled local standard user `sparky-tunnel`, a member of `Users` and not `Administrators`. Its password was set through a secure prompt; `net user sparky-tunnel /passwordreq:yes` was required to set the separate Windows password-required flag. `net user` and `Get-LocalUser` then confirmed a password is set and required. No password value is recorded.
- The AI PC's service private key at `C:\ProgramData\Sparky\ssh\sparky_vdm_tunnel_ed25519` has no extension. Its matching public key is `C:\Users\devours\.ssh\sparky_vdm_tunnel_ed25519.pub`, fingerprint `SHA256:q7QUwCYqRacls+lxkB+iYw66UBaSBy8jB3f8qgA/tvU`. `C:\ProgramData\Sparky\ssh\id_ed25519.pub` is a different key and must not be used for this service identity.
- The VDI authorized key line was verified to contain the restrictions `restrict,port-forwarding,permitlisten="127.0.0.1:5080"` and the matching AI-PC service public key. The account home is `C:\ProgramData\ssh\users\sparky-tunnel`; its `.ssh\authorized_keys` ACL grants full control only to `NS556032\sparky-tunnel`, `BUILTIN\Administrators`, and `NT AUTHORITY\SYSTEM`. The home and `.ssh` directory ACLs have the same three principals. The `sshd` service remains stopped and the inbound TCP 22 firewall rule remains disabled.
- VDI RSA, ECDSA, and Ed25519 host keys were generated with `ssh-keygen -A`. `FixHostFilePermissions.ps1 -Confirm:$false` reported `sshd_config` absent (expected before configuration), removed `NS556032\line` access from private host keys, and granted it read access to public host keys. The Ed25519 host-key fingerprint is `SHA256:gT/ShA+FtruZl8/cghktMFDTPFMjRpNwrv2hxSJ2qmo`; the private Ed25519 key ACL now lists only `NT AUTHORITY\SYSTEM` and `BUILTIN\Administrators` with Full Control.
- `%ProgramData%\ssh\sshd_config` was created with a single allowed account (`sparky-tunnel`), public-key-only authentication, the explicit authorized-key path, remote TCP forwarding only, listener restriction `127.0.0.1:5080`, no agent forwarding or TTY, and `MaxSessions 0`. `sshd.exe -t -f ...` succeeded. `sshd.exe -T -f ...` reported the intended effective values, including `PasswordAuthentication no`, `AllowTcpForwarding remote`, `GatewayPorts no`, `PermitListen 127.0.0.1:5080`, and `MaxSessions 0`. The service remains stopped pending confirmation of the enabled firewall rules.
- A read-only query found no matching enabled inbound allow rule for TCP port 22, a rule name containing `ssh`/`sparky`, or a program path containing `sshd.exe`. Confirm the active Windows Firewall profile and its default inbound action before starting the service.
- The VDI's active network category is `Public` on interface `Ethernet 2`. The effective Public profile reports `Enabled=True` and `DefaultInboundAction=Block`; `netsh advfirewall show publicprofile` reports `BlockInbound,AllowOutbound`. No matching enabled inbound allow rule was found, so starting `sshd` for a local listener check remains protected by the firewall. `LocalFirewallRules` reports `N/A (GPO-store only)`; inspect `AllowLocalFirewallRules` in ActiveStore before adding an ingress exception, as local rules may be ignored when merging is disabled.
- `sshd` has now been started manually and reports `Running`. The returned output did not include a clear TCP/22 listener count or the full ActiveStore profile properties; retrieve those explicitly before proceeding to firewall changes.
- Follow-up inspection found one TCP/22 listener on `0.0.0.0`, owned by PID `7416`, confirmed as `C:\Program Files\OpenSSH\sshd.exe`. The effective Public firewall profile is enabled, has `DefaultInboundAction=Block`, and has `AllowLocalFirewallRules=True`; the earlier query found no matching enabled inbound allow rule. SSH is therefore listening on IPv4 but remains blocked by the host firewall until a source-restricted rule is added.
- The AI PC network's public egress IPv4 is static and has been supplied for the firewall restriction. The exact public IP is kept out of this repository note.
- Created and verified enabled local firewall rule `Sparky-SSH-In`: inbound TCP port 22, Public profile, program `C:\Program Files\OpenSSH\sshd.exe`, remote address restricted to the AI network's supplied static egress IPv4, edge traversal blocked. The previously existing broad `SSH` rule remains disabled.
- From the AI PC, `Test-NetConnection` to the VDI's public endpoint on TCP port 22 succeeded (`TcpTestSucceeded=True`), confirming the current host/provider network path is open from the allowed egress address.
- The first AI-PC `ssh-keyscan -p 22 -t ed25519` attempt returned no key despite the successful TCP probe. Verbose output showed `choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com`; VDI OpenSSH logged pre-auth connection closures from the AI egress address. This matches [Win32-OpenSSH issue #2140](https://github.com/PowerShell/Win32-OpenSSH/issues/2140): `ssh-keyscan.exe` can propose this KEX even when that binary cannot use it, while the regular `ssh.exe` client is unaffected. The regular client's displayed ED25519 fingerprint matched the value read locally on VDI (`SHA256:gT/ShA+FtruZl8/cghktMFDTPFMjRpNwrv2hxSJ2qmo`). The user accepted this matching key in the diagnostic prompt; client authentication was disabled, so the subsequent `Permission denied (publickey)` was expected. The verified ED25519 host-key line was then written to a separate temporary known-hosts file and added to the AI service's `C:\ProgramData\Sparky\ssh\known_hosts`. `ssh-keygen -lf` confirmed the same fingerprint from the service file. Its ACL is now `SYSTEM:F`, `Administrators:F`, and `Local Service:R`; the earlier `Administrators:R` ACL had prevented pin updates while still allowing service reads. After restarting Sparky, VDI still has no `127.0.0.1:5080` listener and OpenSSH logs `Connection reset by authenticating user sparky-tunnel ... [preauth]`; temporarily set `SyslogFacility LOCAL0` and `LogLevel DEBUG3`, capture `%ProgramData%\ssh\logs\sshd.log` for one attempt, then restore the original SSH config. Win32-OpenSSH documents this file-logging procedure in [Logging Facilities](https://github.com/PowerShell/Win32-OpenSSH/wiki/Logging-Facilities).
- From the AI PC, ICMP ping to the VDI public endpoint succeeded, while TCP port 22 failed. This is the expected closed-port baseline; keep SSH ingress closed until the server implementation is installed and its restrictions are validated.
- The user confirms VDI must remain on Server 2016. Server 2016 requires a separately maintained package; the current Win32-OpenSSH standalone install guidance labels that path a test release, so verify the signed release and hash, keep ingress source-restricted, and plan manual updates before enabling it.
- The exact public endpoint is kept out of this repository note. Windows Server 2016 extended support ends on 12 January 2027.
- A later process inspection found that the running AI-side Sparky service still targeted the earlier LAN test machine and account: its child `ssh.exe` was connected to the main PC's private address as `sparky`. VDI therefore saw no connection from that service during the SSHD debug capture. The VDI tunnel account is `sparky-tunnel`; update `SPARKY_SSH_HOST` to VDI's reachable endpoint and `SPARKY_SSH_USER` to `sparky-tunnel` in the AI-side settings, then restart Sparky and verify the connection and loopback forward on VDI. The settings generator rewrites the full settings file, so a targeted edit of these two non-secret JSON values avoids re-entering DPAPI-protected credentials.
- After changing the AI service settings to VDI's reachable endpoint and `sparky-tunnel`, VDI reported a listener on `127.0.0.1:5080` (PID `7632`). This confirms the reverse-forward listener is established. The displayed `sshd.log` was last written before the settings change and must not be used to diagnose this current connection; run the health request through the tunnel next.
- VDI's first `Invoke-RestMethod https://127.0.0.1:5080/health` reached TLS validation but failed with `Could not establish trust relationship for the SSL/TLS secure channel`. On the AI PC, the public `C:\ProgramData\Sparky\sparky-api.cer` file had no read access for the elevated administrator; granting the Administrators group read access to this `.cer` allowed inspection. It is `CN=Sparky local API`, thumbprint `4DD35A29B0B5C6BB901272F526B8AFCEB07F253F`, has no private key, and its SAN is `IP Address=127.0.0.1`. The public certificate was transferred to VDI and imported into `Cert:\LocalMachine\Root`; `Import-Certificate` returned the matching subject and thumbprint. The forwarded HTTPS health request then returned `status : ok`, confirming certificate validation and the full VDI-to-AI tunnel path. Never transfer the PFX/private key or bypass certificate validation.
- From VDI, an authenticated `POST https://127.0.0.1:5080/v1/answer` returned `status: answered` for Standard medium HCL laser, including translated stats, direct recipe components, and recursively expanded full materials. This confirms the full VDI-to-AI-to-LM Studio answer path over the reverse SSH tunnel and validated HTTPS.
- From VDI, a `POST https://127.0.0.1:5080/v1/answer` with no Authorization header returned HTTP `401`, confirming the API rejects unauthenticated answer requests before inference.
- From VDI, a `POST https://127.0.0.1:5080/v1/answer` with a deliberately incorrect bearer token also returned HTTP `401`.
- From the AI PC, `Test-NetConnection` to VDI's public address on TCP port `5080` failed (`TcpTestSucceeded: False`) while ping succeeded. Together with VDI's listener bound to `127.0.0.1:5080`, this confirms the API forward is not exposed on VDI's network interface.
- After restarting `Perpetuum.Sparky` on the AI PC, the established SSH connection returned. On VDI, the reverse-forward listener appeared on `127.0.0.1:5080` (PID `10648`), and `https://127.0.0.1:5080/health` returned `status: ok`. Both `sshd` on VDI and `Perpetuum.Sparky` on the AI PC are `Running` with `StartMode: Auto`. This confirms service-restart recovery and that both services are configured for automatic startup. VDI reboot validation is deferred because VDI is a live game server; do not reboot or restart its SSH service during live operation. Verify boot recovery during a planned maintenance window.

## Pin VDM's SSH host key

Obtain the VDM host-key fingerprint from its local console or another trusted channel. Compare that fingerprint with the AI PC's observation before trusting it. Save only the verified host key in the configured `SPARKY_SSH_KNOWN_HOSTS_PATH` file. For a non-default port, the known-hosts entry normally uses `[host]:port` form. Sparky runs SSH with strict host-key checking and ignores the global known-hosts file; a missing or changed pin prevents the tunnel from starting.

Never disable host-key checking. Do not trust unverified `ssh-keyscan` output: it can collect the key, but the fingerprint must be matched out of band first.

For example, read VDM's SSH host-key fingerprint locally with `ssh-keygen -lf` on its public host-key file. On the AI PC, `ssh-keyscan -p <ssh-port> -t ed25519 <vdm-host>` can collect a candidate known-hosts entry; compare its fingerprint to the VDM console result before saving it in the configured pin file.

If Win32 `ssh-keyscan.exe` exits with `choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com`, use the regular SSH client to display the host fingerprint without saving a key or authenticating. At the prompt, compare the displayed ED25519 fingerprint with the VDM console value and answer `no` so this diagnostic does not add a host key:

```powershell
ssh.exe -p <ssh-port> `
    -o StrictHostKeyChecking=ask `
    -o UserKnownHostsFile=NUL `
    -o GlobalKnownHostsFile=NUL `
    -o HostKeyAlgorithms=ssh-ed25519 `
    -o PubkeyAuthentication=no `
    -o PasswordAuthentication=no `
    -o KbdInteractiveAuthentication=no `
    sparky-tunnel@<vdm-host>
```

This is only a fingerprint check. Once it matches, obtain the public host-key line from VDM through a trusted channel and add it to the service's known-hosts file; keep strict host-key checking enabled for the tunnel.

## Create and trust the HTTPS certificate

Create a machine-specific self-signed TLS server certificate on the AI PC with `127.0.0.1` as an IP subject alternative name. Run elevated PowerShell:

```powershell
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject 'CN=Sparky local API' `
    -KeyAlgorithm RSA -KeyLength 2048 -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature, KeyEncipherment `
    -TextExtension @('2.5.29.17={text}IPAddress=127.0.0.1', '2.5.29.37={text}1.3.6.1.5.5.7.3.1') `
    -CertStoreLocation 'Cert:\LocalMachine\My'
$pfxPassword = Read-Host 'PFX password' -AsSecureString
Export-PfxCertificate -Cert $cert -FilePath 'C:\ProgramData\Sparky\sparky-api.pfx' -Password $pfxPassword
Export-Certificate -Cert $cert -FilePath 'C:\ProgramData\Sparky\sparky-api.cer'
```

Microsoft documents `New-SelfSignedCertificate` in [New-SelfSignedCertificate](https://learn.microsoft.com/en-us/powershell/module/pki/new-selfsignedcertificate?view=windowsserver2025-ps). Keep the PFX outside the MSI and install directory; restrict its NTFS ACL to the Sparky service identity and administrators.

Export the certificate's public `.cer` portion and import that public certificate into `Cert:\LocalMachine\Root` on both the AI PC and VDM. Do not copy the PFX/private key to VDM. Set `SPARKY_SERVICE_CERT_PATH` to the AI PC's PFX path and `SPARKY_SERVICE_CERT_PASSWORD` if a PFX password is used. VDM callers must use normal TLS validation for `https://127.0.0.1:<forward-port>`; never use certificate-validation bypasses.

For certificate rotation, import and trust the replacement public certificate on both computers first, then switch Sparky to the replacement PFX. Verify the forwarded HTTPS health request, then remove the old trusted certificate from both machines.

## Configure and install Sparky

1. Build or obtain the self-contained x64 `Perpetuum.ItemStatsExporterInstaller.msi`.
2. Install the MSI on the AI PC. It registers `Perpetuum.Sparky` as an auto-start Windows service running as `NT AUTHORITY\LOCAL SERVICE`; it does not start it during MSI installation.
3. Create `C:\ProgramData\Sparky` and place the verified offline export at `C:\ProgramData\Sparky\output.json`.
4. Store the SSH private key, verified known-hosts file, PFX certificate, settings file, and export in this external data area or another external directory. Do not store secrets in command arguments, source control, the MSI, or logs.
5. From an elevated PowerShell prompt, create the settings file with `tools\Configure-SparkyServiceSettings.ps1`. Supply the model ID and LM Studio URL (use the AI-machine-to-model-machine HTTPS address), certificate path, VDM host/SSH port/tunnel user, SSH key and known-hosts paths, and VDM forward port. The script prompts securely for the Sparky API token, optional LM Studio token, and optional PFX password; it encrypts secret values with Windows DPAPI and resets ACL inheritance throughout the dedicated local Sparky data directory so only Local Service, administrators, and SYSTEM can read it. It refuses system directories and paths with symbolic links or junctions in the path ancestry or data tree, so keep only Sparky data in that folder. Create this file on the AI PC; DPAPI-protected values are machine-specific and must not be copied to another machine.

   Example with non-secret values only:

   ```powershell
   .\tools\Configure-SparkyServiceSettings.ps1 `
       -SettingsPath 'C:\ProgramData\Sparky\service-settings.json' `
       -ModelId 'your-loaded-model-id' `
       -ModelBaseUrl 'https://your-lm-studio-host:1234/v1' `
       -ServiceCertificatePath 'C:\ProgramData\Sparky\sparky-api.pfx' `
       -SshHost 'your-vdm-hostname' -SshPort 22 -SshUser 'sparky-tunnel' `
       -SshPrivateKeyPath 'C:\ProgramData\Sparky\ssh\id_ed25519' `
       -SshKnownHostsPath 'C:\ProgramData\Sparky\ssh\known_hosts' `
       -VdmForwardPort 5080
   ```
6. Ensure the Local Service identity can read the export, key, known-hosts file, PFX, and settings file. Ensure `ssh.exe` exists at `SPARKY_SSH_CLIENT_PATH` or the Windows OpenSSH default path.
7. Start the Sparky service only after the export, settings, certificate trust, and VDM SSH restrictions are ready. Check its safe lifecycle logs, then confirm the tunnel from VDM.

The settings file contains non-secret connection details and DPAPI-encrypted credentials. Keep its ACL restricted even though the secret fields are encrypted. The interactive environment-variable mode remains available for console diagnostics; use the external settings file for the installed Windows service.

## Configuration reference

| Setting | Meaning | Default |
|---|---|---|
| `SPARKY_API_TOKEN` | API bearer token (DPAPI encrypted in settings JSON) | Required; 32+ characters |
| `SPARKY_LMSTUDIO_MODEL_ID` | Model name loaded by LM Studio | Required |
| `SPARKY_LMSTUDIO_BASE_URL` | LM Studio OpenAI-compatible base URL | Required in generated service settings |
| `SPARKY_SERVICE_URL` | HTTPS API bind URL; IPv4 loopback only | `https://127.0.0.1:5080` |
| `SPARKY_SERVICE_CERT_PATH` | PFX path with server private key | Required |
| `SPARKY_SERVICE_CERT_PASSWORD` | Optional PFX password (DPAPI encrypted in settings JSON) | Empty |
| `SPARKY_TUNNEL_ENABLED` | Enable the SSH supervisor | `true` in generated service settings |
| `SPARKY_SSH_HOST` | VDM DNS name or IP | Required |
| `SPARKY_SSH_PORT` | VDM SSH listener port | Required |
| `SPARKY_SSH_USER` | Dedicated tunnel account | Required |
| `SPARKY_SSH_PRIVATE_KEY_PATH` | AI-PC private key path | Required |
| `SPARKY_SSH_KNOWN_HOSTS_PATH` | Verified VDM host-key pin file | Required |
| `SPARKY_VDM_FORWARD_PORT` | VDM loopback listener port | Required |
| `SPARKY_SSH_CLIENT_PATH` | `ssh.exe` path | Windows OpenSSH default path |
| `SPARKY_SSH_RECONNECT_MIN_SECONDS` | Initial retry delay | `5` |
| `SPARKY_SSH_RECONNECT_MAX_SECONDS` | Maximum retry delay | `60` |

The generated settings file also includes the required `SPARKY_API_TOKEN` (DPAPI encrypted), `SPARKY_LMSTUDIO_MODEL_ID`, and `SPARKY_LMSTUDIO_BASE_URL`. Optional LM Studio token is DPAPI encrypted. Existing request limits can be added to the JSON using the setting names `SPARKY_MAX_REQUEST_BODY_BYTES`, `SPARKY_MAX_QUESTION_CHARACTERS`, `SPARKY_MAX_ANSWER_CHARACTERS`, `SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS`, `SPARKY_LMSTUDIO_TIMEOUT_SECONDS`, and `SPARKY_SERVICE_MAX_CONCURRENCY`.

## Validate before game integration

1. From the AI PC, confirm `ssh.exe` connects to VDM using the pinned host key and dedicated identity.
2. On VDM, verify the remote listener is on `127.0.0.1:<forward-port>` only. Confirm it is not bound to `0.0.0.0` or a public/LAN interface.
3. From VDM, call `https://127.0.0.1:<forward-port>/health` and confirm TLS succeeds without a bypass.
4. Submit a valid bearer-authenticated stats and recipe question; verify non-empty answers. Confirm a missing or incorrect bearer token returns HTTP 401.
5. Confirm an invalid SSH key, invalid host-key pin, or untrusted API certificate prevents a successful request.
6. From an external network, confirm the forward port is unreachable and only VDM's SSH port is reachable.
7. Stop SSH connectivity and confirm Sparky retries; stop/restart Sparky and verify child `ssh.exe` processes are cleaned up. Reboot both systems in a controlled order and verify unattended startup.
8. Review logs for absence of question text, answer text, API/model tokens, private keys, and player/account identifiers.

## Rollback

Stop and uninstall the `Perpetuum.Sparky` Windows service. Remove the VDM authorized key and dedicated tunnel account, disable the VDM SSH firewall rule, and stop/disable `sshd` if it was enabled only for Sparky. Remove Sparky's trusted public certificate from both Trusted Root stores after no client depends on it. Keep the export and external configuration if they are needed for later recovery; the MSI does not own or delete them.
