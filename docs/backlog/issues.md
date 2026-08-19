# Last ID used

041

## ISSUE-041 - Characters stay online after the player logs out and closes the client ("zombie sessions")

Status: IN_PROGRESS
Priority: MEDIUM
Area: Networking / Sessions
Tracking: https://github.com/OpenPerpetuum/PerpetuumServer2/issues/51

### Problem
Players are sometimes shown as online after they have logged out and closed the game client. Reported
from live server experience; previous attempts were made to address it and none is confirmed to have
fixed it. Not reproduced in a development environment.

A mechanism that produces exactly this symptom is already documented inside [[ISSUE-038]], which found
it while investigating memory growth:

- `TcpConnection` (`src/Perpetuum/Network/TcpConnection.cs:32`) sets the OS-level TCP keepalive, and
  that keepalive is the only backstop for a peer that disappears without a clean TCP close.
- There is no application-level heartbeat or idle timeout. `ZoneSession` tracks
  `_lastReceivedPacketTime` / `InactiveTime`, but it is read in exactly one place — `Player.cs:1305`,
  for the "was this player AFK at time of death" loot calculation — and never compared against a
  threshold to force a disconnect.
- Until the keepalive fires, `SessionManager` keeps the entry, `Character.IsOnline` stays `true`, and
  the ghost's `Player` stays `InZone` and is still processed by the zone tick every frame.

[[ISSUE-038]] shipped a mitigation for that path — keepalive time reduced from 24 hours to 2 hours —
and explicitly deferred the robust fix, recording an application-level idle timeout as "Deliberately
not done ... user was offered this as an option and did not request it. Worth reconsidering if 2h
keepalive still isn't tight enough after real-world observation." **That observation has now been
reported, so the deferral is due for review.** The 2-hour value is confirmed shipped and live in
`TcpConnection.cs:32`.

**The detail that did not fit is now explained, and it points at a second, independent mechanism.**
The keepalive above covers only *ungraceful* disconnects, while the report describes a *graceful* exit.
Investigated 2026-08-17: there is a single teardown path, and it is fragile in a way that produces this
exact symptom on **either** kind of exit.

`Session.Disconnect(safeLogout)` calls `ForceQuit`, which ends in `_connection.Disconnect()`
(`src/Perpetuum/Network/TcpConnection.cs:59`) — the same call a dropped socket makes. Nothing below
that point distinguishes a clean logout from a lost connection:

1. `TcpConnection.Disconnect()` runs the whole teardown as
   `Task.Run(OnDisconnected).ContinueWith(t => Dispose()).LogExceptions()` (`TcpConnection.cs:68`).
2. `Session.OnDisconnected` (`Session.cs:300`) wraps `SignOut()` in a transaction, completes it, and
   **then** raises `Disconnected` on the next line (`Session.cs:308`), outside the transaction.
3. `SessionManager` subscribes twice and in this order: `OnSessionDisconnected` at
   `SessionManager.cs:67`, then `Remove` at `SessionManager.cs:124` by way of `Add`.

So one exception thrown anywhere inside `SignOut()` leaves the server in exactly the reported state:

- The transaction rolls back. `Character.IsOnline` is the `characters.inuse` column
  (`Character.cs:195`), so the `inuse = 0` written by `DeselectCharacter` is undone and the character
  stays online.
- `Session.cs:308` is never reached, so `SessionManager.Remove` never runs, the entry stays in
  `_sessions`, and the `Player` stays in the zone tick.
- `LogExceptions` (`TaskExtensions.cs:9`) logs the exception and swallows it. Nothing else reports it.

A second variant needs no rollback at all: `Disconnected` is a plain multicast invoke with no
per-subscriber guard, so if `OnSessionDisconnected` throws, `Remove` — subscribed after it — never runs.

This also fits the report saying *sometimes* rather than *always*: `SignIn` clears the flags for the
account on the way in, so a ghost heals itself the next time that player signs in. That statement now
lives in `StaleOnlineFlags.ClearForAccount` and carries `and inuse=1`, which leaves the data identical
and makes the rows affected mean something: without the predicate the update matches every character
on the account and reports that count on every sign in, stale or not.

**What is not established is what throws.** The remaining candidates are the three `Character` database
writes in `DeselectCharacter` and the `ThrowIfNull(AccountNotFound)` against the account repository.
That cannot be derived from the code and needs a live server log.

