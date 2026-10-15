# Perpetuum Item Stats Exporter

This Windows tool exports translated weapon-module defaults, compatible weapon-ammo variants, base production recipes, and assembled robot facts from a local Perpetuum content database. It also builds a versioned multilingual Sparky knowledge bundle and provides an optional loopback-only HTTPS API that answers item, robot, and source-grounded mechanics questions through LM Studio.

It uses the server's entity factory and property classes to construct and initialize items. The output preserves raw server values and aggregate metadata, and also includes an `AnswerView` projection for player-facing responses. The projection applies the database multiplier, offset, and precision, and uses only field labels and units present in the selected dictionary, including legacy enum-key labels; fields without a public label are omitted. Recipe components are selected from products exposed by the `production_data` view and their base one-unit quantity is calculated through the server's `ProductionComponent` logic. A recipe gets a player-facing answer view only when its product and every component have translations in that language.

The versioned knowledge bundle can include any of the game's dictionaries found in a numeric `customDictionary/<language-id>.json` file. Language IDs and display names come from `CustomDictionary.LanguageNames`; `0.json` is mandatory and defines the canonical public translation-key inventory. Missing translated names are kept as missing and incomplete recipes are not exposed as complete recipes.

Versioned bundles also include deterministic tier-aware entity matching. The reviewed alias catalog is `tools/sparky-entity-resolution-rules.json`: it maps exact `tiertype`/`tierlevel` pairs separately for items and robots, with aliases grouped by dictionary language ID. Language `0` is shared English. Edit this file to adjust accepted wording; the offline build validates the catalog and hashes it into bundle provenance. Once the schema 4 runtime is installed, alias-only edits need a bundle refresh and activation, not another executable deployment.

## Run

From the repository root:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- <GameRoot> <path-to-customDictionary-0.json> <output.json>
```

`<GameRoot>` must contain the normal `perpetuum.ini` with a connection string targeting a local database copy. The single-export command accepts the English `customDictionary/0.json`; it writes the resulting JSON to `<output.json>`.

To build and validate the versioned multilingual bundle on the Main development machine, pass the game data root, the directory containing the dictionary files, the repository root, the approved lore directory, and the output path:

```powershell
$gameRoot = 'G:\PerpetuumServer\data'
$dictionaryDirectory = 'E:\MyStuff\Projects\OPDB\Patches\Live_36\Server\data\customDictionary'
$sourceRoot = 'E:\MyStuff\Projects\PerpetuumServer2'
$loreRoot = 'E:\Perpetuum-lore'
$bundlePath = 'E:\MyStuff\Projects\PerpetuumServer2\sparky-knowledge.bundle.json'

dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --build-knowledge $gameRoot $dictionaryDirectory $sourceRoot $loreRoot $bundlePath
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --validate-bundle $bundlePath
```

The builder reads the local database copy through the offline content initializer, exports the same server-calculated item and recipe values, and adds one localized answer view for each dictionary included in the directory. The manifest records a derived game-content fingerprint, repository revision, UTC build time, and dictionary hashes. No credentials or player/account rows are included. This command does not connect to the live VDI.

Mechanics and lore passages come only from `tools/sparky-knowledge-sources.json`. The two mechanics summaries under `docs/sparky/knowledge/` are hashed together with their supporting gameplay source files. Eleven named lore text files under `E:\Perpetuum-lore` are explicitly allowlisted; the builder does not enumerate that directory. Each lore revision is hashed from its text, and only source IDs and passage hashes enter the bundle manifest. The absolute lore directory and source file paths are not included in the bundle or model prompt. The lexical index has fixed source, passage, text-size, and retrieval limits; questions without relevant evidence receive a localized clarification instead of guessed lore.

The offline initializer registers only the content/entity modules and their readers. It does not call `PerpetuumBootstrapper.Init()`, execute `initServer` or `PersonalConfig`, start auto-activated server processes, or open game/relay listeners. Export code reads entity defaults, definition configs, extensions, aggregate values, module-property mappings, robot-template relations, and aggregate-field metadata. It does not query account or character tables.

To add or regenerate the player-facing projection from an existing raw export without connecting to the database:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --project-existing <input.json> <output.json>
```

