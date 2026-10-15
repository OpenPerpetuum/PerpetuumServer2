# Sparky Live Knowledge Snapshots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. The user previously chose Native execution. Use checkbox (`- [ ]`) syntax to track steps.

**Goal:** Deliver bounded, profile-isolated season snapshots from the game server to Sparky so it can answer current-season and archived public leaderboard questions using fresh, traceable evidence.

**Architecture:** The game server builds immutable season snapshots from `SeasonService`'s five-minute cache and its existing hourly/final ranking calculations. A non-blocking publisher sends changed snapshots and idempotent season-finalization records over the current authenticated reverse tunnel. The AI service validates and stores these records in its own SQLite database, publishes lock-free per-profile views for retrieval, and passes selected season evidence through the existing single-model-call answer path. The Admin Panel exposes freshness and trace diagnostics and a Dev-only synthetic fixture for replay.

**Tech Stack:** .NET 8 / C#, ASP.NET Core minimal APIs, `System.Text.Json`, SQLite (`Microsoft.Data.Sqlite`), existing bounded `Channel<T>` worker, existing Sparky Admin SPA, existing HTTPS and restricted reverse SSH tunnel.

**Spec:** `docs/superpowers/specs/2026-10-15-sparky-live-knowledge-snapshots-design.md`

## Global Constraints

- The game server adds no per-question season database query and no blocking database work to its update loop.
- Snapshots are built from cached season data and ranking calculations the server already performs; publication uses immutable reference replacement.
- Use the existing authenticated game-to-AI HTTPS route and restricted reverse SSH tunnel. Add no public listener, inbound port, AI callback, or second model call.
- Current snapshots expire after 15 minutes by default; `SeasonService` refreshes its cache every five minutes. The age limit is editable per Admin Panel profile, with a hard range of 5–1440 minutes.
- Main writes only to Dev and VDI writes only to Live. The authenticated token determines the server-side profile; the submitted profile must match it.
- Export only public season configuration and public top tens. Never serialize character/account IDs, private progress, claims, private messages, or caller identity.
- Store current snapshots as replaceable state and season finals as durable, idempotent records. Static knowledge bundles remain separate.
- Live facts are retrieved evidence, not prompt instructions, profile memory, or model-authored knowledge. Ignore stale current state and continue to use valid archived results.
- Main has no season rows. Dev verification uses a synthetic fixture in replay and does not seed or alter Main's database.
- Real data extraction is verified only after a planned VDI deployment and game-server restart. This plan does not authorize that deployment.
- Do not commit changes unless the user explicitly asks; project guidance prohibits unrequested commits.

## File Map

| File | Responsibility in this feature |
|---|---|
| `src/Perpetuum/Services/Sparky/SparkyLiveKnowledgeModels.cs` | Game-side wire records for current snapshots and finalization updates. |
| `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeModels.cs` | AI-side matching wire records, profile view, acknowledgement, and diagnostics records. |
| `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeValidator.cs` | Strict schema, field-bound, profile, timestamp, and content-hash validation. |
| `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeStore.cs` | Durable SQLite persistence for current profile/provider state and finalized seasons. |
| `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeCatalog.cs` | Lock-free immutable profile views loaded from and updated with the store. |
| `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgePassageBuilder.cs` | Converts typed public season facts to bounded, source-attributed retrieval passages. |
| `src/Perpetuum/Services/Sparky/SparkyLiveKnowledgeOutbox.cs` | Coalesces current-state updates and durably queues finalization events for retry. |
| `src/Perpetuum/Services/Seasons/SparkySeasonKnowledgeSnapshotBuilder.cs` | Converts already-cached season models and public rankings into wire records. |
| Existing Sparky client/coordinator files | Send updates asynchronously over the existing tunnel and preserve answer-worker behavior. |
| Existing AI HTTP, retrieval, profile, trace, replay, and Admin SPA files | Ingest, retrieve, trace, and evaluate the new evidence without changing static bundle semantics. |
| `docs/codebase/ARCHITECTURE.md`, `docs/codebase/INTEGRATIONS.md`, `docs/codebase/STRUCTURE.md`, `src/Perpetuum.ItemStatsExporter/README.md` | Record ownership, transport, storage, operations, and deployment boundaries. |

## Review Focus