**The log settles it cheaply, because the teardown leaves a marker.** `[Relay] client disconnected.`
(`SessionManager.cs:99`) is written by a `Disconnected` subscriber, so it can only appear after
`Session.cs:308` has run. A normal disconnect logs it; a ghost produced by this path logs an exception
and no such line. `Character deselected /M\` (`Session.cs:289`) is the same kind of marker, since it
runs from the transaction's commit callback.

### Impact
- Players appear online when they are not. This is visible to everyone and misinforms corporation
  coordination and PvP decisions.
- Every surviving ghost holds its `Player` in the zone and keeps it inside the `ProcessManager` tick
  loop, so it costs CPU as well as memory for as long as it lives. This is the mechanism [[ISSUE-038]]
  identified as a likely contributor to the reported memory growth.
- Anything gated on online state acts on stale information for the lifetime of the ghost.

### Proposed Fix
1. **Instrumented 2026-08-19 instead of asked.** The question was whether a ghost came from a sign out
   that rolled back or from a peer that vanished without closing, and the log could not tell them
   apart: the sign in handler wrote `a logged in account was found` for both. It now writes which one
   it is — `[Ghost] stale login: live session still held` with the connection's silence when the
   server is still holding the session, and `[Ghost] stale login: no live session, the account flag was
   left set` when the flag outlived it. The first is the missing idle timeout, the second is the
   rolled-back sign out. Also shipped: `[Session] closing.` carrying session, account, character,
   endpoint and silence, written before sign out clears the identity; a count of the stale flags each
   sign in clears (`StaleOnlineFlags`); and `StaleOnlineFlagCensus`, which reports every five minutes
   and at startup how many characters are flagged online with no session behind them. Nothing here
   changes behaviour. **Read the live log after the next patch deploy and the mechanism is named.**
2. **DONE 2026-08-17.** The teardown no longer depends on `SignOut()` succeeding.
   `Session.OnDisconnected` raises `Disconnected` from a `finally`, so the session leaves `_sessions`
   either way, and `SessionManager.OnSessionDisconnected` contains its own failure so it cannot cancel
   the `Remove` subscribed after it. The exception is still allowed to leave `Session.OnDisconnected`,
   because what throws is step 1's question and swallowing it would make that question harder to
   answer. **This closes the leak, not the visible symptom** — see the half still open below.
3. **Half done 2026-08-17.** The idle timeout is not implemented, because its threshold cannot be
   chosen here: the client's zero-length keepalive packets arrive as data, but their interval is a
   client decision and a threshold set below it disconnects players who are still connected. What
   shipped is the measurement — `ConnectionActivity` records when data last arrived and the widest gap
   between two receives, `TcpConnection` touches it on every receive, and both numbers are logged when
   a connection closes. **Read those numbers off a live server, then set the threshold and enable the
   disconnect.** Note the timeout belongs on the relay connection rather than on `ZoneSession`, whose
   `InactiveTime` only covers players who are in a zone.
4. Cover the teardown at the unit tier with a fake session — a `SignOut()` that throws must still leave
   the session removed and the character offline — and the surviving-state question at the integration
   tier. **Not done, and it needs a decision first:** `Session` takes a raw `Socket` in its constructor
   and `SessionManager.Add` is private and reachable only through a real `TcpListener` accept, so
   neither can be exercised at the unit tier without a production seam. The step 2 change is covered by
   inspection only. `ConnectionActivity` was built as a separate unit precisely so the part that could
   be tested, was — eleven tests, written first and observed failing.

### A live report, 2026-08-19

The operator lost their connection to the live server when their internet dropped, and on signing in
again was told the character was already logged in and that continuing would disconnect the old
session. That is the `account.IsLoggedIn` branch of `SignInRequestHandler`, and it is the **keepalive**
half of this issue rather than the rollback half: a dropped link sends no close, so the server went on
holding a session whose peer was gone. It is also the case the shipped measurement was built for — the
silence on that session is exactly what `ConnectionActivity` records.

It cannot be attributed with certainty, because the log of the day could not distinguish the two
mechanisms. That is what the step 1 instrumentation fixes, and the next occurrence will say which it
was.

### The half still open

Step 2 stops the session leaking and stops the `Player` being ticked forever, but it does **not** put
the character offline when the transaction rolls back. `Character.IsOnline` is a database write
(`Character.cs:195`), so a rolled-back `SignOut()` leaves `characters.inuse = 1` and the player still
shows as online until they next sign in. Fixing that means a compensating write outside the failed
transaction, in a catch path — a design decision with its own risks, and one that should not be made
while the thing being compensated for is still unnamed. It waits on step 1.

### Notes
- Priority is a judgement made when filing; the report did not assign one. Held at MEDIUM because a
  partial mitigation already shipped and because the mechanism found on 2026-08-17 is so far a reading
  of the code, not an observation of the live server. **Raise it as soon as a log confirms that
  mechanism**, since it would mean an ordinary logout can leave a ghost. Status stays TODO rather than
  BLOCKED: only step 1 waits on the maintainers, and step 3 can proceed without them.
- Filed separately from [[ISSUE-038]] rather than folded into it: that issue is about memory growth and
  would close on a memory measurement, while the visible-online symptom is what players actually
  report and needs to survive that issue closing.
- Named alongside [[ISSUE-040]] as a known trouble area to investigate, fix and cover by tests.
- Found while tracing the above and unrelated to the symptom, so recorded here rather than filed on its
  own: the `.ThrowIfZero(ErrorCodes.SQLExecutionError)` guards on `accountonlinetimestart` and
  `accountonlinetimestop` (`Session.cs:218` and `Session.cs:263`) can never fire. Both procedures begin
  with `SET NOCOUNT ON`, so `ExecuteNonQuery()` returns `-1` rather than a row count, and `ThrowIfZero`
  compares against `0` (`Guard.cs:12`). It was ruled out as the trigger for this issue for that reason.
  Worth a maintainer's decision rather than an unprompted fix, since making the guard live would turn a
  currently silent no-op into a thrown exception.
- Every line number above was checked against `1e68c4a` and is anchored to it. The files cited are
  byte-identical between `4e6d697` and `1e68c4a`, so the earlier anchors still resolve. Line numbers
  drift.

---

## ISSUE-040 - Assembled robots in corporation hangars at PBS bases are altered by a server restart ("Peanut Plague")

Status: TODO
Priority: HIGH
Area: Corporations / Containers / PBS
Tracking: https://github.com/OpenPerpetuum/PerpetuumServer2/issues/50

### Problem
Storing **assembled** (non-repackaged) robots in a corporation hangar hosted on a PBS docking base
leaves them in a wrong state after a server restart. The failure is known internally as the "Peanut
Plague". Reported from live server experience; previous attempts were made to address it and none is
confirmed to have fixed it. Not reproduced in a development environment.

**No reproduction steps, affected-robot list, or exact post-restart symptom have been recorded.** The
whole of the current description is that a restart "makes funny things with them", so establishing what
state the robots actually end up in is the first task, not the fix.

Where an investigation would start:

- `PublicCorporationHangarStorage` (`src/Perpetuum/Containers/PublicCorporationHangarStorage.cs:13`) is,
  in its own words, "the parent of every corporate hangar, one per base". On a PBS base the entire
  hangar tree therefore hangs off an entity owned by that base.
- `PBSDockingBase` (`src/Perpetuum/Zones/PBS/DockingBases/PBSDockingBase.cs:27`) and
  `ExpiringPBSDockingBase` (`src/Perpetuum/Zones/PBS/DockingBases/ExpiringPBSDockingBase.cs`), which
  calls `Kill()` when its lifetime runs out. What happens to the hangar subtree, and to assembled
  robots inside it, when the parent base expires or is killed is worth establishing early — a PBS base
  is not permanent, and an ordinary docking base is.
- `Robot.IsStackable` (`src/Perpetuum/Robots/Robot.cs:87`) is `base.IsStackable && IsRepackaged`. An
  assembled robot is an entity subtree — components, modules and its own `RobotInventory` — while a
  repackaged one is a plain stackable item. That difference is the reason the assembled case is the one
  that breaks, and it is what makes a restart, which rebuilds every entity from the database, the point
  where it surfaces.
- `Container.cs:394` already special-cases `CorporateHangar` and `CorporateHangarFolder` during item
  movement, so the hangar types are known to need their own handling elsewhere.

### Impact
Player property, without any player action, repeating on every restart. Robots are among the most
expensive assets a corporation owns and corporation hangars at player-built bases are where they are
kept. Whatever the concrete symptom turns out to be, the affected items were paid for.

### Proposed Fix
Investigation first, fix second:

1. Reproduce locally — place an assembled robot in a corporation hangar on a PBS base, record the full
   entity subtree from the database, restart the server, and diff the subtree.
2. Derive the mechanism from that diff rather than from a hypothesis.
3. Cover it at the integration tier once the mechanism is known. This is the shape of defect that tier
   exists for: it is about what survives a real load from a real database, which no faked data layer
   can answer.

### Notes
- Priority is a judgement made when filing; the report did not assign one. HIGH because it destroys or
  alters player assets and recurs, against a report that carries no measured frequency.
- Named alongside [[ISSUE-041]] as a known trouble area to investigate, fix and cover by tests.
- Every line number above was checked against `4e6d697` and is anchored to it. Line numbers drift.

---

## ISSUE-039 - Insurance price cache never reloads — LoadInsurancePrices runs inside a completed TransactionScope

Status: IN_PROGRESS
Priority: HIGH
Area: Economy / Insurance

### Problem
`InsurancePriceRefreshService.Refresh()` opens a `TransactionScope` with a `using` declaration, calls `scope.Complete()`, and then calls `InsuranceHelper.LoadInsurancePrices()` while still inside the scope's lifetime (`src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs:49-52` at `f9ddac2`):

```csharp
using var scope = Db.CreateTransaction();
_ = Db.Query().CommandText("exec usp_RecalculateInsurancePrices").Timeout(120).ExecuteNonQuery();
scope.Complete();
InsuranceHelper.LoadInsurancePrices();
```

A `using` declaration disposes at the end of the method, not at `Complete()`. `Complete()` only casts the commit vote — the scope stays the ambient transaction until `Dispose()`. `LoadInsurancePrices()` therefore issues its query with `Transaction.Current` pointing at a scope that is already complete, and `DbQuery.ExecuteHelper` calls `connection.Open()` (`src/Perpetuum/Data/DbQuery.cs:55`), which reads `Transaction.Current` for transacted connection pooling and throws:

```
System.InvalidOperationException: The current TransactionScope is already complete.
   at System.Transactions.Transaction.get_Current()
   at Microsoft.Data.ProviderBase.DbConnectionPool.GetFromTransactedPool(Transaction& transaction)
   at Microsoft.Data.SqlClient.SqlConnection.Open(SqlConnectionOverrides overrides)
   at Perpetuum.Data.DbQuery.ExecuteHelper[T](Func`2 execute) in DbQuery.cs:line 55
   at Perpetuum.Services.Insurance.InsuranceHelper.LoadInsurancePrices() in InsuranceHelper.cs:line 447
   at Perpetuum.Services.Insurance.InsurancePriceRefreshService.Refresh() in InsurancePriceRefreshService.cs:line 52
```

The exception propagates to the `catch` in `RefreshAsync`, so every run — the startup run and each daily run — is counted as a failure and logs `refresh failed (N consecutive failure(s))`. The success line at `InsurancePriceRefreshService.cs:53` never runs.

### Impact
`_insurancePrices` (`src/Perpetuum/Services/Insurance/InsuranceHelper.cs:401`) is a static cache populated lazily: on a miss, `GetInsurancePrice` reads `dbo.insuranceprices` once and keeps that value for the definition for the rest of the process lifetime. `LoadInsurancePrices()` is the only path that refreshes an already-cached definition during normal operation — the sole other caller is the `ProductionSetInsurance` request handler.

With `Refresh()` throwing before that call, the daily recalculation updates `dbo.insuranceprices` but the running server keeps quoting the values it cached earlier. Fees and payouts drift from the recalculated table for as long as the process stays up, and a restart is the only thing that clears it. This is the same player-visible symptom ISSUE-036 reports, so applying that issue's migration alone does not restore correct prices on a long-running server.

The `MERGE` itself is not lost: `Complete()` has already been called when the exception unwinds, so `Dispose()` still commits.

### Cross-Reference — ISSUE-036 (IN_PROGRESS)
This defect sat behind ISSUE-036. Until `usp_RecalculateInsurancePrices` was fixed, the `ExecuteNonQuery` on the previous line always threw error 217, so execution never reached `LoadInsurancePrices()`. Applying `docs/db_structure/migrations/ISSUE-036-fix-insurance-proc-self-dependency.sql` to a local P36.8 database removed the `nesting level exceeded` exception from the startup log and exposed this one in its place, at the next line of the same method.

ISSUE-036's production verification list should gain one step: after applying the migration, confirm the log carries `InsurancePriceRefreshService: prices recalculated and cache reloaded.` rather than another `refresh failed` line.

### Fix
`src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs` — replace the `using` declaration with a `using` block that closes immediately after `scope.Complete()`, so the transaction is disposed and `Transaction.Current` is null again before `LoadInsurancePrices()` runs. This matches the `using (var scope = Db.CreateTransaction())` form used throughout `Perpetuum.RequestHandlers`. No logic, no SQL and no transaction boundary changes: the `EXEC` remains the only statement inside the transaction, which is what the original code already intended.

### Notes
Reproduced on a local P36.8 database (`develop` at `f9ddac2`) with the ISSUE-036 migration applied. Status is `IN_PROGRESS` rather than `DONE` because production still has the ISSUE-036 migration pending — until it is applied there, the SQL error masks this code path and the fix cannot be observed live.

## ISSUE-035 - Server fails to start with the perpetuum.ini produced by the official installer

Status: IN_PROGRESS
Priority: HIGH
Area: Configuration / Setup

### Problem
The `perpetuum.ini` written by the Perpetuum Dedicated Server installer carries a connection string that `Microsoft.Data.SqlClient` rejects, in two independent ways:

1. `Connection Reset=True` — the keyword was removed in `Microsoft.Data.SqlClient`. Startup aborts with an unsupported-keyword error naming `Connection Reset`.
2. No `TrustServerCertificate` or `Encrypt` setting — `Microsoft.Data.SqlClient` 4.0+ defaults `Encrypt` to `true`, so a local SQL Server using a self-signed certificate fails logon in the SSL provider, reporting that the certificate chain was issued by an untrusted authority.

Both abort during `PerpetuumBootstrapper.Init`, before any zone is loaded. The legacy `System.Data.SqlClient` used by the original server accepted the first keyword and defaulted `Encrypt` to `false`, so the shipped file worked there.

### Impact
Every fresh local setup fails on first launch. Neither error message names `perpetuum.ini`, so the cause is not obvious from the output. No file under `docs/` mentions either change.

### Proposed Fix
Document the required connection-string edits in a setup page under `docs/codebase/`. Optionally, ignore unsupported legacy keywords when deserializing `GlobalConfiguration` and raise an error that names `perpetuum.ini` and the offending key.

### Notes
Reproduced against SQL Server 2022 Express, named instance, Windows integrated auth. Both errors were observed on a pt-BR install and are quoted here in translation, so search by condition rather than by exact string. Working connection string after both edits:

`Server=localhost\PERPSQL;Database=perpetuumsa;Trusted_Connection=True;TrustServerCertificate=True;Pooling=True;Connection Timeout=30;Connection Lifetime=260;Min Pool Size=20;Max Pool Size=60;`

`TrustServerCertificate=True` disables server certificate validation. It is appropriate for a local development instance only and must not be carried into a deployment where the connection leaves the machine.

### Progress

Both failures re-measured against `Microsoft.Data.SqlClient` 6.0.1 with neutral resources, so the messages below are the ones a maintainer sees rather than a translation:

- `Connection Reset=True` → `System.NotSupportedException: The keyword 'Connection Reset' is not supported on this platform.`, thrown while `SqlConnection` is being **constructed**, not on `Open()`.
- No `Encrypt` / `TrustServerCertificate` → `SqlException` on `Open()`: `A connection was successfully established with the server, but then an error occurred during the login process. (provider: SSL Provider, error: 0 - ...)`. The trailing clause comes from Win32 and stays in the system language.

**Keyword 1 is now handled in code, by reporting rather than repairing.** `ConnectionStringSupport.FindUnsupportedKeywords` returns every setting the driver will refuse. `PerpetuumBootstrapper.Init` calls it right after resolving `GlobalConfiguration`, and when the list is not empty it logs an error naming `perpetuum.ini`, the directory it sits in, and every offending setting, then throws so the server does not start. The connection string itself is never modified — the operator's file stays the only source of truth for how this server connects.

This follows the Proposed Fix above, which already asked for "an error that names `perpetuum.ini` and the offending key". An earlier revision of this work removed the keywords instead; that was changed on maintainer direction.

**The check keeps no list of its own.** It parses with `DbConnectionStringBuilder` — the provider-agnostic parser, which applies the ADO.NET quoting rules without validating against any driver — and then offers each setting to `SqlConnectionStringBuilder` in turn. Whatever that refuses is reported. So `Network Library` and `Context Connection` are now named too, where the removal-based revision had to leave them alone because removing them would have changed how the server connects. A setting nobody anticipated is reported the same way, and nothing here falls out of date when the driver changes.

Splitting on `;` by hand was rejected: it corrupts any quoted value containing a separator, verified against `Password="a;b=c"`.

The catch around the per-setting probe is deliberately broad, because the driver does not use one exception type. Measured against `Microsoft.Data.SqlClient` 6.0.1, and `SqlConnectionStringBuilder` and `SqlConnection` agree on every one:

| Setting | Exception |
|---|---|
| `Connection Reset` | `NotSupportedException` |
| `Network Library` | `NotSupportedException` |
| `Asynchronous Processing` | `ArgumentException` |
| `Context Connection` | `InvalidOperationException` |

A narrow filter that missed a type would report the setting as supported and hand the operator exactly the obscure startup failure this check exists to replace.

All offending settings are reported in one message rather than one at a time, because the installer's file carries more than one and a first-failure-only report would cost a restart per setting.

**Keyword 2 is documentation only.** Defaulting `TrustServerCertificate` in code would weaken authentication for every operator to fix a local development case, so `README.md` now carries a setup section stating the requirement, the working connection string, and the caveat that `TrustServerCertificate=True` belongs on a local instance only.

Verified in three states:

| State | Result |
|---|---|
| `Connection Reset` **and** `Network Library` present, check applied | One `ERR` line naming the game root and both settings, then the process exits with code 1. The check runs before anything else in `Init` needs the game root |
| Neither present, check applied | `tools/smoke-test.ps1` green against the real database: `[Online]` after 80 s, 6435 members spawned, graceful shutdown in 29 s, exit 0. No new output on the startup path |
| Settings present, check absent | `System.NotSupportedException: The keyword 'Connection Reset' is not supported on this platform.`, naming the keyword but not the file. Held as an automated test rather than a manual observation — `ConnectionStringSupportTests.The_driver_really_does_reject_the_installer_string` fails the moment the driver stops rejecting it, which would make the whole check dead weight |

Covered by 15 unit tests in `src/Perpetuum.Tests/Unit/ConnectionStringSupportTests.cs`. Eight of them were observed failing against a stub returning an empty list before the detection was written.

Reported settings carry the lower-cased spelling `DbConnectionStringBuilder` produces, not the operator's own capitalisation. Left as is: any case-insensitive search finds the line, and preserving the original spelling would mean re-scanning the raw string for cosmetics.

### Notes on the documentation location

The Proposed Fix above suggested a setup page under `docs/codebase/`. It went into `README.md` instead: the repository had no setup documentation of any kind, `README.md` was a badge and a title, and it is where someone who just ran the installer looks first. `docs/codebase/` describes the codebase for contributors; this is an operator instruction. Happy to move it if the maintainers prefer.

---

---

## ISSUE-038 - Server RAM usage grows over time — possible memory leak

Status: IN_PROGRESS
Priority: CRITICAL
Area: Server / Runtime / Performance

### Problem
Over time, the running server process shows steadily growing RAM consumption. No specific subsystem has been identified yet. Needs investigation both in general (long-lived caches, event handler leaks, undisposed resources, static collections that only grow) and specifically in terms of recent changes to the codebase, since a regression introduced by recent work is a plausible contributor.

### Impact
Unchecked growth risks eventual OOM crashes or degraded performance (GC pressure) on long-running server instances, affecting all players connected at the time of a crash/restart.

### Proposed Fix
1. **Establish a baseline** — capture memory growth rate under normal load (e.g. dotnet-counters / dotnet-gcdump / dotnet-trace snapshots over time) to characterize the leak (steady linear growth vs. growth tied to specific events like zone transitions, missions, or NPC spawns).
2. **Diff heap snapshots** — take two or more `dotnet-gcdump` snapshots hours apart and diff retained object counts/types to identify what's accumulating (e.g. event subscriptions never unsubscribed, collections indexed by entity/session that are never cleaned up, timers/tasks not disposed).
3. **Audit recent changes** — review recently merged work (e.g. IMPROVEMENT-043 Hunter Drones: drone spawning/despawning, controller wiring, effect setup) for undisposed handlers, static/singleton collections growing per-spawn without corresponding cleanup on despawn/disconnect, or event subscriptions added without matching unsubscription.
4. **Check known high-churn subsystems** — zone updates, NPC AI/spawning, mission engine, market processing, season activity tracking — for per-tick or per-entity allocations that aren't being released (per repo-wide "High-risk hot paths" list in CLAUDE.md).

### Progress

**Static code audit (bullets 3/4) — no leak found, several candidates cleared:**
- IMPROVEMENT-043 (Hunter Drones/Self-Destruct) full diff reviewed: `BandwidthHandler.OnRemoteChannelDeactivated` correctly unsubscribes itself, no new static collections, `SelfDestructDetonation.IsArmed` reads per-unit effect state rather than a static tracker. Clean.
- `Flock.AddMember`'s `npc.Dead += OnMemberDead` is never unsubscribed in `RemoveMember`, but traced the reference direction: the short-lived `Npc` holds the reference to the long-lived `Flock`, not the reverse, so this does not keep flocks (or anything they own) alive — not a leak, just untidy.
- `SessionManager._charactersIndex` (candidate: stale sessions retained past abrupt disconnect) — traced the full disconnect chain (`Session.OnDisconnected → SignOut → DeselectCharacter → CharacterDeselected event → SessionManager` removes the entry). Wired correctly, not a leak.
- Swept static `Dictionary`/`ConcurrentDictionary` fields repo-wide (`NpcEp`, `InsuranceHelper._insurancePrices`, `TransportAssignment.Helpers._baseToTransportStorages`, etc.) — all keyed by a small bounded universe (definition id, base eid), not per-session/per-request. `CorporationDocumentHelper._corporationDocumentViewers` can retain an empty `CorporationDocumentViewer` per distinct viewed (not just registered) `documentId` — a very slow, bounded-by-total-documents accumulation, not a strong match for "grows over time" but noted as a minor cleanup opportunity if anyone revisits that file.

**Live profiling (bullets 1/2) — idle baseline established, no growth observed at idle:**
- Ran `Perpetuum.Server` locally against the dev DB (`perpetuumsa`) with all zones loaded, no players connected, using `dotnet-counters`/`dotnet-gcdump` (both installed as global tools).
- **Baseline** (t0, right after zone load settled): Working Set ~6.58 GB, GC Heap ~5.1 GB (LOH ~4.58 GB / 90% of heap, Gen2 ~970 MB, dominated by per-zone terrain arrays `PlantInfo[]`/`BarrierInfo[]`/`TerrainControlInfo[]`/`BlockingInfo[]` and static content like 6,415 `Npc` and 8,243 `RandomPointMissionTarget` instances — all one-time world/content load, not obviously leaked). Idle allocation rate was ~266 MB/s (see Perf note below).
- **Second snapshot ~50 minutes later, still idle, no players**: GC Heap total was **4,301,091,381 bytes vs. baseline's 4,300,626,679** (+0.01%, noise) with object count actually *down* slightly (8,790,784 vs 8,792,588). Per-type diff across the full heap found exactly one type crossing a 100KB delta threshold — a single transient `System.Byte[]` buffer, ~131KB — i.e. no meaningful growth anywhere in the heap.
- **Conclusion: the server does not leak memory merely from being alive with background zone/AI ticks running.** The reported production growth is very likely tied to actual player-driven activity/session churn (logins/logouts, missions, PBS, market, combat) rather than passive uptime, or accumulates far more slowly than a 50-minute window can detect above GC noise.
- **Recommended next step for whoever continues this**: reproduce with real load — either an automated test client looping through session connect/disconnect + mission accept/complete + PBS facility use + NPC kill cycles while re-sampling `dotnet-gcdump` every 15-30 min, or (lower effort) add a scheduled `dotnet-gcdump` capture against the **production** process during real play hours and diff those, since that's where the growth was actually observed. Compare object counts for session/character/mission-target/NPC-adjacent types between snapshots taken hours apart under real traffic.

**Unrelated finding worth a look regardless of the leak:** idle allocation rate of ~266 MB/s (no players) is unexpectedly high for a supposedly idle loop — Gen0/1 collections keep up fine so this isn't the leak, but it suggests something in the zone tick / PBS energy-network reconciliation path (log showed continuous `facility enabler received` / `++CONNECT++` churn even with nobody online) is allocating far more than expected per tick. Could be worth a separate perf-focused look, not filed as its own issue yet.

**Root cause found and fixed: stale/ghost client connections from a 24-hour TCP keepalive.** Following up on the "needs real player activity/session churn" conclusion above:
- `TcpConnection` (`src/Perpetuum/Network/TcpConnection.cs:32`) set the OS-level TCP keepalive to `time=24 hours`, `interval=5s` before the first probe. Both `Session`'s connection (`SessionConnection : EncryptedTcpConnection : TcpConnection`, the lobby/login/character-select connection) and `ZoneSession`'s connection (`EncryptedTcpConnection : TcpConnection`, the actual gameplay connection) inherit this from the same base class — i.e. every client socket the server ever opens.
- This keepalive is the *only* backstop for a peer that disappears without a clean TCP close. Confirmed there's no app-level heartbeat/idle-timeout anywhere: `ZoneSession` already tracks `_lastReceivedPacketTime`/`InactiveTime`, but it's only read once, by `Player.cs:1305`, for the "was this player AFK at time of death" trash/loot calculation — never checked against a threshold to force a disconnect.
- Net effect: a client that vanishes without sending a TCP FIN or the app-level "closing socket" command (crash, force-kill, WiFi/network drop, laptop sleep/lid-close, mobile network handover, power loss — all common, everyday occurrences for any real player base) leaves a fully-resident ghost session on the server for **up to 24 hours** before the OS keepalive even starts probing. Until then: `SessionManager._sessions`/`_charactersIndex` keep the entry, `Character.IsOnline` stays `true`, and — critically for the "constant load" half of the user's question — the ghost's `Player` robot stays `InZone`, meaning the zone's `ProcessManager` tick loop keeps processing it every frame (nearby NPC/player targeting checks, regen, etc.), not just holding memory.
- This fully explains why the earlier idle-server profiling (zero players, 50 min, flat heap) found nothing: ghost sessions can only be created by real client churn, which a zero-player idle test can't produce. A live server with a real population would accumulate ghosts throughout the day at whatever rate players disconnect ungracefully, consistent with "RAM usage grows over time."
- **Fix applied**: `TcpConnection.cs:32` keepalive time changed from `1000 * 60 * 60 * 24` (24h) to `1000 * 60 * 60 * 2` (2h), per explicit user direction (a more conservative value than the 30-60s initially proposed, to avoid false-positives from ordinary short-lived network hiccups while still bounding worst-case ghost lifetime to 2h instead of 24h). Interval left at 5s. Build verified (`Perpetuum.csproj` Release/x64, 0 errors/warnings).
- **Deliberately not done**: an application-level idle-timeout/heartbeat enforcement (using the existing but currently-unused `InactiveTime`) as a more robust backstop independent of OS keepalive behavior (which some NATs/firewalls can interfere with) — user was offered this as an option and did not request it. Worth reconsidering if 2h keepalive still isn't tight enough after real-world observation.

### Notes
- No specific subsystem, reproduction steps, or timeframe confirmed yet for the actual leak *magnitude* — the idle-server hypothesis was ruled out by direct measurement, and the stale-connection root cause above is a strong, confirmed mechanism, but its actual contribution to the originally-reported growth hasn't been measured against production (no way to do that from this dev environment). Recommend monitoring production RAM/session-count trends after this fix ships to confirm impact before closing this issue.
- Tooling installed for this investigation (both global dotnet tools, reusable next time): `dotnet-counters`, `dotnet-gcdump`.

---

## ISSUE-037 - Mission target NPCs sometimes fail to spawn — InvalidCastException casting Player to Npc in Flock.CreateMemberInZone

Status: IN_PROGRESS
Priority: CRITICAL
Area: Missions / NPC Spawning

### Problem
Players report that on some assignments, target NPCs sometimes fail to spawn at all. No specific assignment/mission has been identified yet — reports are inconsistent about which mission triggers it. Production logs show a matching exception on the NPC-spawn-on-success path:

```
System.InvalidCastException: Unable to cast object of type 'Perpetuum.Players.Player' to type 'Perpetuum.Zones.NpcSystem.Npc'.
   at Perpetuum.Zones.NpcSystem.Flocks.Flock.CreateMemberInZone() in Flock.cs:line 112
   at Perpetuum.Zones.NpcSystem.Flocks.Flock.SpawnAllMembers() in Flock.cs:line 81
   at Perpetuum.Zones.NpcSystem.Flocks.FlockExtensions.SpawnAllMembers(IEnumerable`1 flocks) in FlockExtensions.cs:line 10
   at Perpetuum.Services.MissionEngine.MissionTargets.ZoneMissionTarget`1.AddDirectPresenceToPosition(IPresenceManager presenceManager, Position successPosition) in ZoneMissionTarget.cs:line 396
   at Perpetuum.Services.MissionEngine.MissionTargets.ZoneMissionTarget`1.<>c__DisplayClass38_0.<SpawnNpcOnSuccess>b__0() in ZoneMissionTarget.cs:line 353
