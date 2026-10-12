# Perpetuum Item Stats Exporter

This Windows tool exports translated weapon-module defaults, compatible weapon-ammo variants, and base production recipes from a local Perpetuum content database. It also provides an optional loopback-only HTTP prototype that answers supported item questions from the generated offline export through LM Studio.

It uses the server's entity factory and property classes to construct and initialize items. The output preserves raw server values and aggregate metadata, and also includes an `AnswerView` projection for player-facing responses. The projection applies the database multiplier, offset, and precision, uses dictionary labels when present, and otherwise humanizes known `AggregateField` enum names when a translated unit is available. Recipe components are selected from products exposed by the `production_data` view and their base one-unit quantity is calculated through the server's `ProductionComponent` logic. A recipe gets a player-facing answer view only when its product and every component have English dictionary translations.

## Run

From the repository root:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- <GameRoot> <path-to-customDictionary-0.json> <output.json>
```

`<GameRoot>` must contain the normal `perpetuum.ini` with a connection string targeting a local database copy. Pass the complete English `customDictionary/0.json` from the Sparky machine. The tool writes the resulting JSON to `<output.json>`.

The offline initializer registers only the content/entity modules and their readers. It does not call `PerpetuumBootstrapper.Init()`, execute `initServer` or `PersonalConfig`, start auto-activated server processes, or open game/relay listeners. Export code reads entity defaults, definition configs, extensions, aggregate values, module-property mappings, robot-template relations, and aggregate-field metadata. It does not query account or character tables.

To add or regenerate the player-facing projection from an existing raw export without connecting to the database:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --project-existing <input.json> <output.json>
```

To resolve an English item name from an export and return only its verified answer facts:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --answer <input.json> "What are the stats for Standard medium HCL laser?"
```

The lookup matches complete dictionary display names or definition names inside the question. It returns `found`, `not_found`, or `ambiguous`; it does not guess when no complete name matches or several independent item names match.

To let LM Studio phrase a matched answer, start its local API server and run `--ask` on the LM Studio machine. With the `dotnet run` command below, a repository copy must be available there; a published exporter executable can be used instead. The JSON export must also be available on that machine. Copy it from the machine with the local database; `--ask` reads the JSON only and does not need database access.

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --ask <input.json> <model-id> "What are the stats for Standard medium HCL laser?"
```

`--ask` first performs the deterministic item lookup. It does not contact LM Studio for `not_found` or `ambiguous` results. For a match, it sends only the question and that item's verified answer facts (stats, ammo, direct recipe, and recursively expanded materials when available) to the OpenAI-compatible chat-completions endpoint. The default endpoint is `http://localhost:1234/v1`; here, `localhost` means the computer running `--ask`, which should be the LM Studio machine. Override it with `SPARKY_LMSTUDIO_BASE_URL` only when needed. Non-loopback endpoints must use HTTPS. If API authentication is enabled in LM Studio, set `SPARKY_LMSTUDIO_API_TOKEN` in the environment. The generated answer is limited to 2,000 characters by default; set `SPARKY_MAX_ANSWER_CHARACTERS` to tune the cap. Completion tokens default to 700; set `SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS` to tune the model generation budget. Empty-answer errors report finish reason and token counts, without including hidden reasoning text.

Weapon names are included only when the dictionary key matches `entitydefaults.definitionName`. Ammo variants are included only when the game module reports that the ammo definition is loadable. Damage values come from the server's weapon-ammo calculation after attaching the ammo to the module.

The raw export retains every non-default stat produced by the server, including fields that have no custom-dictionary translation. `AnswerView` omits unknown fields without dictionary labels, known enum fields without either a translated label or unit, and ammo variants without a translated display name. Damage entries use values calculated by the server after the ammo is attached to the weapon module. Raw arrays remain available for inspection and later formatting changes.

Recipe exports retain raw product/component identifiers and dictionary names for inspection. Untranslated or partially translated recipes remain in the raw export but are excluded from deterministic lookup and the model prompt, so Sparky cannot present a partial ingredient list as complete. Recipe quantities are base requirements per production cycle; `OutputQuantity` records how many item units that cycle produces. Skill bonuses, production modes, material efficiency, and NIC cost are not included.

### Recursive recipe expansion

Named higher-tier items can use lower-tier items as recipe components. Direct recipes remain available, and a “full materials” question also returns the recursively expanded terminal components, aggregated by item. Intermediate production yields are accounted for by rounding up to the whole cycles needed. Full expansion is omitted if any required recipe is untranslated, has an invalid output quantity, or forms a cycle; in that case Sparky can still report the direct recipe and state that a complete recursive list is unavailable.

### Local HTTP prototype

The `--serve` mode reads an existing export once and exposes a small local API. It does not connect to the live database. It binds only to IPv4 loopback and uses HTTPS; the VDM game server reaches it through the configured SSH reverse forward, not through a public or LAN listener.

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `SPARKY_API_TOKEN` | Required | Bearer token, at least 32 characters |
| `SPARKY_LMSTUDIO_MODEL_ID` | Required | Model identifier configured in LM Studio |
| `SPARKY_SERVICE_URL` | `https://127.0.0.1:5080` | Local HTTPS listener URL; must remain on IPv4 loopback |
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

Generate an ephemeral bearer token for the current PowerShell session, set the model, and start the service:

```powershell
$tokenBytes = [byte[]]::new(32)
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($tokenBytes) } finally { $rng.Dispose() }
$env:SPARKY_API_TOKEN = [Convert]::ToBase64String($tokenBytes)
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

`GET /health` returns `{"status":"ok"}` and does not need authentication. `POST /v1/answer` requires the bearer token and accepts a JSON `question` string plus an optional `contextItem` display name of at most 256 characters. The API validates the hint against public names in the loaded export, uses it only for a clear item follow-up with no explicit item match, and always gives a currently named item precedence. Invalid hints are ignored. The API does not retain context or accept caller identities.

```json
{"status":"answered","item":"Standard medium HCL laser","answer":"Accuracy: 12"}
{"status":"not_found"}
{"status":"ambiguous","candidates":["Standard medium HCL laser","... another match"]}
```

Unauthenticated requests receive 401; malformed input receives 400; requests over a configured limit receive 413; saturated inference capacity receives 429; model failures return a generic 502 or 504. The service does not log question/answer contents, context names, or tokens; does not accept account/character identifiers; and does not retain follow-up context. A not-found or ambiguous lookup does not call LM Studio.

Do not expose this endpoint on a LAN or through router port forwarding. Keep the reverse tunnel, API bearer token, and normal TLS certificate validation enabled for VDM access.