To resolve an English item name from an export and return only its verified answer facts:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --answer <input.json> "What are the stats for Standard medium HCL laser?"
```

This flat-export command uses name-only matching and returns `found`, `not_found`, or `ambiguous`; it does not guess when no complete name matches or several independent item names match. Tier-aware matching is used by the schema 4 versioned bundle service, not by this legacy flat-export command.

To let LM Studio phrase a matched answer, start its local API server and run `--ask` on the LM Studio machine. With the `dotnet run` command below, a repository copy must be available there; a published exporter executable can be used instead. The JSON export must also be available on that machine. Copy it from the machine with the local database; `--ask` reads the JSON only and does not need database access.

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --ask <input.json> <model-id> "What are the stats for Standard medium HCL laser?"
```

`--ask` first performs the deterministic item lookup. It does not contact LM Studio for `not_found` or `ambiguous` results. For a match, it sends only the question and that item's verified answer facts (stats, ammo, direct recipe, and recursively expanded materials when available) to the OpenAI-compatible chat-completions endpoint. The default endpoint is `http://localhost:1234/v1`; here, `localhost` means the computer running `--ask`, which should be the LM Studio machine. Override it with `SPARKY_LMSTUDIO_BASE_URL` only when needed. Non-loopback endpoints must use HTTPS. If API authentication is enabled in LM Studio, set `SPARKY_LMSTUDIO_API_TOKEN` in the environment. The generated answer is limited to 2,000 characters by default; set `SPARKY_MAX_ANSWER_CHARACTERS` to tune the cap. Completion tokens default to 700; set `SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS` to tune the model generation budget. Empty-answer errors report finish reason and token counts, without including hidden reasoning text.

Weapon names are included only when the dictionary key matches `entitydefaults.definitionName`. Ammo variants are included only when the game module reports that the ammo definition is loadable. Damage values come from the server's weapon-ammo calculation after attaching the ammo to the module.

The raw export retains every non-default stat produced by the server, including fields that have no custom-dictionary translation. `AnswerView` omits unknown fields without dictionary labels, known enum fields without either a translated label or unit, and ammo variants without a translated display name. Damage entries use values calculated by the server after the ammo is attached to the weapon module. Raw arrays remain available for inspection and later formatting changes.

Recipe exports retain raw product/component identifiers and dictionary names for inspection. Untranslated or partially translated recipes remain in the raw export but are excluded from deterministic lookup and the model prompt, so Sparky cannot present a partial ingredient list as complete. Recipe quantities are base requirements per production cycle; `OutputQuantity` records how many item units that cycle produces. Skill bonuses, production modes, material efficiency, and NIC cost are not included.

### Robot knowledge

Robot facts are built only for definitions with a valid `robottemplaterelation`. The exporter constructs the requested robot definition through the server entity factory, assembles its related components, and runs the normal initialization path before reading properties. Assembled stats therefore include the server's component and equipment-set calculations; each translated component also has its own recognized stats and slot count. Missing or invalid template relations are skipped with a build diagnostic rather than receiving fallback template data.

Chassis bonuses are exported separately from baseline stats. Repeated extension/property contributions across parts are grouped using the server's extension modifier calculation and reported at extension level 1; the bundle does not know a caller's extension level. Module facts use server slot tests and allowed-robot rules to report static eligible component slots, plus the one-per-category restriction where applicable. CPU and power-grid values describe the module itself. Each compatibility response includes a localized note that only static slot eligibility was checked; current free slots and total CPU/power-grid capacity are unknown.

For a recognized robot name, explicit stats, component, bonus, and fitting questions are routed to these structured facts even if the language model classifies them as ordinary item questions. If a fitting question names no exact module in the catalog, Sparky asks in the caller's language and lists the closest exact module names from the localized bundle instead of guessing a fit.

The structured router also ranks assembled robot definitions by baseline `speed_max`, compares two named robots by matching aggregate field IDs and precomputing second-minus-first differences, and lists laser modules using the exported `cf_lasers` category. A clear item-stat/recipe miss does not fall through to lore retrieval. When local sources cannot verify a request, the model can phrase a short clarification from lookup-status metadata and verified candidate names; the localized templates remain as a fallback if the model request fails.