```

`Flock.CreateMemberInZone()` (`src/Perpetuum/Zones/NpcSystem/Flocks/Flock.cs:112`) does:
```csharp
var npc = (Npc)EntityService.Factory.Create(Configuration.EntityDefault, EntityIDGenerator.Random);
```
`EntityService.Factory.Create` builds a concrete entity type based on the `EntityDefault`'s configured entity class, and the result is unconditionally cast to `Npc`. The exception means that for the flock's `Configuration.EntityDefault` used in this failing case, the factory produced a `Player` instance instead — i.e. the entity default resolved by that flock/presence configuration is not actually an NPC-class entity default. This is called from `ZoneMissionTarget.AddDirectPresenceToPosition` → `SpawnNpcOnSuccess`, which runs on a background `Task.Run` (`ZoneMissionTarget.cs:351`) with `.LogExceptions()` — the exception is logged but swallowed, so the mission's success/spawn flow does not surface a player-facing error; the target presence/flock is simply left without its NPC(s).

### Impact
- Assignments that spawn NPCs on success (random-pop / direct-presence style targets, `ZoneMissionTarget.SpawnNpcOnSuccess`) can silently fail to populate their target NPC(s), leaving the mission target impossible to complete.
- Failure is silent to the player — no error is shown, the beam/teleport-storm effect may still fire (it runs regardless, after the spawn loop), but the flock has zero or partial members.
- Since the failure happens on a fire-and-forget background task, it does not crash the mission engine or zone loop, which makes it easy to miss without log monitoring — consistent with reports being vague about which assignment is affected.

### Proposed Fix
1. **Identify the misconfigured EntityDefault** — determine which mission target(s)/`DirectPresenceConfiguration`/flock `EntityDefault` combination resolves to a non-NPC entity class. Check `entitydefaults`/related content tables for the definition(s) used by affected mission targets' presence/flock configs, and verify their configured entity type actually maps to an `Npc`-derived class in `EntityService.Factory`, not `Player` or another type.
2. **Fix the root data/config issue** — correct the offending definition reference in the mission/presence content so it points to a valid NPC entity default.
3. **Add defensive handling regardless of the data fix** — `Flock.CreateMemberInZone()` should not let a bad `EntityDefault` throw an unhandled cast exception deep in a background task. Consider validating the entity type before/after `Factory.Create` and logging a clear, actionable error (including `Configuration.EntityDefault`/flock/presence identifying info) instead of an opaque `InvalidCastException`, so future misconfigurations are diagnosable without a stack trace alone.

### Progress
- **Bullet 3 DONE**: `Flock.CreateMemberInZone()` (`src/Perpetuum/Zones/NpcSystem/Flocks/Flock.cs`) no longer unconditionally casts the factory result to `Npc`. It now checks the resolved entity type; on mismatch it logs an actionable `Logger.Error` (EntityDefault definition id + name, resolved CLR type, flock/presence name, zone id) and returns without spawning that member, instead of throwing an opaque `InvalidCastException` on a background task. The flock is left with fewer/zero members exactly as before (no behavior regression for the working case), but the failure is now diagnosable from the log line alone.
- **Bullets 1/2 NOT DONE — traced but not reproduced.** `SpawnNpcOnSuccess`'s strict-definition path (`DirectPresence.DoStrictDefinitionFlocks`, used when `MyTarget.useQuantityOnly == false`) is only reachable from two callers: `PopNpcZoneTarget.OnTargetComplete` (targettype 20, `pop_npc`) and `FindArtifactZoneTarget.OnTargetComplete` when `FindArtifactSpawnsNpcs` (targettype 11, `find_artifact`, gated on the `spawnnpcs` column). Queried the local dev DB (`perpetuumsa`) directly:
  - Every `pop_npc` target has `usequantityonly = 1`, i.e. none of them take the strict-definition path — they all go through `DoSelectNpcsFromPool`, which sources NPCs from `robottemplaterelations` (already NPC-safe).
  - Every `find_artifact` target with `spawnnpcs = 1` has `definition IS NULL`, which resolves to `EntityDefault.None` (definition 0) — traced through `EntityFactory`'s keyed-container fallback (`EntitiesModule.cs:680`, `!c.IsRegisteredWithKey<Entity>(ed.Definition) ? ctx.Resolve<Entity>() : ...`) and confirmed this yields a plain `Entity`, not `Player` — so it would produce a *different* cast exception message than the one in the reported stack trace, ruling this out as the match.
  - Category-flag resolution itself (`EntitiesModule.cs` `ByCategoryFlags<Player>(cf_robots)` / `ByCategoryFlags<Npc>(cf_npc)`) is exact-match on the low byte (`cf_robots` low byte `0x01` vs `cf_npc` low byte `0x8F` — see `CategoryFlagsExtensions.IsCategory`), and mutually exclusive, so this isn't a registration-ordering bug in code either.
  - Conclusion: the misconfigured content that produced the production stack trace is not present in this local dev DB snapshot — it's very likely prod-only content (or has since been edited/removed there). Root identification still requires production log correlation (mission id / target id / definition id at the moment of the exception), same blocker as noted below.

### Notes
- Exact affected assignment(s) are unknown — needs log correlation (mission ID / target ID / definition ID at time of the exception) to narrow down. Search production logs around each occurrence for the mission/target context that isn't captured in the current log line. Once identified, run: `SELECT mt.*, ed.definitionname, ed.categoryflags & 0xFF AS low_byte FROM missiontargets mt JOIN entitydefaults ed ON ed.definition = mt.definition WHERE mt.id = <target id>` against production/prod-mirrored content to confirm the low_byte is `0x01` (cf_robots/Player) instead of `0x8F` (cf_npc/Npc), then correct `missiontargets.definition` for that row to a valid NPC entity default.
- `SpawnNpcOnSuccess` (`ZoneMissionTarget.cs:344-375`) and `AddDirectPresenceToPosition` (`ZoneMissionTarget.cs:379-400`) are shared by `ZoneMissionTarget<T>`, so this is not scoped to one mission target subtype — any assignment using direct-presence NPC pop-on-success is a candidate.

---

## ISSUE-036 - Insurance payouts stale/too high — usp_RecalculateInsurancePrices recurring "nesting level exceeded" failure

Status: IN_PROGRESS
Priority: CRITICAL
Area: Economy / Insurance

### Problem
Players report insurance payouts are too high and don't reflect current market state. Production logs show `InsurancePriceRefreshService.Refresh()` failing on every scheduled run with:

```
Microsoft.Data.SqlClient.SqlException: Maximum stored procedure, function, trigger, or view nesting level exceeded (limit 32).
   at Perpetuum.Data.DbQuery.ExecuteHelper[T](Func`2 execute) in DbQuery.cs:line 54
   at Perpetuum.Services.Insurance.InsurancePriceRefreshService.Refresh() in InsurancePriceRefreshService.cs:line 40
```

`Refresh()` (`src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs:37-44`) wraps `EXEC usp_RecalculateInsurancePrices` in a `TransactionScope` that is never completed when the exception is thrown — `scope.Complete()` at line 41 is skipped, so the MERGE never commits. `InsuranceHelper.LoadInsurancePrices()` also never runs. `dbo.insuranceprices` is therefore frozen at its last successfully computed values while raw material prices keep moving, so payouts drift out of sync with the market (upward, since fees/payouts are proportional to production cost which has likely risen since the last successful run).

### Impact
- Insurance payout/fee values do not track current production cost — a direct economic/balance bug affecting every insured loss payout.
- The failure is silent: only `Logger.Exception(ex)` fires (`InsurancePriceRefreshService.cs:32`), with no alerting, so this can persist for a long time before being noticed via player reports.
- Insurance is designed as a NIC sink (payout_pct < fee_pct); stale/inflated payouts erode that sink and can flip it toward a net NIC faucet if payout no longer reflects current (lower) production costs.

### Cross-Reference — ISSUE-029 (DONE)
Same exception signature as ISSUE-029, fixed there by inlining `production_data` as a local CTE (`prod_data`) inside `v_all_production_costs` (and the now-renamed `v_required_raw_materials`) so the recursive CTE stops incrementing SQL Server's view-nesting counter each iteration. **The ISSUE-029 fix was confirmed correctly deployed** (view text in the test DB matches docs exactly) — it is not the cause of this recurrence.

### Root Cause (Confirmed)
Not chain depth, not a missing ISSUE-029 deployment. `IMPROVEMENT-036-insurance-overhaul.sql` created `usp_RecalculateInsurancePrices` with the `CREATE OR ALTER PROCEDURE ... END` block immediately followed, **in the same batch (no `GO`)**, by:
```sql
DELETE FROM dbo.insurance;
EXEC dbo.usp_RecalculateInsurancePrices;
```
SQL Server's deferred-name-resolution dependency parser captured that trailing `EXEC` as belonging to the module itself, recording a bogus self-referencing row in `sys.sql_expression_dependencies` (the procedure listed as depending on itself):
```sql
SELECT referenced_entity_name FROM sys.sql_expression_dependencies
WHERE referencing_id = OBJECT_ID('dbo.usp_RecalculateInsurancePrices');
-- returned usp_RecalculateInsurancePrices itself, alongside the real dependencies
```
`v_all_production_costs` already sits close to SQL Server's 32-level nesting ceiling even after the ISSUE-029 fix (per that issue's ~28-level headroom note). The bogus self-dependency adds just enough extra nesting accounting to push execution over the limit — error 217 on every run. `sp_recompile` does **not** clear this (verified); only re-issuing `CREATE OR ALTER PROCEDURE` as the sole statement in its own batch recalculates the dependency list and removes the self-reference.

