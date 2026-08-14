# AdminTool Linux migration

The AdminTool Linux migration is deliberately incremental. The existing WPF
application remains supported while platform-neutral behavior moves into a
plain .NET library and a native Avalonia desktop application consumes that
same core.

## Project layout

- `Perpetuum.AdminTool` — existing Windows WPF application;
- `Perpetuum.AdminTool.Core` — platform-neutral settings, authentication, and
  database primitives, with reusable editing behavior moving here module by
  module;
- `Perpetuum.AdminTool.Core.Tests` — framework-independent unit and database
  contract tests;
- `Perpetuum.AdminTool.Avalonia` — cross-platform desktop application.

The portable core must not reference WPF, Avalonia, or the platform-specific
game-server assembly. UI projects own dialogs, clipboard access, file pickers,
window lifetime, and presentation-only collection views.

## Current native milestone

The Avalonia application provides connection settings, a live database probe,
normal AdminTool account authentication, and a tabbed native workspace. It
includes shared pending-change review, transactional SQL preview/export, and a
guarded direct-apply workflow. The development-focused tabs cover entities,
structured robot templates, definition-to-template relations, equipment sets,
NPC loot, presences, flocks, and presence-to-flock relationships. The NIC-flow
economy dashboard and the custom-dictionary translation editor are also native.
Template descriptions support both raw Genxy
and the structured robot/part/module editor, including slot and ammo
compatibility filtering. Entity aggregate stats are editable, and native
entity creation/deletion includes category and attribute flag pickers plus tier
fields. This establishes the complete Linux path from desktop UI through
`Microsoft.Data.SqlClient` to live Perpetuum data for bot and content work.

Run from a machine with the .NET 8 SDK:

```bash
dotnet run --project src/Perpetuum.AdminTool.Avalonia
```

Or make a self-contained build that does not require .NET on the destination:

```bash
scripts/publish-admin-tool.sh linux-x64
./artifacts/admin-tool-linux-x64/Perpetuum.AdminTool.Avalonia
```

The publish script infers `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`
when its runtime argument is omitted. CI publishes a ready-to-run Linux x64
artifact for every workflow run.

For SQL Server in a local container, publish its port only on loopback and use
`127.0.0.1,<port>` as the server. Disable Integrated Security and use a SQL
login scoped to the `perpetuumsa` database. Do not expose SQL Server directly
to a public or untrusted network.

Create a dedicated login from an existing SQL administrator session. Replace
the example password and do not commit it:

```sql
USE [master];
CREATE LOGIN [perpetuum_admin_tool]
    WITH PASSWORD = 'replace-with-a-long-random-password';

USE [perpetuumsa];
CREATE USER [perpetuum_admin_tool] FOR LOGIN [perpetuum_admin_tool];
ALTER ROLE [db_owner] ADD MEMBER [perpetuum_admin_tool];
```

`db_owner` is intentionally database-scoped. The complete AdminTool can read
and edit content tables and execute maintenance procedures, so a read-only
login is not sufficient. Use a separate, disposable database for development
and take a backup before direct changes.

Settings are shared with the WPF application at the platform's application-data
location under `PerpetuumAdminTool/settings.json`. On Unix the file is written
with mode `0600` because it can contain a SQL credential.

The native application defaults to `127.0.0.1,1433` with SQL authentication on
Linux and macOS. The Windows application retains its named-instance and
Integrated Security defaults.

Set **Game root** to the directory that contains the client's
`customDictionary` folder before using the Translations tab. Translation edits
are saved directly as atomically replaced UTF-8 JSON files and do not enter the
SQL pending-change queue.

## Verification

The normal test suite uses no database and includes a headless UI test that
loads the compiled Avalonia XAML. To exercise the real `SqlClient` path against
a disposable or local Perpetuum database, set these variables before running
the core tests:

```bash
export PERPETUUM_ADMINTOOL_TEST_SERVER=127.0.0.1,1433
export PERPETUUM_ADMINTOOL_TEST_USER=perpetuum_admin_tool
export PERPETUUM_ADMINTOOL_TEST_PASSWORD='replace-me'
dotnet test src/Perpetuum.AdminTool.Core.Tests
```

The database integration test is skipped when those variables are absent.

### Human smoke test

Automated tests cover compiled XAML, authentication state, live read queries,
generated SQL, transaction handling, and the Linux publish. Before relying on a
new build for content work, a person should perform the parts that cannot be
meaningfully judged headlessly:

1. launch the self-contained executable and confirm the window, text, and all
   nine module tabs render at the workstation's actual DPI;
2. sign in and reload each development tab, checking that long names and the
   structured template editor remain usable at the chosen window size;
3. edit one loaded row, queue it, review the SQL in **Pending changes**, then
   clear the queue and confirm the editor still shows the database baseline;
4. create one unsaved row, queue its insert, inspect the SQL, and clear it
   without applying; and
5. on a disposable database or after a backup, apply one reviewed change and
   reload to confirm the end-to-end write path.

## Native feature sequence

The port is organized as tabbed modules so every stage remains usable:

1. connection, secure local settings, database probe, and admin authentication;
2. pending-change review, SQL-script export, and explicit commit safeguards
   (native now);
3. entities and robot templates, which support content and bot development
   (entity primitive fields, structured robot templates, and template relations
   are native now);
4. NPC loot, presences, flocks, and equipment sets (all four are native now,
   including presence-to-flock relationships);
5. AutoMarket and the remaining economy diagnostics (NIC flow is native now);
6. seasons, packages, and remaining creation dialogs (translations are native
   now).

The WPF application remains the compatibility implementation until this list
reaches parity. New database behavior belongs in the portable core and is
consumed by both front ends rather than being duplicated.

### Change-application safeguards

- SQL previews and exports wrap all queued statements in one transaction with
  `XACT_ABORT` enabled.
- Generated comments cannot be escaped with newline characters from account or
  content text.
- Export creates a unique UTF-8 file atomically and never overwrites an existing
  script.
- Direct application remains disabled until the administrator reviews the SQL
  and types `APPLY`; a queue containing a destructive operation instead requires
  `APPLY DELETE`.
- A failed export or database operation preserves the queue for review, retry,
  or recovery through script export.

## Wine compatibility fallback

The existing WPF application still builds as a self-contained Windows x64
application and can be used with Wine while modules are migrating:

```bash
dotnet publish src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj \
  --configuration Release \
  -p:EnableWindowsTargeting=true \
  -p:Platform=x64

wine src/Perpetuum.AdminTool/bin/x64/Release/net8.0-windows/win-x64/Perpetuum.AdminTool.exe
```

Wine is a compatibility path rather than the target architecture. Native
features and automated UI coverage live in the Avalonia project.

## Migration rules

1. Preserve a buildable WPF application until native feature parity is reached.
2. Move reusable behavior into the core before porting its screen.
3. Keep view models independent from concrete windows and controls.
4. Add tests for every extracted behavior and SQL mutation path.
5. Default destructive changes to SQL-script generation, not direct execution.
6. Keep database and game runtime artifacts outside the repository.
7. Require both Linux-native and Windows compatibility checks on every change.
