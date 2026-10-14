# Sparky knowledge bundle refresh

This runbook refreshes the offline knowledge used by `Perpetuum.Sparky` on the AI machine. It does not connect to the live database, restart the game server, or change the reverse SSH or HTTPS credentials.

## 1. Build on Main

Use the Main development machine, its local Perpetuum database copy, the checked-out server repository, and the custom dictionary directory. The local game server may remain open in Visual Studio Debug; the exporter initializes only offline content readers.

```powershell
$gameRoot = 'G:\PerpetuumServer\data'
$dictionaryDirectory = 'E:\MyStuff\Projects\OPDB\Patches\Live_36\Server\data\customDictionary'
$sourceRoot = 'E:\MyStuff\Projects\PerpetuumServer2'
$loreRoot = 'E:\Perpetuum-lore'
$bundlePath = 'E:\MyStuff\Projects\PerpetuumServer2\sparky-knowledge.bundle.json'

dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --build-knowledge $gameRoot $dictionaryDirectory $sourceRoot $loreRoot $bundlePath
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --validate-bundle $bundlePath
Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256
```

The build requires `customDictionary\0.json`, rejects unsupported language IDs and malformed dictionary files, and reports the bundle version and included language/item/recipe/passage/robot/module/skipped-robot counts. Review that output before transfer. Robot records are created from valid `robottemplaterelation` rows by assembling and initializing the requested server robot entity. The assembled baseline includes the server's component and equipment-set calculations. Conditional chassis extension results are separate level-1 facts; static module eligibility comes from server slot and allowed-robot rules and does not promise that a player fit has free slots or sufficient total CPU/power grid. Records without required relations or translated names are omitted, as are individual labels without dictionary translations. The exporter does not include account, character, or current loadout data and does not use model-generated calculations for these facts.

The curated source allowlist is `tools\sparky-knowledge-sources.json`; it currently names two mechanics summaries and eleven lore text files under `$loreRoot`. The builder does not enumerate the lore folder. It includes only those passages and source hashes in the bundle; no absolute path is written to the bundle. Do not add database tables, repository trees, or new source files without reviewing them for player-visible content and private/operational data.

Tier wording is maintained in `tools\sparky-entity-resolution-rules.json`. Each rule pairs an entity kind with the exact nullable `tiertype`/`tierlevel` values and lists accepted aliases under `aliasesByLanguage`; language `0` is shared English, and localized aliases should be added only when supported by reviewed wording. Item and robot tiers are separate. Calibration-template aliases are in the same file, and only a canonical definition/dictionary key ending exactly in `_cprg` qualifies as a template. Tier shorthand selects a record before its facts are formatted; an unqualified name can remain ambiguous, and a tier label without a base name never selects a record.

The bundle manifest records the catalog hash as `SourceHashes.sparkyEntityResolutionRules`, and that hash contributes to the bundle version. Alias-only edits therefore use the normal build, validation, transfer, stage, and profile activation workflow below; they do not require another executable deployment once schema 4 support is installed. New parsing behavior still requires an executable update.

## 2. Transfer and update the AI-machine executable

Transfer the validated bundle and its SHA-256 value to the AI machine through the already-approved private file-transfer method. On the AI machine, compare its hash with the Main-machine hash:

```powershell
Get-FileHash -LiteralPath 'D:\Sparky\staging\sparky-knowledge.bundle.json' -Algorithm SHA256
```

The hashes must match exactly. Do not transfer the service settings file, API token, PFX, certificate private key, or SSH private key.

When exporter/service code changed, publish once on Main and copy the same published output to both AI-machine installations: `D:\Sparky\SparkyItemStatsExporter` for command-line use and `C:\Program Files\PerpetuumSparky` for the Windows service. The refresh script is included in published output as `tools\Refresh-SparkyKnowledge.ps1`. The AI machine needs no .NET installation when the self-contained publish is used:

