# External Integrations

**Analysis Date:** 2026-05-11

## Databases

**Microsoft SQL Server:**
- The sole data store for all game state, accounts, entities, missions, market, production, standings, characters, corporations, seasons, and more
- Connection string stored in `perpetuum.ini` (GameRoot directory), surfaced via `GlobalConfiguration.ConnectionString`
- Client: `Microsoft.Data.SqlClient` 6.0.1
- Access layer: `src/Perpetuum/Data/DbQuery.cs` (fluent query builder), `src/Perpetuum/Data/Db.cs` (static factory), `src/Perpetuum/Data/Database.cs` (table cache utilities)
- Auto-detects inline SQL vs stored procedures by presence of spaces in command text
- Transactions: `System.Transactions.TransactionScope` with `ReadCommitted` isolation; distributed transactions enabled at startup in `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`
- Schema documentation: `docs/db_structure/` — authoritative source; includes tables, stored procedures, views, functions, data types
- Admin tool (`src/Perpetuum.AdminTool/`) connects directly using its own `Microsoft.Data.SqlClient` reference

## Network Protocols

**Custom game protocol (GenXY / GenxyString):**
- Binary wire format defined in `src/Perpetuum/GenXY/`
- `GenxyReader` / `GenxyWriter` for serialization; `GenxyConverter` for registering custom type converters
- All client ↔ server game traffic uses this protocol
- Type converters registered at startup (e.g., `Character` → `int` id)

**Encrypted TCP:**
- `src/Perpetuum/Network/EncryptedTcpConnection.cs` — XOR stream cipher with rolling key (fixed seed bytes `0xCA`/`0xAC`)
- `src/Perpetuum/Network/TcpConnection.cs` — base TCP framing
- `src/Perpetuum/Network/ClientConnection.cs` — client session wrapper
- Game listens on port defined by `GlobalConfiguration.ListenerPort`; each zone has its own additional listener port (configured in `perpetuum.ini`)

**HTTP (outbound):**
- `src/Perpetuum/Network/Http.cs` — simple `WebClient.UploadValues` POST helper (`User-Agent: PerpetuumServer/1.0`)
- Used for webhook and external notification calls (deprecated `WebClient` API; suppresses `SYSLIB0014` warning)
- Discord webhook integration: `GlobalConfiguration.WebHookId` and `GlobalConfiguration.WebHookOAuth`

**Cryptography:**
- RSA: custom implementation at `src/Perpetuum/Rsa.cs` — hardcoded modulus key bytes; used for client authentication handshake
- RC4: custom stream cipher at `src/Perpetuum/Rc4.cs` — used for session key exchange

## External Services

**Steam (Valve):**
- Native DLL interop: `sdkencryptedappticket64.dll` (Steamworks SDK, x64)
- Used to authenticate players via encrypted app tickets
- Implementation: `src/Perpetuum/Services/Steam/SteamHelper.cs`
- Config: `GlobalConfiguration.SteamAppID` (int) and `GlobalConfiguration.SteamKey` (byte[])
- The DLL must be present in the working directory; P/Invoke calls via `[DllImport]`

**Discord:**
- Library: `Discord.Net` 3.17.4 (`DiscordSocketClient`)
- Gateway intents: `AllUnprivileged | MessageContent`
- Bidirectional bridge between in-game chat channels and Discord channels
- Perpetuum → Discord: messages forwarded in `src/Perpetuum/Services/EventServices/EventListenerService.cs`
- Discord → Perpetuum: handled by `src/Perpetuum/Services/EventServices/EventProcessors/DiscordIntegrationHandler.cs`
- Channel mapping stored in DB via `IChannelManager.GetChannelNameByDiscordId()`
- Config: `GlobalConfiguration.DiscordBotToken`, `GlobalConfiguration.OpHelpChannelId`, `GlobalConfiguration.WebHookId`, `GlobalConfiguration.WebHookOAuth`
- Integration is optional; bot token left empty disables it

**Sparky local item-answer service:**
- The API runs on the separate AI PC and uses an offline export; it does not query the live game database.
- The game server reaches `https://127.0.0.1:5080/v1/answer` through the established reverse SSH tunnel. The API remains bound to loopback and validates a bearer token in addition to SSH/TLS.
- `GlobalConfiguration.EnableSparky` defaults to `false`. Other `Sparky*` settings in `perpetuum.ini` tune the loopback URL, request timeout, answer/message caps, bounded queue, worker count, per-caller pending limit, context lifetime/capacity, and sender character.
- The bearer token is stored separately under `%ProgramData%\Sparky\vdm-client-settings.json`, encrypted with machine-scope DPAPI, and ACL-restricted to the explicitly configured game-server service identity, SYSTEM, and local Administrators.
- Only the question and an optional public item display name cross the API boundary. Game/Discord identities, channel IDs, account data, session state, and live database content remain local to the game server.
- Game answers bypass Discord mirroring; Discord answers return only to the source channel and disable mentions. Offline in-game callers receive character mail.
- Implementation and operator steps: `src/Perpetuum/Services/Sparky/`, `src/Perpetuum.ItemStatsExporter/`, and `docs/superpowers/sparky-vdm-client-setup.md`.

**UPnP / NAT Traversal:**
- Library: `SharpOpenNat` 4.0.17
- Attempts to map game and zone TCP ports on router via UPnP protocol
- Controlled by `GlobalConfiguration.EnableUpnp` (opt-in)
- Wired in `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` (`TryInitUpnp`)

## File System

**GameRoot directory** (path from `appsettings.json` → `GlobalConfiguration.GameRoot`):
- `perpetuum.ini` — main server config: SQL connection string, ports, zone config, feature flags (stored as `GlobalConfiguration` JSON)
- `Sparky*` entries in `perpetuum.ini` contain only non-secret game-side settings; Sparky remains disabled unless `enableSparky` is explicitly true
- Zone terrain data and map files — loaded by `src/Perpetuum/Zones/Terrains/`
- Robot templates and definition data — referenced at startup via `EntityDefault` / `IEntityDefaultReader`
- `src/Perpetuum/IO/IFileSystem.cs` — abstraction over file I/O; implementations at `src/Perpetuum/IO/FileSystem.cs`

**Log files:**
- Custom file logger: `src/Perpetuum/Log/Loggers/FileLogger.cs`
- Composite logger: `src/Perpetuum/Log/Loggers/CompositeLogger.cs` — fan-out to multiple targets (file, console, buffered)
- Channel chat logs: `src/Perpetuum/Services/Channels/ChannelLogger.cs`
- Corporation action logs: `src/Perpetuum/Common/Loggers/` (transaction logger)

**Sparky protected settings:**
- `%ProgramData%\Sparky\vdm-client-settings.json` — machine-scope DPAPI-protected API token; the provisioning script applies explicit service-account, SYSTEM, and Administrators ACLs
- Never copy the AI PC private key or TLS private key to the game server; the operator setup guide requires confirming the real service identity before provisioning

**Admin Tool data files** (`src/Perpetuum.ServerService2/`):
- `admincreds.json` — admin credentials for the WPF Admin Tool (local only, not committed to production)
- `localserverinfo.json` — local server metadata

**Build output:**
- `bin/x64/Release/net8.0/` — all compiled assemblies and native DLLs (including `sdkencryptedappticket64.dll`)

---

*Integration audit: 2026-05-11*