Reproduced and verified against the local up-to-date-with-live test DB:
- `EXEC usp_RecalculateInsurancePrices` reliably failed with error 217 in the DB's existing (as-deployed) state.
- Re-creating the exact same procedure body in isolation (own batch, no trailing statements) immediately fixed it — `sys.sql_expression_dependencies` dropped to the 4 legitimate dependencies and the procedure ran cleanly.
- Re-deploying the buggy form (proc + trailing `DELETE`/`EXEC` in one batch, mirroring the original migration) reproduced the failure again on demand, confirming the mechanism.

### Fix
1. **`docs/db_structure/migrations/ISSUE-036-fix-insurance-proc-self-dependency.sql`** (new) — re-issues `usp_RecalculateInsurancePrices` alone in its own batch (`GO`-terminated) with byte-identical logic to `docs/db_structure/stored_procedures/dbo.usp_RecalculateInsurancePrices.sql`, purging the bogus self-dependency. Idempotent (`CREATE OR ALTER`). **Needs to be applied manually to the live production DB by the operator** — not yet applied there.
2. **`docs/db_structure/migrations/IMPROVEMENT-036-insurance-overhaul.sql`** — added the missing `GO` after the procedure's `END` so this script can't reintroduce the same bogus self-dependency if it's ever re-run against a fresh environment.
3. **`src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs`** — added consecutive-failure tracking; each failure now also logs via `Logger.Error` with a running consecutive-failure count and an explicit "prices are stale" note, so a persistent refresh failure is loud in logs rather than only visible via a single `Logger.Exception` call. Reset to 0 on the next successful run. (No external alert/paging integration exists for this service yet — wiring one up was judged out of scope for this fix.)

