![opp-server2](opp-server2.png)

[![Build Perpetuum.Server Service v2](https://github.com/OpenPerpetuum/PerpetuumServer2/actions/workflows/dotnet.yml/badge.svg?branch=develop)](https://github.com/OpenPerpetuum/PerpetuumServer2/actions/workflows/dotnet.yml)

# The Open Perpetuum Server 2

Local development runs in **Linux containers** (Docker or Podman). That is the setup documented below.

The console host `Perpetuum.Server` and the WPF Admin Tool remain Windows-only. Use them only if you are running the server on a Windows machine without containers — see [Native Windows host](#native-windows-host).

## Local development (containers)

`compose.yml` defines the asset server, SQL Server, migration job, and game server. Configuration lives in `.env.local`.

Two named volumes persist between restarts:

- `openperpetuum-data` — original `PerpetuumServer/data`, custom layers, and a generated `perpetuum.ini`
- `openperpetuum-db` — SQL Server files

`make` wraps the compose commands; you can call `docker compose` / `podman compose` yourself if you prefer.

### Requirements

- Docker or Podman (Linux containers)
- (optional) `make`
- Steam: Perpetuum Dedicated Server installed
- Latest gamma island layers: https://drive.google.com/file/d/1Xp0T1K57Pv-vjgmpXMG8Iea_ec0bWYR4/view?usp=drive_link
- Latest asset resource: https://drive.google.com/file/d/18fh8aRqMP1J7ycGBNGraFyQ31mMXZaq1/view?usp=drive_link

### 1. Clone and submodules

```sh
git clone https://github.com/OpenPerpetuum/PerpetuumServer2.git
# or: git clone git@github.com:OpenPerpetuum/PerpetuumServer2.git
cd PerpetuumServer2
git submodule init && git submodule update
```

Submodules:

- `db` (OPDB) — database migration files per game update
- `asset` (OPResource) — client resources served when a client connects (definitions, translations, gfx, layers, audio, custom bot models)

### 2. Custom resources

Do this whenever the gamma layers or asset pack are updated.

- Uncompress the gamma layers and copy every `.bin` into both:
  - `asset/lang0000/layers/GAMMA_LAYERS_NEW`
  - a new `custom-layers` directory (same files)
- Unarchive the asset resource and copy `gfx`, `sfx`, and `textures` into `asset/lang0000`
- Create `perpetuum-data` and copy the Dedicated Server installer `data` folder into it (`database`, `layers`)

Paths for `perpetuum-data` and `custom-layers` can be changed in `.env.local`.

### 3. Configuration

Edit `.env.local` for ports, the database password, paths, and the SQL connection string.

The migration job writes `perpetuum.ini` from `template/perpetuum.ini.template`. Do not copy the installer `perpetuum.ini` into the data volume — that file was written for `System.Data.SqlClient` and this server uses `Microsoft.Data.SqlClient`. The template already uses a Linux-compatible string: SQL authentication (`sa`), `TrustServerCertificate=True`, no `Trusted_Connection`, and no keywords the driver refuses (`Connection Reset`, `Network Library`, `Context Connection`).

Linux does not support distributed transactions. `.env.local` sets `DISTRIBUTED_TRANSACTIONS=false` for that reason.

`SERVER_PORTS` must be a range of about 300 ports starting at `SERVER_PORT` (default `17700-17900`). A single mapped port is enough to log in; entering a zone then shows a black screen.

### 4. Run the server

```sh
make up
```

This builds and starts the containers and runs migrations. The command returns before the game host is fully up; wait a few minutes.

```sh
make log-server
```

The server is ready for a client when you see lines such as `Unit enter to zone` or `Planthandler STOP SIGNAL received`.

### 5. Point the client at this host

- Open the client → **Server list** → **ADD PRIVATE SERVER**
- Name: `local`
- Address: `127.0.0.1:17700` (use `SERVER_PORT` from `.env.local` if you changed it)
- Connect, then log in with user `test` / password `test`

The first connect can take several minutes while the asset server transfers files.

### 6. Stop

```sh
make down     # stop and remove containers; keep data and db volumes
make delete   # also delete the volumes
```

## Native Windows host

Skip this if you are using the containers above.

`Perpetuum.Server` is annotated `[SupportedOSPlatform("windows")]` and the Admin Tool is WPF. You need the .NET 8 SDK, a SQL Server instance, and the `perpetuumsa` database — see [OPDB](https://github.com/OpenPerpetuum/OPDB) for restore and patches.

### Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

### Configure

The server reads `perpetuum.ini` from the game root. `ConnectionString` must match your machine.

**The `perpetuum.ini` written by the Perpetuum Dedicated Server installer will not start this server.** `Microsoft.Data.SqlClient` differs from `System.Data.SqlClient` in two ways that abort startup before any zone loads:

1. **`Connection Reset` was removed.** The driver refuses the keyword while the connection is being constructed:

   ```
   The keyword 'Connection Reset' is not supported on this platform.
   ```

   The server checks the string before it connects. If anything is refused, it logs `perpetuum.ini`, the directory it is in, and **every** rejected setting, then stops — one restart is enough to clear them all. Delete those keys from the file. `Connection Reset` is safe to drop: a pooled connection is always reset. `Network Library` and `Context Connection` are refused the same way. The check asks the driver; it does not keep its own list of keywords.

2. **`Encrypt` now defaults to `true`.** Since version 4.0 the driver encrypts and validates the server certificate. A local SQL Server with a self-signed certificate fails logon in the SSL provider:

   ```
   A connection was successfully established with the server, but then an error occurred during
   the login process. (provider: SSL Provider, error: 0 - The certificate chain was issued by an
   authority that is not trusted.)
   ```

   The trailing part of that message comes from Windows and appears in the system language, so match on the condition rather than the exact text. The server will not decide for you whether skipping certificate validation is acceptable.

A connection string that works against a local named instance with Windows authentication:

```
Server=localhost\PERPSQL;Database=perpetuumsa;Trusted_Connection=True;TrustServerCertificate=True;Pooling=True;Connection Timeout=30;Connection Lifetime=260;Min Pool Size=20;Max Pool Size=60;
```

`TrustServerCertificate=True` keeps the connection encrypted but skips validating the certificate. **Use that only on a local development instance.** On a deployment that leaves the machine, install a trusted certificate instead. `Encrypt=False` also works locally and is strictly worse: it drops encryption as well.

### Run

```
cd src/Perpetuum.Server
dotnet run -- "C:\PerpetuumServer\data"
```

The server is up when the log reads `>>>> Perpetuum Server State : [Online]`. Ctrl+C shuts it down; a clean shutdown ends at `State : [Off]`.