Robot and module answer views are localized from the included custom dictionaries. A missing robot or module name prevents that record from being answerable in that language; a component, field, unit, or bonus without a public translation is omitted. Internal definition names, numeric IDs, and slot masks stay in the bundle for lookup and validation and are excluded from answer facts. The robot export uses the local offline database copy and server calculations; it does not read live data, account or character records, player loadouts, or generate stat and compatibility calculations with the model.

New bundles use schema 4 and include localized robot/module views, validated compatibility references, the module laser category exported from server category flags, exact nullable tier metadata for item/recipe/robot/module records, and the reviewed tier-resolution rules. Runtime matching uses translated public names and exact tier pairs; numeric tier metadata and internal definition names are not included in answer evidence. A tier qualifier alone never selects a record, and ties remain ambiguous. Calibration-template aliases (`CT`, `calibration template`, and `blueprint`) apply only to records whose canonical definition/dictionary key ends exactly in `_cprg`.

The service continues to accept schema 1 bundles with empty robot sections and schemas 2–3 under their existing rollback validation. Those legacy schemas keep name-only matching because they lack tier metadata. The AI executable must be updated once for schema 4 support; after that, changing aliases in the rules file requires only rebuilding and activating a new bundle. See [`docs/superpowers/sparky-knowledge-refresh.md`](../../docs/superpowers/sparky-knowledge-refresh.md) for build, transfer, validation, activation, and rollback steps.

### Recursive recipe expansion

Named higher-tier items can use lower-tier items as recipe components. Direct recipes remain available, and a “full materials” question also returns the recursively expanded terminal components, aggregated by item. Intermediate production yields are accounted for by rounding up to the whole cycles needed. Full expansion is omitted if any required recipe is untranslated, has an invalid output quantity, or forms a cycle; in that case Sparky can still report the direct recipe and state that a complete recursive list is unavailable.

### Local HTTP prototype

The `--serve` mode reads an existing export once and exposes the game API and a separate admin panel. It does not connect to the live database. Both HTTPS listeners bind only to IPv4 loopback; the game API remains on port 5080 and the admin panel defaults to port 5081. The VDM game server and Main admin browser reach their respective listeners through separate SSH reverse forwards, not through public or LAN listeners.

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `SPARKY_DEV_API_TOKEN` | Required | Dev profile bearer token, at least 32 characters |
| `SPARKY_LIVE_API_TOKEN` | Disabled when empty | Separate Live profile bearer token; must differ from Dev and admin tokens |
| `SPARKY_ADMIN_API_TOKEN` | Required | Separate admin bearer token, at least 32 characters; must differ from Dev/Live API tokens |
| `SPARKY_LMSTUDIO_MODEL_ID` | Required | Model identifier configured in LM Studio |
| `SPARKY_SERVICE_URL` | `https://127.0.0.1:5080` | Local HTTPS listener URL; must remain on IPv4 loopback |
| `SPARKY_ADMIN_SERVICE_URL` | `https://127.0.0.1:5081` | Separate local HTTPS admin listener; must remain on IPv4 loopback and use a different port |
| `SPARKY_SERVICE_CERT_PATH` | Required | HTTPS certificate PFX path; the certificate must include the `127.0.0.1` IP SAN |
| `SPARKY_SERVICE_CERT_PASSWORD` | Empty | Optional PFX password |
| `SPARKY_LMSTUDIO_BASE_URL` | `http://localhost:1234/v1` | OpenAI-compatible LM Studio endpoint |
| `SPARKY_LMSTUDIO_API_TOKEN` | Empty | Optional LM Studio API token |
| `SPARKY_MAX_REQUEST_BODY_BYTES` | `8192` | Maximum JSON request body size |
| `SPARKY_MAX_QUESTION_CHARACTERS` | `2000` | Maximum trimmed question length |
| `SPARKY_MAX_ANSWER_CHARACTERS` | `2000` | Maximum returned answer length |
| `SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS` | `700` | Model completion budget |
| `SPARKY_LMSTUDIO_TIMEOUT_SECONDS` | `120` | Maximum wait for one model response |
| `SPARKY_SERVICE_MAX_CONCURRENCY` | `2` | Maximum simultaneous model requests |
| `SPARKY_TRACE_DB_PATH` | `%ProgramData%\Sparky\traces\sparky-traces.db` | Local SQLite trace, profile, feedback, evaluation, and replay store |
| `SPARKY_DEV_TRACE_CAPTURE_CONTENT` | `true` | Store Dev question/answer content; queries and entity candidates expire under content retention |
| `SPARKY_LIVE_TRACE_CAPTURE_CONTENT` | `false` | Store Live question/answer content |
| `SPARKY_TRACE_CONTENT_RETENTION_DAYS` | `30` | Days before captured text and replay content is cleared |
| `SPARKY_TRACE_METADATA_RETENTION_DAYS` | `90` | Days before trace metadata and related records are deleted |
| `SPARKY_ADMIN_TUNNEL_ENABLED` | `false` | Enable the separate AI-to-Main admin reverse tunnel |
| `SPARKY_ADMIN_SSH_HOST` | Required when enabled | Main machine SSH host for the admin tunnel |
| `SPARKY_ADMIN_SSH_PORT` | `22` | Main machine SSH port for the admin tunnel |
| `SPARKY_ADMIN_SSH_USER` | Required when enabled | Restricted Main machine tunnel account |
| `SPARKY_ADMIN_SSH_PRIVATE_KEY_PATH` | Required when enabled | Admin-only SSH private key; keep it separate from the game API tunnel key |
| `SPARKY_ADMIN_SSH_KNOWN_HOSTS_PATH` | Required when enabled | Admin tunnel host-key pin file; keep a separate copy |
| `SPARKY_ADMIN_FORWARD_PORT` | `5082` | Main-side loopback listener forwarded to the AI admin listener |