Other proc-creating migrations were spot-checked (`IMPROVEMENT-039`, `-040`, `-042`) — none have trailing statements sharing a batch with a `CREATE PROCEDURE` block the way `IMPROVEMENT-036` did, so this is not believed to be a systemic pattern elsewhere.

### Notes
- Status is `IN_PROGRESS`, not `DONE`: the code fix is in and build-verified, and the corrective migration is generated and verified against the local test DB, but per project convention DB migrations are never applied directly by the agent — **the operator must run `ISSUE-036-fix-insurance-proc-self-dependency.sql` against production** before this is fully resolved live.
- Once applied, confirm on production: `SELECT referenced_entity_name FROM sys.sql_expression_dependencies WHERE referencing_id = OBJECT_ID('dbo.usp_RecalculateInsurancePrices');` returns exactly 4 rows (no self-reference), `EXEC dbo.usp_RecalculateInsurancePrices` succeeds, and `dbo.insuranceprices` values update to match current `v_all_production_costs` output.

---

## ISSUE-032 - Recurring season creates duplicate next-run on each cache refresh before new run starts

Status: DONE
Priority: CRITICAL
Area: Seasons / Recurring

### Problem
After a recurring season ends and a new run is cloned (but not yet started — its `start_time` is in the future), `SeasonService` keeps creating additional clones on every subsequent `RefreshCache()` call, producing duplicate season rows.

