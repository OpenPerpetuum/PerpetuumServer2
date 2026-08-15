![opp-server2](opp-server2.png)

[![Build Perpetuum.Server Service v2](https://github.com/OpenPerpetuum/PerpetuumServer2/actions/workflows/dotnet.yml/badge.svg?branch=develop)](https://github.com/OpenPerpetuum/PerpetuumServer2/actions/workflows/dotnet.yml)

# The Open Perpetuum Server 2

## Running a local server

Windows and x64 only. The bootstrapper is annotated `[SupportedOSPlatform("windows")]` and the Admin
Tool is WPF.

You need the .NET 8 SDK, a SQL Server instance, and the `perpetuumsa` database — see
[OPDB](https://github.com/OpenPerpetuum/OPDB) for restoring and patching it.

### Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

### Configure

The server reads `perpetuum.ini` from the game root directory. `ConnectionString` is the only setting
that must match your machine.

**The `perpetuum.ini` written by the Perpetuum Dedicated Server installer will not start this server.**
It was written for the original server, which used `System.Data.SqlClient`; this one uses
`Microsoft.Data.SqlClient`, which differs in two ways that both abort startup before any zone loads:

1. **`Connection Reset` was removed.** `Microsoft.Data.SqlClient` refuses the keyword while the
   connection is being constructed:

   ```
   The keyword 'Connection Reset' is not supported on this platform.
   ```

   The server drops this keyword for you and logs a warning naming the file, because the framework
   had already stopped honouring it — a pooled connection is always reset. Deleting it from
   `perpetuum.ini` silences the warning.

2. **`Encrypt` now defaults to `true`.** Since version 4.0 the driver encrypts by default and
   validates the server certificate. A local SQL Server using a self-signed certificate fails logon
   in the SSL provider:

   ```
   A connection was successfully established with the server, but then an error occurred during
   the login process. (provider: SSL Provider, error: 0 - The certificate chain was issued by an
   authority that is not trusted.)
   ```

   The trailing part of that message comes from Windows and appears in the system language, so match
   on the condition rather than the exact text. **This one you must fix yourself** — the server
   cannot decide for you whether skipping certificate validation is acceptable.

A connection string that works against a local named instance with Windows authentication:

```
Server=localhost\PERPSQL;Database=perpetuumsa;Trusted_Connection=True;TrustServerCertificate=True;Pooling=True;Connection Timeout=30;Connection Lifetime=260;Min Pool Size=20;Max Pool Size=60;
```

`TrustServerCertificate=True` keeps the connection encrypted but skips validating the certificate.
**It is appropriate for a local development instance only.** Do not carry it into a deployment where
the connection leaves the machine — install a trusted certificate there instead. Setting
`Encrypt=False` also works locally and is strictly worse: it drops the encryption as well.

### Run

```
cd src/Perpetuum.Server
dotnet run -- "C:\PerpetuumServer\data"
```

The server is up when the log reads `>>>> Perpetuum Server State : [Online]`. Ctrl+C shuts it down;
a clean shutdown ends at `State : [Off]`.