1. An unknown schema, bad hash, oversized payload, future timestamp, or profile mismatch must be rejected without replacing the last valid snapshot. (Tasks 1–2.)
2. Missing or stale current state must not answer a current-season question as though it were current; archived final standings must remain usable. (Tasks 2–3.)
3. Prompt-like or oversized display text must remain bounded, untrusted evidence, and no private identifiers may enter serialized payloads or traces. (Tasks 1, 3, and 5.)
4. Repeated finalization must be idempotent; a newer authorized correction must be traceable; a pending finalization must survive game-process restart. (Tasks 2 and 4.)
5. Concurrent snapshot publication and answering must see one complete immutable view, and Dev fixture/profile data must never leak into Live. (Tasks 2, 3, and 6.)

---

### Task 1: Define and validate the season snapshot contract

**Files:**
- Create: `src/Perpetuum/Services/Sparky/SparkyLiveKnowledgeModels.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeModels.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeValidator.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgeValidatorTests.cs`

**Interfaces:**
- The two model files define JSON-compatible records with matching web-camel-case fields. The game and exporter projects cannot share a source assembly, so keep the wire record shapes mirrored and verify them with serialization tests.
- Define `SparkyLiveKnowledgeUpdate(int SchemaVersion, string Provider, string Profile, string Operation, DateTimeOffset CapturedAtUtc, string ContentHash, SparkySeasonCurrentState? Current, SparkySeasonFinalization? Finalization)`.
- Use `Provider = "seasons"`, `Operation = "replace-current"` with `Current` set and `Finalization = null`; `Operation = "refresh-current"` with both payload properties null; or `Operation = "finalize-season"` with `Finalization` set and `Current = null`.
- `SparkySeasonCurrentState(SparkySeasonFacts? ActiveSeason, IReadOnlyList<SparkySeasonStanding> CurrentTopTen)` contains cached active facts and the latest already-computed public top ten. `ActiveSeason = null` and an empty list means off-season; it does not mean “keep the old active season.”
- `SparkySeasonFacts` contains `SeasonId`, `Name`, `Description`, UTC start/end, scoring mode, and typed activity-rate, objective, and tier lists.
- `SparkySeasonFinalization` contains the season key, name, end time, and at most ten `SparkySeasonStanding` records (`Rank`, `DisplayName`, `TotalPoints`). No record contains character or account IDs.
- Current facts use typed lists for activity rates, objectives, and tiers. Do not add arbitrary JSON payloads; a future provider requires a new typed section and validator branch.
- `SparkyLiveKnowledgeValidator.ValidateUpdate(update, authenticatedProfile, nowUtc)` returns a validated update or a specific rejection code. Enforce schema version 1, known provider/operation, profile match (`dev`/`live`), a 256 KiB serialized-body cap, UTC capture time no more than five minutes in the future, 64-hex SHA-256 content hash, at most 32 activity rates, 128 objectives, 32 tiers, ten standings, and field-length caps (season/objective names 120 characters; descriptions 2,000; display names 100).
- Compute a current semantic hash from schema, provider, profile, and typed current content while excluding `Operation`, `CapturedAtUtc`, and `ContentHash`; compute a final semantic hash from schema, provider, profile, and finalization content while excluding `CapturedAtUtc` and `ContentHash`. A `refresh-current` update contains no season payload and is accepted only when its hash matches the persisted current record for that authenticated profile/provider.

- [x] Add validator cases for valid current/off-season/finalization/refresh payloads, wrong profile, unknown provider/schema/operation, payload over each bound, malformed hash, and future timestamp.
- [x] Run `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release --filter FullyQualifiedName~SparkyLiveKnowledgeValidatorTests`; confirm the new cases fail before implementation.
- [x] Add the mirrored game/exporter records with exact property order and names from the interface above.
- [x] Implement deterministic SHA-256 calculation over provider/profile/schema and typed content, excluding `Operation`, `CapturedAtUtc`, and `ContentHash` for current snapshots; exclude `CapturedAtUtc` and `ContentHash` for finalizations. Require full payload hashes to match; verify metadata-only refreshes by exact equality with the stored hash.
- [x] Make validator rejection codes stable (`invalid_schema`, `invalid_profile`, `invalid_payload`, `invalid_hash`, `invalid_timestamp`, `payload_too_large`) and rerun the focused test command.
- [x] Add a JSON round-trip assertion proving the game DTO serializes into the exporter DTO with all optional sections preserved.