### Root Cause (Confirmed)
`GetPendingRecurringSeason()` lacked a filter on `end_time`. Its query:
```sql
WHERE is_active = 0 AND is_recurring = 1 AND start_time <= GETUTCDATE()
```
matches the already-completed previous season (S1) because S1 has `is_active = 0`, `is_recurring = 1`, and `start_time` in the past — even though it has already ended. The future clone (S2) is excluded by the `start_time <= now` predicate until its own start time arrives.

This caused `RefreshCache()` to re-activate S1 every 5 minutes → S1 ends immediately → `ProcessSeasonEnd(S1)` runs again → another clone is created → indefinitely.

### Fix
1. **Primary** — Added `AND end_time > GETUTCDATE()` to `GetPendingRecurringSeason()`. Ended seasons are no longer candidates, so only truly future pending runs are returned.
2. **Defense-in-depth** — Added `HasFutureClone(Season)` repository method that checks for any existing future inactive clone in the same recurring chain. `ProcessSeasonEnd` now guards `CloneSeasonForNextIteration` with this check, preventing a second clone even if the method fires twice.

### Files Changed
- `src/Perpetuum/Services/Seasons/SeasonRepository.cs` — fixed query in `GetPendingRecurringSeason()`; added `HasFutureClone()`
- `src/Perpetuum/Services/Seasons/SeasonService.cs` — guarded clone call in `ProcessSeasonEnd`

### Notes
- Any orphan clone rows already accumulated in the DB (`start_time` in the future, `is_active = 0`) are harmless and will be correctly activated when their `start_time` arrives. Duplicates with the same `start_time` should be deleted manually.

---

## ISSUE-031 - Season leaderboard rewards not delivered automatically or via admin command

Status: DONE
Priority: CRITICAL
Area: Seasons / Rewards / Leaderboard

### Problem
Participants of a season are not receiving leaderboard rewards, neither automatically when the season ends nor when an admin manually triggers the reward delivery command.

### Related Error
The following exception fires in `SeasonService.Update` on every tick, which may block the reward delivery path:

```
System.InvalidCastException: Unable to cast object of type 'System.Byte' to type 'System.Int32'.
   at Perpetuum.Data.DataRecordExtensions.GetValue[T](IDataRecord record, Int32 index)
   at Perpetuum.Data.DataRecordExtensions.GetValue[T](IDataRecord record, String name)
   at Perpetuum.Services.Seasons.SeasonRepository.GetPendingRecurringSeason()
   at Perpetuum.Services.Seasons.SeasonService.RefreshCache()
   at Perpetuum.Services.Seasons.SeasonService.Update(TimeSpan time)
```

A column returned by the `GetPendingRecurringSeason` query is typed as `tinyint` (or similar `BYTE`-width type) in the DB but is read as `int` in C#. The exception throws every update tick, causing `RefreshCache()` to abort. This may be preventing the service from ever seeing the active season — and therefore from running leaderboard reward delivery.

### Impact
- Leaderboard rewards are silently not delivered to top season participants.
- `SeasonService.RefreshCache()` crashes on every update tick due to the type mismatch.
- Admin re-deliver command has no effect if the service cannot load season state.
- Players expect rewards after season end; silent failure erodes trust.

### Proposed Fix
1. **Fix the type mismatch** — identify which column in the `GetPendingRecurringSeason` result set is a `tinyint`/`smallint`/`byte` in SQL but is read as `int` in C#. Change the C# read to use the correct numeric type, or `CAST` the column to `int` in the query.
2. **Verify reward delivery path** — once `RefreshCache()` no longer throws, confirm that leaderboard reward delivery runs for the ended season. If not, trace the delivery trigger separately.
3. **Investigate admin command** — check whether the admin re-deliver command (`SeasonRedeliverLeaderboardRewards`, if implemented per ISSUE-025) also depends on `RefreshCache()` or uses a separate repository path that may have its own bug.

### Notes
- Stack trace points to `SeasonRepository.cs:511` inside `GetPendingRecurringSeason()`.
- Cross-reference ISSUE-025 (leaderboard rewards not delivered — root cause was swapped `rank_min`/`rank_max`). Verify those DB rows are correctly set before concluding the reward path itself is broken.
- The exception is non-fatal to the process but fires on every tick — investigate whether it swallows the exception or propagates to the caller and aborts the season update loop.

---

## ISSUE-029 - Insurance price recalculation crashes with SP nesting level exceeded (limit 32)

Status: DONE
Priority: CRITICAL
Area: Economy / Insurance

### Problem
On production, calling `usp_RecalculateInsurancePrices` throws:

> Maximum stored procedure, function, trigger, or view nesting level exceeded (limit 32)

The recalculation fails entirely; insurance prices are not updated.

### Root Cause (Confirmed)
Both `v_all_production_costs` and `v_required_raw_materials` contain recursive CTEs whose recursive
member JOINs against `production_data`, which is a VIEW (not a base table). SQL Server increments
the view nesting counter on every recursive iteration that references an external view. On production
data with crafting chains deeper than ~28 items the counter exceeds the 32-level limit. Locally,
sparse data means chains rarely exceed 3–5 levels, so the bug never triggers.

`usp_RecalculateInsurancePrices` executes `v_all_production_costs` inline inside a MERGE statement,
which exposes the per-iteration view nesting accumulation. `usp_RefreshAutoMarketOrders` is
unaffected because it materializes the same views into temp tables via a standalone SELECT, where
the optimizer handles the recursive CTE differently.

### Fix
Inlined `production_data` as a local CTE (`prod_data`) at the top of both recursive views.
A CTE reference inside a recursive member does not increment the view nesting counter.
Semantics are identical (same filter, same columns).

### Files Changed
- `docs/db_structure/views/v_all_production_costs.sql`
- `docs/db_structure/views/v_required_raw_materials.sql`
- `docs/db_structure/migrations/ISSUE-029-fix-view-nesting-in-recursive-cost-views.sql`

### Notes
- Migration can be applied while the server is running (`CREATE OR ALTER VIEW` is non-blocking).
- After applying, uncomment and run `EXEC dbo.usp_RecalculateInsurancePrices` to verify.

---

## ISSUE-028 - AdminTool AutoMarket: buyback orders not removed after deleting item from trade list

Status: DONE
Priority: CRITICAL
Area: AdminTool / AutoMarket

### Problem
After deleting an item from the AutoMarket trade list and running "Refresh Now", sell orders for that item were removed correctly but buy (buyback) orders remained on the market.

### Root Cause
Step 0 of `usp_RefreshAutoMarketOrders` snapshots "unbought resources" using `NOT EXISTS (SELECT 1 FROM market_orders_configuration)` to skip production-item buyback orders. When an item is deleted from `market_orders_configuration` before the SP runs, this check passes for its buyback order — the order is captured into `automarket_unbought_resources` as if it were an unfulfilled raw-material buy order. Step 1 deletes all auto orders, but Step 4 then re-inserts a new buy order for the deleted item from the `Unbought` carry-over, because the item still has a production cost in `v_all_production_costs`.

### Fix
In Step 0's `automarket_unbought_resources` insert, replaced:
```sql
AND NOT EXISTS (SELECT 1 FROM market_orders_configuration moc WHERE moc.definitionname = ed.definitionname)
```
with:
```sql
AND NOT EXISTS (SELECT 1 FROM production_data pd_check WHERE pd_check.product = ed.definitionname)
```
This classifies items by whether they can be manufactured (stable) rather than whether they are currently in the trade list (breaks on deletion).

### Files Changed
- `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`

---

## ISSUE-027 - Sell orders at matching prices do not auto-fulfill against open buy orders

Status: DONE
Priority: CRITICAL
Area: Market / Trading

### Problem
Players report that creating a sell order at a price equal to or below an existing open buy order does not result in an automatic trade. The sell order is posted as a standing order rather than immediately matching and settling against the best available buy order.

### Impact
Market trades do not settle when they should. Players placing competitive sell orders experience no fulfillment despite valid counterpart buy orders existing, breaking the fundamental market matching expectation and potentially trapping capital in open orders.

### Root Cause
The matching condition in both `MarketCreateSellOrder` and `MarketCreateBuyOrder` was:
```csharp
if (!forMyCorporation && highestBuyOrder != null)
```
This condition completely skips automatic matching whenever the player marks their order as corporation-only (`forMyCorporation = true`), even when a matching corp-only order from the same corporation exists. Players in player corporations are the primary affected group.

Additionally, `GetHighestBuyOrder` had a minor inconsistency: the SQL column reference used `@itemDefinition` (capital D) while `SetParameter` used `@itemdefinition` (lowercase d) — and similarly `submitterEID` vs `submittereid`. These are harmless with SqlClient's case-insensitive parameter matching but were corrected for consistency.