Generate an ephemeral bearer token for the current PowerShell session, set the model, and start the service:

```powershell
$tokenBytes = [byte[]]::new(32)
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($tokenBytes) } finally { $rng.Dispose() }
$env:SPARKY_API_TOKEN = [Convert]::ToBase64String($tokenBytes)
$adminTokenBytes = [byte[]]::new(32)
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($adminTokenBytes) } finally { $rng.Dispose() }
$env:SPARKY_ADMIN_API_TOKEN = [Convert]::ToBase64String($adminTokenBytes)
$env:SPARKY_LMSTUDIO_MODEL_ID = "your-loaded-model-id"
$env:SPARKY_LMSTUDIO_BASE_URL = "http://localhost:1235/v1"

$serviceDirectory = 'D:\Sparky\SparkyItemStatsExporter'
$serviceJob = Start-Job -ArgumentList $serviceDirectory -ScriptBlock {
    param($directory)
    Set-Location -LiteralPath $directory
    & .\Perpetuum.ItemStatsExporter.exe --serve .\output.json
}
Start-Sleep -Seconds 2
if ($serviceJob.State -ne 'Running') {
    Receive-Job -Job $serviceJob -Keep
    throw 'Sparky failed to start; inspect the job output above.'
}
```

The `localhost` model URL above applies when LM Studio is on the same computer as Sparky. If LM Studio is on another computer, use its HTTPS endpoint; the answer client rejects plain HTTP to non-loopback hosts. The incoming Sparky API remains HTTPS and loopback-only; VDM access is carried by the SSH reverse tunnel described in `docs/superpowers/sparky-vdm-reverse-ssh-operations.md`.

From the same PowerShell window, check readiness and ask a question. The job inherited the token environment variable; the token stays in the current shell rather than being printed:

```powershell
$headers = @{ Authorization = "Bearer $env:SPARKY_API_TOKEN" }
Invoke-RestMethod https://127.0.0.1:5080/health

$body = @{ question = "What are the stats for Standard medium HCL laser?" } | ConvertTo-Json -Compress
Invoke-RestMethod https://127.0.0.1:5080/v1/answer -Method Post -Headers $headers -ContentType "application/json" -Body $body

# Optional follow-up hint. The name must match a public display name in the export.
$followUp = @{ question = "What is its recipe?"; contextItem = "Standard medium HCL laser" } | ConvertTo-Json -Compress
Invoke-RestMethod https://127.0.0.1:5080/v1/answer -Method Post -Headers $headers -ContentType "application/json" -Body $followUp

# When finished:
Stop-Job $serviceJob
Remove-Job $serviceJob
```

If a health request says it cannot connect, check the service job before retrying:

```powershell
$serviceJob.State
Receive-Job -Job $serviceJob -Keep
Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue
```

`GET /health` returns `{"status":"ok","bundleVersion":"..."}` and does not need authentication. `POST /v1/answer` requires the bearer token and accepts a JSON `question` plus optional public `contextItem`, `contextLanguageId`, ordered `contextTurns`, `contextSummary`, `pendingClarification`, and normalized `callerRank`. Context is size-checked and further bounded by the active profile. The interpreter treats history and summaries as untrusted dialogue context, never as facts or instructions. The game server owns and expires source-separated player context; the AI API uses it for the current request and does not persist a caller session. No caller identity or raw Discord/game permissions cross the API boundary. The response may include a bounded `contextUpdate` for the game server to store.

```json
{"status":"answered","item":"Standard medium HCL laser","answer":"Accuracy: 12"}
{"status":"not_found"}
{"status":"ambiguous","candidates":["Standard medium HCL laser","... another match"]}
```

Unauthenticated requests receive 401; malformed input receives 400; requests over a configured limit receive 413; saturated inference capacity returns a localized `busy` response; model failures return a generic 502 or 504. Normal application logs exclude question/answer contents, context names, and tokens. The local trace database can capture question/answer contents only when that profile's content-capture setting is enabled, and retention later clears them. The service does not accept account/character identifiers or retain follow-up context. A not-found or ambiguous structured lookup can trigger the small interpretation call, but it does not send an answer-generation prompt with item facts.

### Admin panel, hot configuration, and replay

The panel is served at `https://127.0.0.1:5081/sparky-admin/` on the AI machine. If the separate admin tunnel is enabled, open `https://127.0.0.1:5082/sparky-admin/` on Main. The AI and Main URLs are loopback-only; the panel does not bind to a LAN interface. The game API tunnel remains on Main port 5080 and cannot reach the admin listener.

The panel token is independent from both game API credentials. Keep the clear admin token in a password manager; the service settings helper stores only a machine-DPAPI-protected value in `service-settings.json`. The browser retains the token only in `sessionStorage` for the current tab and clears it on logout. The panel shows request traces, tunnel state, profile status, model/bundle versions, and content-capture settings. Trace filters are bounded and support UTC date range, profile, source, outcome, and feedback label.

The Dev/Live profile editor exposes bounded conversation context and answer phrasing settings. `ConversationHistoryTurns` counts user/assistant messages and must be even (2–16); `ConversationHistoryCharacters` is 256–8,000. The answer model's temperature is 0–2 and top-p is 0.01–1. These settings do not change the deterministic question interpreter. Context and tuning are applied from the selected immutable profile revision on each request. Existing schema-v1 profile JSON is loaded as schema v2 with defaults of 8 messages, 4,000 characters, temperature 0.2, and top-p 0.9; the stored historical JSON is left untouched until a new revision is saved.

Dev and Live revisions are separate. Edit the Dev profile, validate it, then activate the selected draft without restarting the service. Changes to Live require an explicit Dev-to-Live promotion confirmation; rollback creates a new revision from the previous activated version. Route aliases are literal phrases with an intent, language, priority, and optional excluded phrases; the validator rejects arbitrary regular expressions. Bundle refresh stages a validated immutable bundle in the local catalog; select that version in a profile draft before activating it.