### Task 2: Persist and publish profile-scoped snapshots in the AI service

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeStore.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeCatalog.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgeStoreTests.cs`

**Interfaces:**
- `SparkyLiveKnowledgeStore(string databasePath)` owns a separate SQLite file beside the configured trace database; by default it is `%ProgramData%\Sparky\traces\live-knowledge.db`. Do not add snapshot rows to trace-retention tables.
- Implement `InitializeAsync(CancellationToken)`, `ReplaceCurrentAsync(SparkyApiProfile profile, SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`, `FinalizeSeasonAsync(SparkyApiProfile profile, int routeSeasonId, SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`, and `LoadProfileAsync(SparkyApiProfile profile, CancellationToken cancellationToken)`.
- Implement `RefreshCurrentAsync(SparkyApiProfile profile, SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`; it updates only the current record capture time when the content hash matches, and otherwise returns `current_missing` or `hash_mismatch` so the game resends the full snapshot.
- Return `SparkyLiveKnowledgeApplyResult(string Status, string ContentHash, int? Revision)` from write methods.
- SQLite tables: `LiveKnowledgeCurrent(Environment, Provider, SchemaVersion, CapturedAtUtc, ContentHash, PayloadJson)` with primary key `(Environment, Provider)`; `LiveSeasonFinal(Environment, SeasonId, Revision, ContentHash, CapturedAtUtc, PayloadJson)` with primary key `(Environment, SeasonId, Revision)`.
- Duplicate current hash returns an idempotent content acknowledgement and advances only the persisted freshness timestamp when the submitted capture time is newer. An older capture timestamp is rejected. Duplicate finalization hash is idempotent. A newer finalization for the same season inserts a new monotonically increasing revision; previous revisions remain queryable for audit while retrieval returns only the newest revision.
- `SparkyLiveKnowledgeCatalog` exposes `Task InitializeAsync(CancellationToken)`, `Task<SparkyLiveKnowledgeApplyResult> ApplyAsync(SparkyApiProfile profile, SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`, and `SparkyLiveKnowledgeReadView GetReadView(SparkyApiProfile profile, DateTimeOffset nowUtc, TimeSpan maximumAge)`. It loads once at startup, writes durably before publishing a new immutable profile view, and uses atomic reference replacement for lock-free reads.
- Add `Task<SparkyLiveKnowledgeApplyResult> RefreshCurrentAsync(SparkyApiProfile profile, SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)` to the catalog; it publishes a new profile freshness timestamp without changing the cached supplement index.
- Define `SparkySeasonFinalRecord(int SeasonId, int Revision, string ContentHash, DateTimeOffset CapturedAtUtc, SparkySeasonFinalization Finalization)` and `SparkyLiveKnowledgeProfileState(SparkyLiveKnowledgeUpdate? Current, IReadOnlyList<SparkySeasonFinalRecord> FinalizedSeasons)` as the validated stored view loaded from SQLite. The `Current.CapturedAtUtc` value comes from the current table's freshness timestamp, updated by heartbeat, not from an older payload JSON copy.
- The catalog rebuilds its immutable supplemental passage index only on startup or accepted semantic content changes. Same-hash timestamp advances update freshness metadata without rebuilding the index. `GetReadView` marks the current source excluded when stale without rebuilding the index; finalized season sources remain available.
- Add authenticated API routes `PUT /v1/live-knowledge/current` and `POST /v1/live-knowledge/seasons/{seasonId:int}/finalize`. Resolve profile from the bearer token with the existing credential resolver; require the body profile to match. Read request bodies with the existing bounded-body helper and strict JSON options.
- Add authenticated `POST /v1/live-knowledge/current/refresh` for metadata-only heartbeat updates; it stores no payload and never changes the search index.
- Keep the snapshot body cap at 256 KiB and raise the default `SPARKY_MAX_REQUEST_BODY_BYTES` from 131072 to 262144 so normal installs accept snapshots without an extra settings edit. Continue honoring an operator-configured smaller cap.

- [x] Add SQLite repository cases proving current replacement is profile-scoped, matched heartbeat advances freshness without changing payload/hash, mismatched heartbeat leaves state unchanged, identical finalization is idempotent, older updates cannot roll back newer state, and final records survive reopening the database.
- [x] Run the focused `SparkyLiveKnowledgeStoreTests` filter and observe expected failures.
- [x] Create the dedicated SQLite schema with `WAL`, `synchronous=FULL`, foreign keys, and bounded busy timeout matching the existing trace-store policy.
- [x] Set the default `SPARKY_MAX_REQUEST_BODY_BYTES` to 262144 and ensure Kestrel's global maximum is at least the configured request cap, trace-ingest cap, and live-knowledge cap.
- [x] Implement transactional current replacement and finalization writes; update freshness time on a same-hash full replacement without rebuilding the search index, and reject a finalization whose route season ID does not equal the body season ID.
- [x] Implement `RefreshCurrentAsync` as an atomic capture-time-only update keyed by profile/provider/hash; test that an unknown or mismatched hash leaves the existing timestamp unchanged.
- [x] Implement catalog initialization and publish only after a committed write; retain the previous view if validation or persistence fails.
- [x] Create the trace database directory if needed and initialize the catalog in `SparkyHttpService.RunAsync` at `Path.Combine(Path.GetDirectoryName(traceDatabasePath), "live-knowledge.db")`. If the new store is unavailable, log a warning and keep ordinary static-bundle answers available without live evidence.
- [x] Map all three routes after authentication, return bounded JSON acknowledgements, and confirm unauthenticated calls receive 401 while the existing `/health` behavior remains unchanged.

### Task 3: Retrieve season evidence, enforce freshness, and expose diagnostics

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgePassageBuilder.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeRetriever.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyTraceEventValidator.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyDiagnosticExporter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyProfileSnapshot.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyProfileValidator.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyTraceModels.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyTraceStore.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/index.html`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/app.js`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgePassageBuilderTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgeRoutingTests.cs`

**Interfaces:**
- Add `SparkyLiveKnowledgeSettings(int CurrentSnapshotMaxAgeMinutes = 15)` to `SparkyProfileDraft`; validate 5–1440 minutes and edit it in the profile editor.
- Define `SparkyKnowledgeSupplement(IReadOnlyList<SparkyKnowledgePassage> Passages, SparkySearchIndex SearchIndex)` and `SparkyLiveKnowledgeReadView(SparkyKnowledgeSupplement Supplement, IReadOnlySet<string> ExcludedSourceIds, SparkyLiveKnowledgeDiagnostics Diagnostics)`.
- `SparkyLiveKnowledgePassageBuilder.BuildSupplement(SparkyLiveKnowledgeProfileState state)` creates pre-indexed current and historical passages. `SparkyLiveKnowledgeCatalog.GetReadView(profile, nowUtc, maximumAge)` returns that cached supplement plus the current freshness decision and excluded stale source IDs.
- Emit stable IDs `live-season-current` and `live-season-final-{seasonId}`; use category `mechanics` and typed source revision/hash. Do not include internal character/account IDs or arbitrary request text.
- Append optional `SparkyLiveKnowledgeReadView? liveKnowledge = null` after `sourceCategories` in `SparkyKnowledgeRetriever.Search(string playerQuery, string englishQuery, int maxPassages, int maxCharacters, IReadOnlyList<string>? mentionedEntities, bool prioritizeEntityDefinitions, double minimumRelevanceScore, IReadOnlyList<string>? sourceCategories, SparkyLiveKnowledgeReadView? liveKnowledge)`; filter `ExcludedSourceIds` without rebuilding the supplement index.
- Append optional `SparkyLiveKnowledgeReadView? liveKnowledge = null` after `callerRank` in `SparkyAnswerRouter.AnswerAsync(string question, SparkyQuestionInterpretation interpretation, SparkyKnowledgeBundle bundle, string? contextItem, int? contextLanguageId, CancellationToken cancellationToken, IReadOnlyList<SparkyConversationTurn>? contextTurns, string? contextSummary, SparkyPendingClarification? pendingClarification, SparkyCallerRank callerRank, SparkyLiveKnowledgeReadView? liveKnowledge)` and pass it through private `AnswerCoreAsync` to every mechanics/lore retrieval call; leave CLI calls unchanged when it is null.
- Add trace diagnostics to `SparkyTraceEvent` for provider, schema, accepted hash, capture age, and freshness decision (`fresh`, `stale`, `missing`, `rejected`); selected source IDs already exist. Do not add these fields to `SparkyTraceEventRequest`, and do not log full snapshot bodies or reasoning text.

- [x] Add passage-builder cases for active season, off-season, current leaderboard, historical top tens, stale current data, and malicious prompt-like labels remaining quoted evidence.
- [x] Add routing cases proving a current question selects fresh `live-season-current`, a champion question selects the matching finalized source, stale current data is excluded, and archived final data remains searchable.
- [x] Run the two focused test filters and observe failures before implementation.
- [x] Add the typed passage builder and build its supplemental token index only when the catalog publishes a new content hash.
- [x] Extend the retriever to score supplemental passages using the existing query tokens, category filter, relevance threshold, and passage/character budgets.
- [x] Have `/v1/answer` call `GetReadView(profile, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(activeProfile.LiveKnowledge.CurrentSnapshotMaxAgeMinutes))` once and pass that same view to the router and trace event.
- [x] Require the generated answer to cite only selected season evidence under the existing answer prompt; if no fresh evidence exists, use the existing grounded “cannot verify current state” behavior rather than stale facts.
- [x] Add the per-profile current-age setting to default profile creation, profile validation, profile serialization, and Admin profile editor. Existing revisions deserialize with the default 15-minute value.
- [x] Add additive SQLite trace columns for live provider/hash/age/freshness; display them in the trace details pane and keep existing selected-source IDs visible.
- [x] Extend `SparkyTraceEventValidator` bounds and `SparkyDiagnosticExporter` serialization for the new diagnostics so trace ingestion, export, and Admin display agree on the same fields.
- [ ] Rerun the focused filters and inspect a trace from a fresh current snapshot and one from a stale snapshot.

### Task 4: Add the asynchronous publisher and durable finalization outbox

**Files:**
- Create: `src/Perpetuum/Services/Sparky/SparkyLiveKnowledgeOutbox.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyApiClient.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyClientOptions.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyRequestCoordinator.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgeOutboxTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgeClientTests.cs`

**Interfaces:**
- Derive API paths from the validated existing `https://127.0.0.1:<port>/v1/answer` URI; do not add a new configuration URL or listener.
- Add `SparkyApiClient.PublishCurrentAsync(SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`, `RefreshCurrentAsync(SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`, and `PublishFinalizationAsync(SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)`; all use the existing bearer token, strict bounded JSON, and the existing request timeout policy.
- Add `bool SparkyRequestCoordinator.TryPublishCurrentSnapshot(SparkyLiveKnowledgeUpdate update)` and `bool TryPublishSeasonFinalization(SparkyLiveKnowledgeUpdate update)`. They enqueue work without waiting on HTTP and never use the answer worker's caller lanes. Expose `string? Profile => _options?.Profile` so the season service uses only the profile from protected client settings.
- The current update channel has capacity one and coalesces to the newest snapshot. Finalizations enter a bounded in-memory channel without file I/O on the season update thread; the publisher worker appends each to `%GameRoot%\logs\sparky-live-knowledge-outbox.jsonl` before any HTTP attempt and retries until the AI service acknowledges it. Cap the file at 2 MiB / 256 finalizations; if full, preserve existing records, reject the new record, and log an error without disrupting season processing.
- On coordinator start, load outbox records and resume retries. On acknowledgement, atomically rewrite the outbox without that record. Derive the game profile from the same protected client settings already used by answers.
- When a new semantic hash is present or not yet acknowledged, send `replace-current`; when the hash is unchanged, send `refresh-current` with no season payload. If AI replies `current_missing` or `hash_mismatch`, immediately resend the latest full current snapshot.
- Retry transient transport/server failures with exponential delays starting at five seconds and capped at five minutes; reset delay after any successful acknowledgement. Treat 4xx validation/profile errors as permanent for that payload and log one error per content hash until a new hash arrives.

- [x] Add outbox tests for append/reload/acknowledge, duplicate finalization IDs, atomic rewrite, bounded full-file behavior, and retaining unacknowledged records after simulated restart.
- [x] Add client tests asserting current and finalization routes use the existing authority, bearer token, body caps, and cancellation behavior.
- [x] Run the two focused test filters and observe failures.
- [x] Implement the JSON-lines outbox with write-through temp file plus atomic replace, stable finalization keys, and no private identifiers; serialize append and acknowledgement rewrites under one lock.
- [x] Add all three publisher methods to `SparkyApiClient`; accept only 2xx acknowledgements with the matching operation and content hash.
- [x] Make the publisher send a tiny heartbeat every five-minute cache tick when the semantic hash is unchanged; resend the full snapshot when the AI reports a missing/mismatched hash. Verify that three successive heartbeats keep an otherwise unchanged snapshot fresh beyond the 15-minute TTL.
- [x] Add the independent bounded publisher worker to `SparkyRequestCoordinator.Start/Stop`; coalesce current updates and drain/retry the finalization outbox without blocking `Update` or answer delivery. On `Stop`, close publication inputs, persist queued finals for up to the existing five-second shutdown grace, then cancel network retries.
- [ ] Log one concise status transition per failure/recovery, not a warning on every retry tick; verify the existing answer queue still accepts and completes questions while the AI endpoint is unavailable.

### Task 5: Build season snapshots from existing cached data and public rankings

**Files:**
- Create: `src/Perpetuum/Services/Seasons/SparkySeasonKnowledgeSnapshotBuilder.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkySeasonKnowledgeSnapshotBuilderTests.cs`

**Interfaces:**
- `SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(Season? season, IReadOnlyList<SeasonActivityRate> rates, IReadOnlyList<SeasonObjective> objectives, IReadOnlyList<SeasonTier> tiers, IReadOnlyList<SparkySeasonStanding> currentTopTen, DateTimeOffset capturedAtUtc, string profile)` returns a deterministic `replace-current` update. `season == null` represents off-season.
- `BuildFinal(Season season, IReadOnlyList<SparkySeasonStanding> finalTopTen, DateTimeOffset capturedAtUtc, string profile)` returns an idempotent `finalize-season` update.
- Builder inputs contain only already-loaded cache data or rankings already fetched by `AnnounceLeaderboard`/`ProcessSeasonEnd`; it cannot depend on `SeasonRepository` or execute SQL.
- `SeasonService` publishes its own immutable latest update reference after cache refresh and hourly leaderboard refresh. Inject `Func<SparkyRequestCoordinator>` for deferred coordinator resolution so season service construction does not create a bootstrap cycle.

- [x] Add builder cases for active/off-season, tier/objective/rate mapping, top-ten truncation, sorting/rank assignment, exact hash repeatability, and absence of player identifiers.
- [x] Run `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release --filter FullyQualifiedName~SparkySeasonKnowledgeSnapshotBuilderTests` and confirm failures.
- [x] Implement the pure builder using UTC timestamps and the contract's deterministic hash function.
- [x] In `RefreshCache`, build the current candidate after all season cache references have been replaced. If its hash changed, atomically publish it and queue a full update; if unchanged, retain the current reference and queue a metadata-only refresh with the new capture time. Publish an explicit off-season update after the old state is cleared.
- [x] In `AnnounceLeaderboard`, reuse the already-filtered `rankings` list and names/points to replace the current top-ten section; do not query the database again for Sparky.
- [x] Publish the empty current top ten when the active season has no eligible participants, before the existing no-announcement early return, so an older top ten cannot remain presented as current.
- [x] In `ProcessSeasonEnd`, collect only the public top ten from the same filtered final ranking used for the existing “Seasons Info” announcement, and enqueue finalization before discarding cached season state.
- [x] Keep publication failures isolated from season announcements, reward delivery, and server update processing; confirm the finalization outbox receives the public result without any character IDs.

### Task 6: Add Dev-only fixture replay and operator-visible status

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyLiveKnowledgeFixtures.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyTraceModels.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyReplayService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyAdminEndpoints.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/index.html`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/app.js`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/styles.css`
- Test: `src/Perpetuum.Tests/Unit/SparkyLiveKnowledgeFixtureReplayTests.cs`

**Interfaces:**
- `SparkyLiveKnowledgeFixtures.CreateReadView(string fixtureId, DateTimeOffset capturedAtUtc)` returns fixture `season-sample-v1` containing one synthetic current season and two synthetic finalized seasons; it has no connection to game or database state.
- Add optional `LiveKnowledgeFixtureId` to `SparkyReplayRequest`. Accept only `null` or `season-sample-v1`; reject a fixture when the replay profile is Live.
- Capture one live evidence view at replay start and pass the same view to both Active and Draft for every turn, preventing a snapshot update from making the comparison inconsistent.
- Add `SparkyLiveKnowledgeDiagnostics? LiveKnowledge` to `SparkyReplayOutput` and render freshness and selected live-source IDs in each replay turn result.
- Add read-only `GET /admin/api/live-knowledge/status?environment=Dev|Live` returning provider, snapshot age/freshness, content hash, and completed-season count; it never returns full payloads.
- Add a Dev-only fixture selector to the replay panel and show current live-snapshot status for the selected profile. Fixture selection affects replay only; it never writes the fixture into Dev's persisted current snapshot.

- [x] Add replay cases proving fixture data is identical for Active/Draft, omitted from actual answer requests, and rejected for Live.
- [x] Run `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release --filter FullyQualifiedName~SparkyLiveKnowledgeFixtureReplayTests` and confirm failures.
- [x] Add deterministic synthetic facts with recognizable names and points, including an unambiguous current leader and a prior-season champion.
- [x] Add the Admin status endpoint using only catalog metadata and an allowlisted profile value.
- [x] Add the fixture selector to replay request serialization and form handling; disable it for Live and show a clear error if a manually crafted Live replay includes a fixture ID.
- [x] Add the profile-scoped status display with freshness states and hashes, but no snapshot body.
- [ ] In the Dev evaluation editor, create a two-turn case with “What is the current season?” requiring `live-season-current`, followed by “Who won the previous season?” requiring the fixture's `live-season-final-{seasonId}` source; select `season-sample-v1` in the fixture control.
- [ ] Replay the case against Active and a Draft revision; confirm the fixture snapshot is the same in both and no durable catalog rows change.

### Task 7: Document operations, Dev validation, and Live rollout boundary

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/README.md`
- Modify: `docs/codebase/ARCHITECTURE.md`
- Modify: `docs/codebase/INTEGRATIONS.md`
- Modify: `docs/codebase/STRUCTURE.md`

**Interfaces:**
- The README explains the new sibling `live-knowledge.db`, `%ProgramData%\Sparky\traces` access requirements, accepted routes, snapshot TTL, trace diagnostics, Dev fixture steps, and recovery behavior.
- Architecture and integration docs state that Main emits Dev snapshots, VDI emits Live snapshots, the existing tunnel carries updates, AI has no game DB access, and finals are public-only durable evidence.
- The deployment checklist separates AI-service publishing from game-server publishing and explicitly requires a planned Live game-server restart. It contains no instruction to open a new firewall port.

- [x] Update `ARCHITECTURE.md` with the game-provider → authenticated tunnel → AI catalog/retrieval flow and the separate static/live knowledge stores.
- [x] Update `INTEGRATIONS.md` with endpoint paths, token-derived profile enforcement, body bounds, retry/outbox behavior, and TTL semantics.
- [x] Update `STRUCTURE.md` with the new model, builder, outbox, store, catalog, and passage-builder ownership.
- [x] Update the exporter README with exact Dev fixture/replay instructions and how to read missing/stale/selected-source diagnostics.
- [x] State that season finals published after the VDI deployment are archived automatically; older results are not reconstructed from the live database by this release, so an operator-reviewed public-announcement backfill is required before Sparky can reliably answer questions about seasons that ended before deployment.
- [x] Review the docs against the implementation and ensure they do not imply that Main provides real season data or that the Live update has been deployed.

## Completion checks

- Run the focused tests listed in Tasks 1–6 and then `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release`.
- Run `dotnet build src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj -c Release` and the documented server build/publish command.
- In the Admin Panel, replay the synthetic Dev current-season and champion sequence and inspect both Active and Draft source IDs, freshness, and profile metadata.
- With Main debug running, verify it emits Dev current snapshots without season DB writes; query through Sparky and confirm no stale current facts are used.
- Simulate an unavailable AI endpoint, confirm normal game processing and answer delivery remain responsive, then confirm queued current state and finalization are acknowledged after recovery.
- Do not deploy to VDI or restart the Live game server in this plan. Prepare the published package and rollback instructions; schedule that operational step separately.