### Fix
- `MarketCreateSellOrder.HandleRequest`: Changed condition to `highestBuyOrder != null && (!forMyCorporation || highestBuyOrder.forMembersOf == forMembersOf)` — allows corp-only sells to match against corp buy orders from the same corp, while still blocking corp sells against public buy orders.
- `MarketCreateBuyOrder.HandleRequest`: Same symmetric fix for `lowestSellOrder`.
- `MarketOrderRepository.GetHighestBuyOrder`: Normalized SQL column/parameter names to lowercase for consistency with `GetLowestSellOrder`.

---

## ISSUE-026 - AdminTool AutoMarket Orders filters not working as expected

Status: TODO
Priority: MEDIUM
Area: Admin Tool / AutoMarket

### Problem
Three distinct filter bugs on the AutoMarket → Orders view in the Admin Tool:

1. **Order type filter returns no results** — selecting a buy or sell order type filter produces an empty list regardless of actual order volume. Likely a binding or query mismatch between the selected enum/value and what the server-side filter expects.
2. **Category filter excludes child categories** — filtering by a parent category only returns items assigned directly to that category; items in sub-categories are excluded. The filter needs to match the selected category and all of its descendants.
3. **No way to reset filters** — once a filter is applied, there is no reset or clear button. Users must restart or navigate away to return to the unfiltered list.

### Impact
Operators cannot meaningfully browse or audit market orders. The broken type and category filters make it impractical to find specific orders; the lack of reset compounds the friction by trapping users in a filtered state.

### Proposed Fix
1. **Order type filter** — trace the selected value from the UI dropdown through the ViewModel command to the server query. Verify the filter value is correctly mapped to the DB column type and that the query predicate is applied (not silently dropped).
2. **Category filter** — replace the direct category equality check with a recursive or closure-based lookup that resolves all descendant category IDs for the selected node and filters on the full set (e.g. via a recursive CTE or a pre-loaded category tree walk).
3. **Reset filters** — add a "Clear Filters" button (or equivalent reset action) to the Orders view that restores all filter fields to their default/unset state and reloads the full order list.

### Notes
- Investigate whether the type filter bug is a null/default value mismatch (e.g. enum default being passed as the filter even when "All" is selected, or vice versa).
- The category tree hierarchy is likely already used elsewhere in the Admin Tool or game content — reuse the existing resolution pattern rather than introducing a new one.
- Fix all three as a single unit since they share the same view; shipping a partial fix leaves the Orders filter UX still broken.

---

## ISSUE-030 - SeasonService ignores season start time, activating seasons before they should begin

Status: DONE
Priority: CRITICAL
Area: Seasons

### Problem
`SeasonService` does not enforce `start_time` anywhere. A season marked `is_active = 1` with a future `start_time` is immediately treated as live: `GetActiveSeason()` queries only `WHERE is_active = 1` with no `start_time <= GETUTCDATE()` guard, and `RefreshCache()`, `RecordActivity()`, and `OnCharacterLogin()` all check only `EndTime` — `StartTime` is never compared against `DateTime.UtcNow` at runtime.

### Impact
- Activity points accumulate before the season is intended to start.
- Players receive intro mails and leaderboard announcements prematurely.
- Recurring season clones (whose `start_time` is set to a future date) go live immediately after the previous season ends instead of waiting for their scheduled start.

### Proposed Fix
Two-layer enforcement:

1. **DB layer** — add `AND start_time <= GETUTCDATE()` to the `GetActiveSeason()` query in `SeasonRepository` so a future-dated active season is invisible to the service until its start time arrives.
2. **Service layer** — in `RefreshCache()`, after loading the season, assert `DateTime.UtcNow >= season.StartTime`; if not, treat as no active season (clear cache, do not notify).  Guard `RecordActivity()` and `OnCharacterLogin()` with the same check so the in-memory `_activeSeason` cannot process activity before start even if the cache is stale.

The DB guard is the primary fix. The service-layer check is a defence-in-depth backstop.

### Notes
- `SeasonService.cs` line 114: `_repository.GetActiveSeason()` — fix in repository query.
- `SeasonRepository.cs` lines 11-15: the `WHERE is_active = 1` query needs the `start_time` predicate.
- `RecordActivity()` line 188: only guards `EndTime`; add `DateTime.UtcNow < season.StartTime` early return.
- `OnCharacterLogin()` line 273: same pattern.
- The recurring season clone path (`CloneSeasonForNextIteration`) already sets a future `start_time`, so the DB fix automatically gates the clone.

---

## ISSUE-025 - Top leaderboard participants did not receive rewards after Active Season ended

Status: IN_PROGRESS
Priority: CRITICAL
Area: Seasons / Rewards / Leaderboard

### Problem
After "Seasons, oh May!" (end_time 2026-06-01T03:00:00) concluded, top leaderboard participants received no rewards. Root cause confirmed: data configuration error.

### Root Cause (Confirmed)
All 3 `season_leaderboard_rewards` rows have `rank_min > rank_max` (swapped fields):

| rank_min | rank_max | Package | Intended |
|---|---|---|---|
| 3 | 1 | Syndicate_Season1_Leadership1 | min=1, max=3 |
| 6 | 4 | Syndicate_Season1_Leadership2 | min=4, max=6 |
| 10 | 7 | Syndicate_Season1_Leadership3 | min=7, max=10 |

Server matching (`SeasonService.cs:399`): `rank >= r.RankMin && rank <= r.RankMax` — impossible to satisfy when min > max. Rewards were never delivered.

Compounded by `MarkLeaderboardDelivered` being called unconditionally (`SeasonService.cs:403`) even when no reward matched. All participants have `leaderboard_reward_delivered = 1`, blocking any automatic re-run.

### Fix

**Operator must apply immediately (SQL):**
```sql
-- Reset delivered flag
UPDATE season_character_points
SET leaderboard_reward_delivered = 0
WHERE season_id = (SELECT id FROM seasons WHERE name = N'Seasons, oh May!');

-- Fix swapped rank ranges
UPDATE season_leaderboard_rewards SET rank_min=1, rank_max=3
WHERE season_id=(SELECT id FROM seasons WHERE name=N'Seasons, oh May!') AND rank_min=3 AND rank_max=1;
UPDATE season_leaderboard_rewards SET rank_min=4, rank_max=6
WHERE season_id=(SELECT id FROM seasons WHERE name=N'Seasons, oh May!') AND rank_min=6 AND rank_max=4;
UPDATE season_leaderboard_rewards SET rank_min=7, rank_max=10
WHERE season_id=(SELECT id FROM seasons WHERE name=N'Seasons, oh May!') AND rank_min=10 AND rank_max=7;
```

**Code changes required:**
1. New `SeasonRedeliverLeaderboardRewards` admin request handler — re-runs reward delivery for a past ended season by ID, respecting the `leaderboard_reward_delivered` flag.
2. Admin Tool validation in `SeasonDetailViewModel.QueueSaveLeaderboardReward` — guard `rank_min ≤ rank_max` before queuing the save.

### Notes
- `DeliverLeaderboardReward` writes to the redeemable items table via `InsertRedeemableItems` — no server restart needed once the command exists.
- The re-deliver command must load leaderboard reward rows directly from the DB (not the in-memory cache, which is cleared at season end).

---

## ISSUE-024 - AutoMarket pricing structurally excludes player crafters from the production economy

Status: DONE
Priority: CRITICAL
Area: Market / Economy

### Problem
AutoMarket's raw material buy prices are designed to be the best on the market, which means farmers preferentially sell to AutoMarket rather than to player crafters. Crafters who need raw materials are left with two unviable options: outbid AutoMarket for farmer supply (unsustainable) or buy from AutoMarket's own raw material sell orders at 2× production cost.

At 2× production cost for inputs, crafters cannot profitably undercut AutoMarket's production sell orders, which are priced at exactly 1× production cost. This makes player crafting economically non-viable. AutoMarket ends up as both the dominant raw material buyer and the dominant production item seller, with no player-to-player trade in either segment.

### Impact
- Player crafters have no viable economic role when competing against AutoMarket.
- The raw material market is dominated by AutoMarket; farmer → crafter trade does not develop.
- The production market stabilizes at AutoMarket's prices with no player undercutting possible.
- NIC injection via raw material purchases is currently uncapped (only plasma purchases have a daily budget cap), creating an inflation risk as AutoMarket absorbs all farming output.
- Economy health degrades to a two-step loop (farmer → AutoMarket → buyer) with no value-add player layer.

### Proposed Fix
Three levers, in order of impact:

1. **Add a margin to production sell prices** — sell production items at production cost × 1.2–1.3 instead of exactly 1×. This creates headroom for crafters who source materials below AutoMarket's buy price to profitably undercut. Lowest implementation cost: one config parameter.

2. **Reduce raw material sell markup from 2× to ~1.3×** — crafters buying from AutoMarket's sell orders at 1.3× can still craft and sell below AutoMarket's marked-up production prices, creating a viable crafter niche even without direct farmer supply.

3. **Add production item buyback orders** — AutoMarket posts buy orders for production items at ~0.85× production cost. Gives crafters a guaranteed exit price, making crafting economically viable in thin player markets and creating a NIC sink that scales with production volume. Largest implementation effort but highest long-term impact.

The minimum viable fix is (1) + (2) as config-only changes. Adding (3) is the complete solution.

