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

The service executable is also the command-line bundle validator. New builds use schema 3 with localized robot/module facts, validated compatibility references, and laser module categories derived from server category flags. `--validate-bundle` accepts schema 1 only when its robot sections are empty, accepts schema 2 without the laser category for rollback, and validates schema 3 category data before activation. The validator reads the bundle only; it does not need database or repository access. If only the refresh script changed, transfer the updated `tools\Refresh-SparkyKnowledge.ps1` instead of recopying the executable directories.

## 3. Validate and activate on the AI machine

Run an elevated PowerShell session if required to update Program Files or control the service. The script copies the staged bundle to a temporary file beside the active file, runs `--validate-bundle`, stops only `Perpetuum.Sparky`, atomically replaces `C:\ProgramData\Sparky\output.json`, starts the service, and checks `/health` for the expected bundle-version prefix. Health TLS is pinned to the configured public certificate at `C:\ProgramData\Sparky\sparky-api.cer`.

```powershell
& 'D:\Sparky\SparkyItemStatsExporter\tools\Refresh-SparkyKnowledge.ps1' `
  -StagedBundlePath 'D:\Sparky\staging\sparky-knowledge.bundle.json'
```

It defaults to service `Perpetuum.Sparky`, active bundle `C:\ProgramData\Sparky\output.json`, exporter `C:\Program Files\PerpetuumSparky\Perpetuum.ItemStatsExporter.exe`, and health URL `https://127.0.0.1:5080/health`. The health probe pins the configured certificate using a compiled .NET callback so Windows PowerShell's asynchronous request thread does not need to execute a PowerShell script block. Failure diagnostics include the last reported bundle version or request error.

A successful result includes `Status = Activated`, the health bundle version, the validation output, and the retained backup path. Backups are stored in `C:\ProgramData\Sparky\knowledge-backups` and the ten newest are kept by default. A rejected bundle is never activated. A service start or health-version failure triggers rollback to the previous bundle and a service restart.

## 4. Verify and resume local acceptance

On the AI machine, the service health response should report `status: ok` and the first 16 characters of the built bundle version. Confirm the Sparky service is running. Then, on Main, use the loopback/reverse-tunnel health and answer commands already documented in the Item Stats Exporter README. Verify the new bundle answers robot stat, component, chassis-bonus, and module-compatibility questions in the supported languages where the required labels exist; also confirm an unknown robot and an untranslated component do not expose internal names. Try a fastest-robot question, a maximum-speed question naming one robot, a top-speed comparison naming two robots, and a comparison of robot recipes or chassis bonuses; the named questions should not be treated as a global ranking or as an ordinary stat comparison. List laser modules, then ask what a laser module is and ask about ammo or stats for a specific laser item; only the explicit category-list request should return the laser catalog. Verify comparison values are computed from matching stat fields and the laser catalog contains only modules marked by the exported server category. Ask for stats for an item name that is absent from the bundle and confirm the response does not use an unrelated lore passage. Check that a no-evidence answer asks a brief, natural clarification in the caller's language. Existing item, recipe, and lore answers should remain available. The local Visual Studio Debug server remains running throughout bundle refresh.

Do not use this runbook to roll the bundle into VDI or the live server. That deployment remains a separate decision after Main-machine acceptance.

## Manual recovery

The activation script attempts automatic rollback. If it reports rollback failure, inspect its printed backup path. Restore a known-good bundle while the service is stopped, then start and check health:

```powershell
Stop-Service -Name 'Perpetuum.Sparky'
Copy-Item -LiteralPath 'C:\ProgramData\Sparky\knowledge-backups\output-<timestamp>.json' `
  -Destination 'C:\ProgramData\Sparky\output.json' -Force
Start-Service -Name 'Perpetuum.Sparky'
Invoke-RestMethod -Uri 'https://127.0.0.1:5080/health'
```

If Windows PowerShell rejects the self-signed HTTPS certificate for a manual health call, use the pinned refresh-script check or the existing certificate-pinned curl procedure; do not disable TLS validation globally.
