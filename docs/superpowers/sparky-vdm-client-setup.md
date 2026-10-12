# Sparky VDM Game-Server Client Setup

This guide configures the VDM-side game-server adapter. It does not change the live VDI; validate these steps on a non-live copy first.

## Order of operations

1. Publish and start the updated `Perpetuum.ItemStatsExporter` on the AI PC first. The VDM client sends the optional `contextItem` field for follow-up questions.
2. Confirm the reverse SSH tunnel is established and the Sparky API is reachable at `https://127.0.0.1:5080/health` from the AI PC.
3. On the non-live game-server machine, identify the Windows service that hosts Perpetuum and inspect its configured logon identity. Do not infer this from the account used for RDP or an elevated PowerShell window.
4. From an elevated Windows PowerShell 5.1 session on that machine, run the provisioning script with that exact service identity:

   ```powershell
   .\tools\Configure-SparkyVdmClientSettings.ps1 -ServiceIdentity 'DOMAIN\PerpetuumService'
   ```

   Replace the example with the verified identity. The script prompts for the bearer token as a secure string, protects it with machine-scope DPAPI, and restricts the settings directory and file to that identity, SYSTEM, and local Administrators. The token is not passed in command arguments or written to `perpetuum.ini`.
5. Add the non-secret options to the game server's `perpetuum.ini`. Keep `enableSparky` false until the non-live checks are ready.

   ```json
   {
     "enableSparky": false,
     "sparkyApiUrl": "https://127.0.0.1:5080/v1/answer",
     "sparkyProtectedSettingsPath": "C:\\ProgramData\\Sparky\\vdm-client-settings.json",
     "sparkyRequestTimeoutSeconds": 90,
     "sparkyMaxQuestionCharacters": 1000,
     "sparkyMaxAnswerCharacters": 2000,
     "sparkyQueueCapacity": 64,
     "sparkyWorkerCount": 2,
     "sparkyPendingPerCallerLimit": 1,
     "sparkyContextLifetimeMinutes": 15,
     "sparkyContextCapacity": 2048,
     "sparkySenderCharacterNick": "[OPP] Sparky - The Syndicate Welcome Agent"
   }
   ```

   Merge these properties into the existing JSON object; do not replace the rest of `perpetuum.ini`. The defaults shown here are also used when an optional property is omitted.
6. Install the verified Sparky API certificate in the VDM computer's trust store as directed by the existing reverse-SSH operations guide. Keep normal TLS certificate validation enabled. The URL must remain the IPv4 loopback endpoint.
7. Inspect the settings file ACL with `icacls`, verify the service identity can read it, and verify an unrelated standard user cannot. Then run the non-live checks below. Set `enableSparky` to true only for that non-live validation.

## Supported behavior

- In-game: `Sparky, ...` in a channel. The original message follows normal channel logging and channel-to-Discord forwarding. The answer is posted only in the original game channel while the caller is online; otherwise it is sent by in-game mail.
- Discord: mention the Sparky bot in any channel it can access. The reply stays in that Discord channel and cannot ping users. Existing mapped Discord-to-game forwarding remains unchanged.
- Follow-up context is in memory, isolated by game character or Discord author, and stores only the last successfully resolved public item display name. It expires after the configured inactivity interval and is cleared at server shutdown.
- The API receives only the question and optional public item display name. No character, account, Discord, or channel identifier crosses the API boundary.
- Whole final messages are capped at 2,000 characters. Do not raise `sparkyMaxAnswerCharacters` above 2,000.

## Non-live validation and rollback

Check a normal item-stat question, direct recipe, recursive materials, a follow-up, and a follow-up that explicitly names another item. Check a bare trigger, an online response, logout during inference and mail delivery, a linked channel with no answer mirrored to Discord, Discord mentions in mapped and unmapped channels, queue saturation, API/tunnel/TLS/auth failure, and output truncation. Review logs to confirm they contain no question text, answer text, tokens, or caller IDs.

Invalid Sparky-only configuration or an unresolved sender disables Sparky without blocking normal server startup. To roll back, set `enableSparky` to false and remove the protected token file when convenient. Do not change, restart, or reboot the live VDI as part of this development validation; live deployment requires its own maintenance-window decision.