### Notes
- Root cause is that AutoMarket is positioned as a market maker (best price) rather than a backstop (last resort). The gap between AutoMarket prices and fair value should be where player trade operates.
- AutoMarket does not currently buy production items back from players.
- The 24h price refresh lag creates an arbitrage window but does not address the structural problem.
- Cap raw material purchase budget similarly to the plasma budget (`daily_plasma_budget_nic`) to prevent unbounded NIC injection.

---

## ISSUE-023 - Editing existing Season objectives does not save 'Is Daily' flag changes

Status: DONE
Priority: CRITICAL
Area: Seasons / Admin Tool

### Problem
When an admin edits an existing objective on an existing Season and changes the 'Is Daily' flag, the change is not persisted. The flag reverts to its previous value after saving, leaving the objective in an incorrect state with no feedback to the admin.

### Impact
Admins cannot correct the daily/non-daily designation of objectives on live seasons. This blocks fixing misconfigured objectives without deleting and recreating them, which is disruptive and may affect active participant progress.

### Proposed Fix
- Locate the save path for objective edits in the Season Admin Tool (likely `SeasonDetailViewModel` or equivalent objective edit command).
- Verify that `IsDaily` is included in the change set sent to the server when building the objective update payload.
- Confirm the server-side handler and repository update include the `is_daily` column in the `UPDATE` statement.
- Fix whichever layer is dropping the field (UI binding, change-set builder, or SQL update).

### Notes
- Reproduces on existing seasons with existing objectives; new objectives are unconfirmed.
- Check whether other boolean flags on objectives (e.g. `IsActive`, visibility flags) are similarly dropped — the root cause may affect a wider set of fields.

---

## ISSUE-022 - Season activity points awarded on market orders that are immediately cancelled (exploit)

Status: DONE
Priority: CRITICAL
Area: Seasons / Activities / Market

### Problem
A player can place a buy order on the market and immediately cancel it, yet still receive season activity points for the order placement. The same exploit likely applies to sell orders and potentially other NIC-related market actions. This allows instant, repeatable season progression with no actual economic commitment.

### Impact
Players can exploit this to gain unlimited season points with zero cost (place order, cancel, repeat). This undermines season integrity, devalues legitimate progression, and constitutes a confirmed exploit that must be addressed before widespread abuse occurs.

### Proposed Fix
Two candidate approaches, in order of preference:

1. **Award points only on order fulfillment** — move the activity hook from order placement to order execution (when the trade actually settles). This is the correct semantic fix: a fulfilled trade represents real economic activity.
2. **Award points only on non-cancelled orders** — on cancellation, reverse or forfeit any points that were awarded at placement time. More complex; requires tracking awarded points per order.

The fastest mitigation is to not credit activity at order placement at all, only at fulfillment. Investigate whether sell orders and other NIC actions share the same vulnerability (likely yes — audit all market-related activity hooks).

### Notes
- Confirmed for buy orders; sell orders and other NIC actions are suspected but unconfirmed.
- Cross-reference `ISSUE-020` (NIC spend activity for market purchases) — the fix for that issue and this one likely share the same hook call site.
- Audit all activity hooks triggered by market events to scope the full surface area.
- Fixed by removing `buyOrderDeposit` (NicSpent) and `buyOrderPayBack` (NicEarned) from
`CharacterWallet.OnCommited`. `TransportAssignmentSubmit` double-count also fixed in the
same change. NicSpent for actual market fulfillments is unaffected (handled by explicit
hooks in `Market.cs`).

---

## ISSUE-021 - NPC fleeing state speed reduction insufficient or not applied

Status: DONE
Priority: HIGH
Area: NPC AI / Combat

### Problem
Players report that NPCs in a fleeing state still move too fast. The expected maximum speed while fleeing is 75% of normal, but the reduction may be set too high or may not apply at all. Target value is 50% of normal max speed.

### Impact
NPCs can outrun or evade players while fleeing more effectively than intended, undermining combat balance and player experience.

### Proposed Fix
- Locate where the fleeing state applies a speed modifier to NPCs.
- Verify the modifier is actually applied during fleeing (not silently skipped).
- Change the maximum speed cap for the fleeing state from 75% to 50%.
- Add a code-level assertion or log that confirms the modifier is applied when an NPC enters the fleeing state.

### Notes
Validate by tracing the NPC state machine: confirm the fleeing state handler sets the speed modifier and that the modifier reaches the movement/speed calculation layer.
Check whether other states (e.g. roaming, chasing) use a similar modifier pattern and could be used as a reference.

---

## ISSUE-020 - NIC Spend activity not tracked for market purchases

Status: DONE
Priority: CRITICAL
Area: Seasons / Activities

### Problem
The `NIC spend` daily objective does not credit points when a player buys an item on the market. A player bought an item costing over 1,000,000 NIC (activity rate: 1 pt per 10,000 NIC), the objective was active, buyer and seller had different IPs, but no completion announcement was made and no points were awarded.

### Impact
The `NIC spend` objective is silently broken for market purchases. Players cannot progress through or complete this daily objective, undermining season participation and reward integrity.

### Known Facts
- Objective is configured and active.
- Rate: 1 point per 10,000 NIC.
- Purchase amount: >1,000,000 NIC (should yield >100 points).
- Buyer and seller had different IPs (rules out self-trade suppression as cause).
- No completion announcement fired, confirming zero points were awarded.

### Proposed Fix
- Locate where market buy orders are fulfilled and identify where (or whether) the `NIC spend` activity hook is called.
- Verify the hook call site passes the correct player, amount, and activity type.
- Check if the activity tracking filters out market transactions (e.g. self-trade guard, zone guard, or missing call entirely).
- Add the missing hook call or fix the incorrect filtering so NIC spent on market purchases is credited.

### Notes
Cross-reference the `DamageDone` and `NPC kill` activity paths to understand the expected hook pattern.
Check whether the `NIC spend` hook is also missing for other spend types (crafting, repair, etc.) — this may be a broader gap.

---

## ISSUE-004 - Avg. Points / Day shows negative values in Seasons Participation Health

Status: TODO
Priority: LOW
Area: Seasons / Admin Tool

### Problem
The "Avg. Points / Day" metric on the Seasons Participation Health view can display negative values, which is not a meaningful state for an average daily point rate.

### Impact
Negative values are confusing to operators and indicate a calculation or data bug — they erode trust in the health dashboard and may mask real participation trends.

### Proposed Fix
- Locate the query or computation that produces the Avg. Points / Day value.
- Identify the root cause: likely a division involving an elapsed-day count that can be zero or negative (e.g. when the season hasn't started yet, or when date arithmetic produces an unexpected sign).
- Guard against zero or negative elapsed days in the divisor — clamp to a minimum of 1 day or return `null`/`0` when no meaningful average can be computed.
- Ensure the displayed value is floored at zero; negative output should never reach the UI.

### Notes
Check whether the issue occurs only before/at season start or also mid-season.
If the underlying data (total points) can itself be negative due to a separate bug, that should be treated as a distinct issue and not masked by clamping here.

---

## ISSUE-006 - DamageDone not credited to player when attacking via RCC

Status: TODO
Priority: LOW
Area: Seasons / Activities

### Problem
When a player controls a Remote Controlled Creature (RCC), damage attributed to the RCC arrives in `Unit.OnDamageTaken` with `source` set to the `RemoteControlledCreature` instance, not the controlling `Player`. The `source is Player` check does not match, so the controlling player receives no `DamageDone` season credit for RCC damage.

### Impact
Players using RCCs in combat cannot accumulate `DamageDone` season points. This is a known limitation of the current implementation — a low-impact gap since RCC usage is a niche playstyle.

### Proposed Fix
Resolve the RCC owner player via the zone (similar to how the NPC kill path uses `Zone.ToPlayerOrGetOwnerPlayer`). This requires zone context at the damage attribution point, which is not available in `Unit.OnDamageTaken`. Options: override `OnDamageTaken` in `RemoteControlledCreature` to resolve owner, or add owner resolution to the `Unit` base class using a virtual property.

### Notes
The NPC kill path in `Npc.cs` handles this via `Zone.ToPlayerOrGetOwnerPlayer` — use that as a reference for the resolution approach.
Do not fix until the design decision is made: should RCC damage count toward `DamageDone`?

---

## ISSUE-007 - Recurring season detail view allows saving invalid RecurrenceGapDays

Status: TODO
Priority: LOW
Area: Seasons / Admin Tool

### Problem
The Season Detail View does not validate `RecurrenceGapDays` before saving. An admin can set `RecurrenceGapDays` to 0, null, or negative while `IsRecurring = true` and commit the change. This produces a `recurrence_gap_days` value in the DB that would cause `CloneSeasonForNextIteration` to throw (or create a zero-gap clone, spawning the next iteration with the same start/end time).

### Impact
Low — requires a deliberate bad edit via the Admin Tool. A guard added in IMPROVEMENT-001 ensures `CloneSeasonForNextIteration` throws an `InvalidOperationException` rather than silently corrupting data, but the UX would be poor.

### Proposed Fix
Add a `SaveGeneral` guard in `SeasonDetailViewModel`: if `Season.IsRecurring && (Season.RecurrenceGapDays == null || Season.RecurrenceGapDays < 1)`, show a validation message and block the save. Alternatively, enforce in `SeasonChanges.BuildUpdate` by refusing to write the change if the constraint is violated.

### Notes
Introduced by IMPROVEMENT-001 (Recurring Seasons). The wizard already validates this (gap must be ≥ 1 day), but the detail view has no equivalent guard.
See `SeasonDetailViewModel.cs` `SaveGeneral` command for the save entry point.