For model tuning, label a trace with feedback, then explicitly add it as a reviewed evaluation case. The case editor starts with the selected trace question and lets you add, delete, reorder, and annotate up to 12 conversation turns. Each turn can record expected scope, intent, resolved entity, source IDs, required facts, forbidden claims, and other expected properties; the case can also have a short note. Feedback alone never changes routing or answers. Replay compares the active revision against one selected draft using up to 100 reviewed cases, runs each sequence in order with fresh, separate context for Active and Draft, and displays per-turn interpretation, query, evidence IDs, resolved entity, response, and effective profile metadata. Existing stored one-question cases appear as one-turn sequences after an additive SQLite migration; the migration does not rewrite existing trace, feedback, review, or replay rows. Replay uses the same concurrency and context limits and has no game or Discord delivery path. Start with a few examples because each turn is evaluated against both revisions and can take multiple model calls. Replay is not a fact checker: compare routes and evidence against the reviewed expectations.

### Comparison evaluation sequences

Add these as multi-turn Dev evaluation cases from a captured trace or by entering the first question, then adding and ordering the remaining turns in the evaluation editor. Add per-turn expectations where useful and give each case a short note. Use properties such as `comparison:contextual`, `comparison:clarification`, or `comparison:global` to label the intended route; use required facts for values that must be preserved. Select the cases and a Dev draft in the replay panel. Check every turn, including the follow-up, against the expected route and evidence:

1. **Contextual robot speed:** “What are stats for Kain?” → “And Tyrannos?” → “Which one is faster?” Expect a contextual robot comparison of Kain (103.32 kph) and Tyrannos (93.6 kph), with Kain ahead by 9.72 kph. It must not use the global fastest-robot route.
2. **Requested item attribute:** “Tell me about Standard medium HCL laser” → “Tell me about Standard Medium Missile Launcher” → “Which has more CPU usage?” Expect the launcher at 40 TF versus the HCL laser at 30 TF. Preserve both values and identify the launcher as higher.
3. **Shared item facts:** Ask about the same two items, then “Compare their stats.” Expect only common, verified numeric fields with matching units; do not fill missing fields or compare unrelated values.
4. **Too many robot candidates:** Ask about Kain, Tyrannos, and Weasel, then “Which one is faster?” Expect clarification because three recent robots are plausible; do not choose a pair silently.
5. **No criterion:** Ask about Kain and Tyrannos, then “Which is better?” Expect a request for a measurable criterion, not a universal winner.
6. **Topic-change boundary:** Ask about Kain, then “Who is Theseus?”, then “Which one is faster?” Expect clarification because the lore question ended the robot comparison context; do not reuse Kain from stale context.
7. **Global ranking:** After a robot-pair comparison, ask “Which robot is fastest?” Expect the global ranking route, not a contextual pair comparison.

Replay starts Active and Draft from separate empty contexts for every case. It does not deliver messages or activate/promote the draft. Context counts, parsed/effective route, resolver status, and comparison outcome are visible in the request trace; captured context text and comparison values appear in the trace detail and export only when content capture is enabled and the operator requests content.

See [`docs/superpowers/sparky-conversation-first-operations.md`](../../docs/superpowers/sparky-conversation-first-operations.md) for the operator workflow and expectations format.

Captured questions, answers, search queries, entity candidates, review notes, and replay output are cleared under content retention. Trace/profile metadata and related records expire under metadata retention. The database remains local to AI. When trace storage is unavailable, the panel marks it and profile edits/replay are disabled rather than silently presenting volatile edits as durable.

Trace JSON export is server-generated and bounded. By default it omits question/answer text, search queries, entity candidates, and feedback notes; it includes selected source IDs and revision metadata. Content can be included explicitly when it was captured and has not expired.

For a manual PowerShell session, the independent admin token is in `$env:SPARKY_ADMIN_API_TOKEN`; the service never returns it. The authenticated API provides profile drafts, validation, activation, promotion and rollback; staged bundle management; trace summaries/timelines and export; feedback; reviewed examples; replay creation and retrieval; and tunnel status. The general game endpoint `GET /health` remains unauthenticated on port 5080 and reports the active Dev bundle version.