```powershell
$publishPath = Join-Path $sourceRoot ("publish\SparkyItemStatsExporter-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))

dotnet publish src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj `
  -c Release -r win-x64 --self-contained true -p:Platform=x64 `
  -o $publishPath
```

Use a fresh timestamped publish directory for each build; reusing an older directory can leave stale assemblies beside a newer dependency manifest. Validate the published executable before transfer:

```powershell
& "$publishPath\Perpetuum.ItemStatsExporter.exe" --validate-bundle $bundlePath
```

The service executable is also the command-line bundle validator. New builds use schema 4 with localized robot/module facts, exact nullable tier metadata for items, recipes, robots, and modules, and the validated entity-resolution rules. The one-time schema 4 rollout requires updating both AI-machine executable installations before activating a schema 4 bundle. `--validate-bundle` continues to accept schema 1 only when its robot sections are empty, schema 2 without the laser category, and schema 3 without tier metadata for rollback; schema 4 requires complete tier metadata and valid resolution rules. Schemas 1–3 remain name-only at runtime. The validator reads the bundle only; it does not need database or repository access. If only the refresh script changed, transfer the updated `tools\Refresh-SparkyKnowledge.ps1` instead of recopying the executable directories.

## 3. Validate and stage on the AI machine

The refresh script copies the bundle to a temporary file in the protected immutable bundle catalog, runs `--validate-bundle`, and stages it under its bundle version. It does not replace the legacy `output.json`, stop or restart the service, or activate the bundle. The currently active profile bundle remains available while staging occurs.

```powershell
& 'D:\Sparky\SparkyItemStatsExporter\tools\Refresh-SparkyKnowledge.ps1' `
  -StagedBundlePath 'D:\Sparky\staging\sparky-knowledge.bundle.json'
```

The script defaults to the installed exporter `C:\Program Files\PerpetuumSparky\Perpetuum.ItemStatsExporter.exe` and protected catalog `C:\ProgramData\Sparky\bundles`. A successful result includes `Status = Staged`, bundle version, validation output, destination path, and a reminder to select and activate it from the panel. If that version already exists, identical file content is accepted; a different file under the same immutable version is rejected.

Open the Sparky Admin Panel on AI at `https://127.0.0.1:5081/sparky-admin/` or on Main through the dedicated tunnel at `https://127.0.0.1:5082/sparky-admin/`. Select the intended profile, choose the staged version in **Knowledge bundle**, save the draft, validate it, and activate it. Activation swaps one immutable in-memory profile snapshot atomically; in-flight requests continue with the snapshot they already captured. The panel keeps revision history and can roll back by activating the previous configuration as a new revision. Promoting a Dev revision to Live is a separate explicit action.

## 4. Verify and resume local acceptance

After activation, `/health` should report the active Dev bundle version prefix without any service restart. Use the Main Dev API tunnel for the same item, recipe, robot, and lore acceptance questions listed above. For schema 4, also try `T1 medium plate`, `T2+ Scorcher`, `T4 prototype sensor amplifier`, a `_cprg` calibration template, `Mk2 Arbalest`, `Mk3 Weasel`, an unqualified unique robot name, and a prototype robot. Confirm a tier-qualified request selects the exact record, an unqualified collision asks for clarification, and an unsupported tier does not fall back to another tier's facts. Confirm the intended profile's panel summary shows the new bundle/revision while Live remains unchanged. The local Visual Studio Debug server remains running throughout bundle refresh and activation.

Do not use this runbook to roll the bundle into VDI or the live server. That deployment remains a separate decision after Main-machine acceptance.

## Manual recovery

To roll back a profile, select it in the panel and use **Rollback profile**. This creates and activates a new revision from the prior activated configuration, including its bundle version. The game server and Sparky service do not need to restart. If the panel or profile database is unavailable, restore trace-store access before changing `output.json`; that file is only the legacy seed used when no Dev profile revision exists.

If normal TLS validation fails on a manual health call, use the pinned certificate procedure in the service operations documentation; do not disable TLS validation globally.
