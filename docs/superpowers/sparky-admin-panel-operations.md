# Sparky Admin Panel Operations

This runbook covers the local Dev/Live tuning workflow for the Sparky Admin Panel. AI hosts the Sparky Windows service and panel. Main accesses the panel through its own reverse SSH tunnel and runs the game server in Visual Studio for Dev acceptance. Live remains unchanged unless a Dev revision is explicitly promoted.

## Network and listener map

| Use | AI listener | Main listener | Authentication |
| --- | --- | --- | --- |
| Game API | `https://127.0.0.1:5080` | `https://127.0.0.1:5080` | Dev or Live game bearer token |
| Admin panel | `https://127.0.0.1:5081` | `https://127.0.0.1:5082` | Separate admin bearer token |

Both AI listeners bind to IPv4 loopback and use the Sparky TLS certificate. The two reverse SSH forwards use separate keys, known-host pins, and Main-side restricted listen ports. Do not open the API or panel ports on a router, LAN interface, or public firewall. Do not reuse the game API tunnel key for the admin tunnel. See [the reverse SSH operations guide](sparky-vdm-reverse-ssh-operations.md#separate-admin-panel-tunnel) for the Main OpenSSH key restrictions and tunnel setup.

## Provision AI settings

Use the existing self-contained Sparky installation on AI. The service executable runs from `C:\Program Files\PerpetuumSparky`; the command-line copy under `D:\Sparky\SparkyItemStatsExporter` is useful for validation but is not the Windows service installation.

When creating or rotating credentials, generate three different random bearer tokens and store their clear values in a password manager:

- Dev API token for Main's local test server.
- Live API token only when Live is ready to be enabled.
- Admin token for the panel browser.

On AI, run the protected settings helper in an elevated PowerShell session. Supply the same model, certificate, and game-tunnel details already used by the working Sparky service. Add the admin tunnel arguments for the restricted Main-side admin account and its dedicated key/known-host files:

```powershell
& 'E:\MyStuff\Projects\PerpetuumServer2\tools\Configure-SparkyServiceSettings.ps1' `
  -SettingsPath 'C:\ProgramData\Sparky\service-settings.json' `
  -ModelId '<loaded-LM-Studio-model-id>' `
  -ModelBaseUrl 'http://<AI-model-host>:1235/v1' `
  -ServiceCertificatePath 'C:\ProgramData\Sparky\sparky-api.pfx' `
  -SshHost '<Main-address>' -SshPort 22 -SshUser '<game-tunnel-user>' `
  -SshPrivateKeyPath 'C:\ProgramData\Sparky\ssh\sparky_vdm_tunnel_ed25519' `
  -SshKnownHostsPath 'C:\ProgramData\Sparky\ssh\known_hosts' -VdmForwardPort 5080 `
  -AdminSshHost '<Main-address>' -AdminSshPort 22 -AdminSshUser '<admin-tunnel-user>' `
  -AdminSshPrivateKeyPath 'C:\ProgramData\Sparky\ssh\sparky_admin_panel_ed25519' `
  -AdminSshKnownHostsPath 'C:\ProgramData\Sparky\ssh\admin_known_hosts' -AdminForwardPort 5082
```

The helper prompts for Dev, optional Live, admin, model API, and PFX secrets and protects stored values with machine DPAPI. Recreate the file on AI; it is machine-bound. Keep the service settings and private keys outside publish artifacts. Restart `Perpetuum.Sparky` on AI after changing service settings, certificate, or tunnel credentials. Profile edits and bundle activation do not require a restart.

For Main Dev testing, provision the matching Dev token with `tools/Configure-SparkyVdmClientSettings.ps1 -Profile dev` for the Windows identity that runs the local game process. The script writes the token to the machine-protected client settings file; keep it out of `perpetuum.ini`. Configure Live only on its intended game host after a separate rollout decision.

## Open the panel from Main

Transfer only the public certificate file (`sparky-api.cer`) to Main through the approved private method. Verify its thumbprint against the AI certificate and import it into the current user's trusted roots:

```powershell
Import-Certificate -FilePath 'C:\ProgramData\Sparky\sparky-api.cer' `
  -CertStoreLocation 'Cert:\CurrentUser\Root'
```

Keep the PFX and its private key on AI. Do not use `-SkipCertificateCheck`, `-k`, or a global TLS validation callback.

Confirm on AI that the service is running and has listeners on `127.0.0.1:5080` and `127.0.0.1:5081`. Confirm the admin tunnel supervisor is running. On Main, check the separate forward and open:

```powershell
Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort 5082
Invoke-RestMethod 'https://127.0.0.1:5082/health'
```

Open `https://127.0.0.1:5082/sparky-admin/` in a browser and enter the independent admin token. The game token must be rejected by the admin API. The panel on AI is at `https://127.0.0.1:5081/sparky-admin/`.

If the Main panel tunnel stops, the panel remains available on AI and the game API tunnel is independent. The Main server should never receive a listener bound to `0.0.0.0` for these forwards.

## Trace review and feedback

Use the **Request history** filters to select Dev, a source (Game/Discord/AI), a status, date range, or previous feedback label. A request trace joins events through a generated request ID and excludes caller, character, account, Discord user, and channel IDs.

Captured question/answer fields appear only when the selected profile enables content capture and the content has not expired. Dev capture defaults on for local tuning; Live defaults off. Search query and entity candidate diagnostics are also subject to content retention. **Export selected trace** is redacted by default. Enable its content checkbox only when you need the captured question and answer for a local investigation; do not post content-bearing exports publicly.

Use **Label this trace** for advisory feedback:

- `wrong_route` — intent or route should differ.
- `missed_entity` — a named entity was overlooked.
- `retrieval_miss` — the relevant passage or structured fact was not selected.
- `incorrect_fact` — output conflicts with available source evidence.
- `language`, `style`, `context`, `delivery`, or `infrastructure` — translation, phrasing, follow-up, delivery, or runtime issue.

Feedback does not change profile settings or answer behavior. A free-form note is limited to 500 characters and is cleared under content retention.

## Create and replay reviewed cases

From a captured trace, use **Add reviewed evaluation case** to store a human-reviewed expected intent and optional properties. This action is explicit; the system does not infer a correct answer. A trace without captured question text cannot become an evaluation case. Use short properties such as `entity:Theseus`, `focus:Stats`, or `source:agent-archer-weasel`.

Select a handful of cases in **Evaluation cases and replay**, choose the exact draft revision, then select **Replay selected cases**. Replay sends each saved question through the active and candidate profile's normal interpreter and answer path with their own model, route, retrieval, persona, and bundle settings. It shares the service inference capacity, may take time, and stores outputs locally. It has no game chat or Discord delivery path.

Compare the intent/status, selected source IDs, and wording side by side with the reviewed expectation and the source passages. Replay is a tuning aid, not a factual verification result. A changed answer is not necessarily better unless its route and selected evidence support the expected result. Replay output and its saved question expire under content retention.

## Edit and activate a profile

1. Keep **Profile** set to `Dev` while testing on Main.
2. Edit model ID, answer/completion limits, timeout, concurrency, capture/retention, retrieval, persona, literal aliases, or bundle version.
3. For aliases, enter JSON using literal `phrase`, `intent`, optional `priority`, optional `languageId`, and optional `excludedPhrases`. Supported intents are the enum names shown in the panel's expected-intent selector. Regex syntax, SQL, scripts, and arbitrary expressions are not accepted.
4. Enter a change reason, save a draft, and validate it. Invalid settings cannot replace the active snapshot.
5. Replay reviewed cases against that exact draft when useful.
6. Select the draft and activate it in Dev. The next request uses the new immutable snapshot; in-flight requests finish against the snapshot with which they started.
7. Confirm the active revision and bundle shown in profile status and `/health`. Confirm Live did not change.

### Bundle updates

On Main, build and validate the offline bundle as documented in [Sparky knowledge refresh](sparky-knowledge-refresh.md). Transfer the JSON bundle to AI. Either use the helper script to validate and stage it under `C:\ProgramData\Sparky\bundles`, or upload it in the panel using **Stage validated knowledge bundle**. The bundle version is immutable; a different file with an existing version is rejected. Select it in a Dev draft and activate after review. The staged bundle does not become active automatically.

### Promote or roll back

Promotion is separate from activating Dev. Select a saved Dev revision, review the confirmation, and use **Promote Dev revision to Live** only after the Live profile is enabled with its own API token. Promotion copies the selected revision and its bundle into a new Live revision; it does not change the Dev profile. Never use Live for local Main testing.

Use **Rollback profile** to activate the previous successfully activated revision as a new revision. The audit history records saved, activated, promoted, and rolled-back revisions with the supplied reason. No game or Sparky service restart is required.

## Storage and recovery

The AI service keeps its SQLite database at `%ProgramData%\Sparky\traces\sparky-traces.db` by default and immutable bundles under `%ProgramData%\Sparky\bundles`. The installer/settings helper grants the service account access to these paths. The panel shows storage status. If storage is unavailable, profile mutation, feedback, evaluation, replay, and trace views report unavailable; answer service availability is independent. Restore the storage directory and service identity access before tuning.

Content retention clears question, answer, search query, entity-candidate, review-note, and replay-output text. Metadata retention removes the related traces, feedback, evaluation examples, and replay records. The service logs deletion counts but never logs deleted text. Retention runs once per day per active profile.

For binary/settings changes or service start failures, inspect the Windows Sparky service log and verify ACLs for `service-settings.json`, the certificate PFX, bundle catalog, trace directory, SSH private keys, and known-host pin files. Do not copy the DPAPI settings file to another machine. Keep backups of the SQLite database and bundle files using a private local backup method; exclude credentials and private keys from ordinary archives.

## Main/AI acceptance checklist

Before considering the panel ready for routine tuning, verify on the local Main/AI setup:

1. Admin token succeeds; Dev/Live game tokens fail on port 5081; Admin token fails on port 5080.
2. AI listeners bind only to `127.0.0.1`; Main has separate loopback listeners at 5080 and 5082.
3. A Dev profile edit activates without restarting Sparky; Live revision and bundle remain unchanged.
4. Invalid profile and invalid bundle leave the active snapshot unchanged.
5. Feedback labels a trace but does not mutate active settings; reviewed example replays without a game/Discord message.
6. Default export excludes content; explicit content export contains only content that was captured and is within retention.
7. Content and metadata retention expire independently in a disposable test store.
8. Stopping the admin tunnel affects the Main panel only; the AI panel and game API tunnel stay available.

Do not perform these acceptance steps on Live. Any Live rollout needs its own explicit decision and maintenance plan.