For the second reverse-forward key, Main OpenSSH restrictions, known-host pinning, and the PowerShell settings script parameters, follow [Sparky VDM reverse SSH operations](../../docs/superpowers/sparky-vdm-reverse-ssh-operations.md#separate-admin-panel-tunnel). Do not reuse the game API tunnel key or its permitted listen port.

### Refreshing the AI-machine bundle

Use [`docs/superpowers/sparky-knowledge-refresh.md`](../../docs/superpowers/sparky-knowledge-refresh.md) for the repeatable Main-machine build, transfer hash check, AI-machine validation, bundle staging, profile activation, health verification, and rollback steps. Bundle activation is a hot profile change; it does not restart Sparky or the game server.

Do not expose this endpoint on a LAN or through router port forwarding. Keep the reverse tunnel, API bearer token, and normal TLS certificate validation enabled for VDM access.

### Live season snapshots

The game server can send bounded public season facts to the AI service; Sparky never queries the game database. `SeasonService` builds the snapshot from its existing five-minute cache and reuses the filtered public leaderboard it already loads hourly. Season-end records contain only public ranks, display names, and points—never character/account IDs. Current snapshots are coalesced in memory, and small metadata heartbeats refresh an unchanged snapshot every cache tick. The default AI profile freshness limit is 15 minutes and is adjustable in the Dev/Live profile editor. This age describes when Sparky last received the snapshot; leaderboard ranks and points can still be up to one hourly calculation interval old. The update adds no extra leaderboard query.

The game client derives these authenticated routes from the configured loopback API authority, so the existing reverse SSH tunnel carries the traffic:

- `PUT /v1/live-knowledge/current` replaces the current season snapshot.
- `POST /v1/live-knowledge/current/refresh` refreshes its capture time without resending unchanged facts.
- `POST /v1/live-knowledge/seasons/{seasonId}/finalize` archives public final standings.

Tokens select the profile: the Dev token can write only Dev data and the Live token can write only Live data. Updates use schema 1, are limited to 256 KiB, and require a matching operation/hash acknowledgement. A missing or mismatched current hash makes the game client resend the full snapshot. Temporary current updates are retried in the background and coalesced to the latest state.

Final standings are first written by a background spooler to `<GameRoot>\logs\sparky-live-knowledge-outbox.jsonl`, then delivered with retry and removed only after a matching acknowledgement. The outbox is limited to 2 MiB and 256 pending finals. Give the game-server process identity write access to the `logs` directory; on Main debug this is the interactive server identity, while a service deployment needs the service identity. On AI, the catalog database is `live-knowledge.db` beside the configured trace database (by default `%ProgramData%\Sparky\traces\live-knowledge.db`); the Sparky service identity needs the same directory access required for trace storage. Neither file contains secrets or private IDs.

Use the authenticated admin route `GET /admin/api/live-knowledge/status?environment=Dev` or `...environment=Live` to inspect freshness, capture age, semantic hash, and archived-season count. The panel shows this status for the selected profile. For replay testing, choose **Synthetic season sample** in Dev's replay panel; it supplies one synthetic current season and two synthetic archived seasons to both Active and Draft. The fixture is replay-only: it does not change the profile catalog, and Live replay rejects it.

To make a season replay case, enable content capture for Dev, ask Sparky a starter question such as “What is the current season?”, load the traces, and select that request. In the selected trace's **Add reviewed evaluation case** form, keep the captured question as turn one, set its source ID to `live-season-current`, then add the follow-up “Who won the previous season?” and set its source ID to `live-season-final-1002`. Save the case, choose **Synthetic season sample**, select the case and a draft revision, then run replay. The two turns should show the synthetic current and archived source IDs for both Active and Draft. The replay result reports freshness and selected sources; the profile status line reports freshness, age, hash, and archived count. `missing` or `stale` means current-season facts are unavailable for answering; archived finals remain usable when current data is stale. Turn content appears only in traces when content capture is enabled.

Main emits Dev snapshots only when the local game server runs with the Sparky adapter enabled. The local database copy may have no season data; that correctly produces an offseason snapshot, while the Dev replay fixture exercises season questions. Live snapshots begin only after the planned VDI game-server update/restart. This change does not deploy to VDI or restart the Live server. Historical results from seasons ended before that deployment are not reconstructed automatically; backfill only after reviewing public season announcements.
