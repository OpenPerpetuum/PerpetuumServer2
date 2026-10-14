# Sparky Admin Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local Sparky Admin Panel and end-to-end trace system that supports safe Dev/Live tuning, replay, feedback, and explicit promotion without routine code redeployment.

**Architecture:** Keep the panel as a browser UI separate from `Perpetuum.AdminTool`, hosted on a second HTTPS loopback listener in the existing Sparky Windows service. Main reaches that listener through a dedicated restricted reverse SSH tunnel; game API traffic stays on its current listener. Add a shared request ID, game-side durable metadata spool, AI-side SQLite trace store, profile-specific configuration and bundle versions, and validated hot reload.

**Tech Stack:** .NET 8, C# 12, ASP.NET Core, `Microsoft.Data.Sqlite` embedded store, JSONL local spool, Windows OpenSSH, existing WiX installer, vanilla HTML/CSS/JavaScript for the panel.

**Spec:** `docs/superpowers/specs/2026-10-14-sparky-admin-panel-design.md` (approved by the user on 2026-10-14).

## Global Constraints

- AI, Main, and Live are Windows x64 machines; AI hosts Sparky and the panel, Main hosts local debug testing, and Live is the VDM production host.
- Bind the game API and panel listeners to IPv4 loopback on AI. Do not add a public listener, router port forward, VPN, or third-party relay.
- Access the panel from Main through its own AI-initiated, restricted reverse SSH tunnel. Keep it separate from the game API tunnel and bearer credential.
- Authenticate Dev and Live by distinct protected bearer credentials; the credential selects the profile, not a request-body profile field.
- Dev and Live config revisions and active bundle versions are isolated. A Live change requires explicit promotion and remains reversible.
- Keep privacy, grounding, request bounds, domain calculations, and network restrictions code-owned. Config edits cannot execute scripts, SQL, arbitrary code, or unrestricted regular expressions.
- Do not send or store account IDs, character IDs, Discord user IDs, session state, API tokens, SSH key material, or hidden model reasoning in traces.
- Dev content capture defaults on; Live content capture defaults off. Proposed retention defaults are 30 days for question/answer content and 90 days for metadata; both remain configurable.
- Game-side spool records omit question and answer text. AI stores content only when the selected profile permits it.
- Bundle generation remains an offline Main-machine operation. Sparky and the panel never query the Live database.
- Preserve the existing one-profile service mode until Dev/Live settings have been provisioned. Legacy credentials must not be silently accepted as Live credentials.
- Preserve unrelated user changes in the working tree. Do not commit.
- Do not change or restart the Live VDM installation as part of this implementation plan; any production rollout requires a later explicit deployment decision.

## Review Focus

- A missing, malformed, or reused request/event ID must not merge unrelated requests or duplicate a trace. Pin ID validation and event deduplication in Tasks 1 and 2.
- A Dev token, trace, config edit, or bundle activation must never select or mutate Live. Pin profile-auth separation in Tasks 1 and 5.
- An invalid hot edit or invalid bundle must leave the active snapshot intact, while in-flight work completes with its original snapshot. Pin this in Task 5.
- A stopped tunnel, restarted server, duplicate upload, or full local spool must not block game processing or silently erase already-spooled events. Pin retry, idempotency, and overflow reporting in Tasks 3 and 4.
- The admin UI and API must not answer on the game API listener, bind to non-loopback addresses, or accept the game bearer token as admin authentication. Pin listener/auth separation in Task 4 and verify it manually in Task 7.

---

## File Map

### Game server

- Modify `src/Perpetuum/Services/Sparky/SparkyRequestCoordinator.cs` to create and propagate a GUID request ID and record queued/result events.
- Modify `src/Perpetuum/Services/Sparky/SparkyApiClient.cs` and `SparkyClientOptions.cs` to send that ID and use the profile-scoped bearer credential.
- Modify `src/Perpetuum/Services/EventServices/EventMessages/SparkyResponseMessage.cs` and `src/Perpetuum/Services/EventServices/EventProcessors/SparkyResponseProcessor.cs` to retain the trace ID through delivery and report delivery outcome.
- Create `src/Perpetuum/Services/Sparky/SparkyTraceEvent.cs`, `SparkyTraceSpool.cs`, and `SparkyTraceUploader.cs` for bounded local metadata journaling and retry.
- Modify `src/Perpetuum/GlobalConfiguration.cs` only for non-secret spool and profile settings; keep secrets in the protected settings file.

### AI service and panel

- Modify `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`, `SparkyHttpService.cs`, `SparkyServiceOptions.cs`, `SparkySettings.cs`, `SparkyTunnelSupervisor.cs`, and `Program.cs` for request correlation, profile resolution, trace ingestion, separate listeners, and tunnel configuration.
- Create `SparkyProfileManager.cs`, `SparkyProfileSnapshot.cs`, `SparkyProfileValidator.cs`, `SparkyTraceStore.cs`, `SparkyTraceModels.cs`, and `SparkyAdminAuthentication.cs` in `src/Perpetuum.ItemStatsExporter/`.
- Add static UI under `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/`; do not add a Node build or a runtime CDN dependency.
- Modify `SparkyQuestionInterpreter.cs`, `SparkyAnswerRouter.cs`, `LocalModelAnswerer.cs`, and `SparkyKnowledgeBundle.cs` only as needed to consume immutable profile snapshots and profile-selected bundles.
- Modify `src/Perpetuum.ItemStatsExporterInstaller/Perpetuum.ItemStatsExporterInstaller.wixproj`, `SparkyServiceComponents.wxs`, and `Package.wxs` only as needed to package the static panel, data directories, and service settings.
- Modify `tools/Configure-SparkyServiceSettings.ps1` and `tools/Configure-SparkyVdmClientSettings.ps1` to protect separate Dev, Live, and admin credentials/settings. Add `tools/Configure-SparkyAdminTunnel.ps1` only if the existing script structure cannot express the dedicated Main tunnel cleanly.

### Tests and docs

- Add focused unit tests under `src/Perpetuum.Tests/Unit/` for profile resolution, request ID propagation, event validation/deduplication, spool recovery, configuration validation, and replay isolation.
- Update `docs/codebase/ARCHITECTURE.md`, `docs/codebase/STRUCTURE.md`, `docs/codebase/INTEGRATIONS.md`, and `src/Perpetuum.ItemStatsExporter/README.md` after implementation behavior is stable.
- Create `docs/superpowers/sparky-admin-panel-operations.md` with AI/Main setup, the separate tunnel, profile setup, log review, retention, promotion, and rollback.

## Interfaces

Implement these contracts before consumers depend on them; keep equivalent JSON names and validation bounds on both sides of the game API:

```csharp
// Game request: profile is selected by its protected bearer token, not a body field.
internal sealed record SparkyApiRequest(
    Guid RequestId,
    string? Question,
    string? ContextItem = null,
    int? ContextLanguageId = null);

// Game-side metadata event. It intentionally has no caller/channel ID or chat content.
internal sealed record SparkyTraceEvent(
    Guid EventId,
    Guid RequestId,
    string Source,
    string Stage,
    string Outcome,
    DateTimeOffset RecordedAtUtc,
    long? ElapsedMilliseconds = null,
    string? DetailCode = null);

// AI ingress maps the authenticated credential to Environment; the caller cannot set it.
internal sealed record SparkyTraceEventRequest(
    Guid EventId,
    Guid RequestId,
    string Source,
    string Stage,
    string Outcome,
    DateTimeOffset RecordedAtUtc,
    long? ElapsedMilliseconds = null,
    string? DetailCode = null);
```

Additional service contracts:

- `SparkyTraceSpool.AppendAsync(SparkyTraceEvent traceEvent, CancellationToken cancellationToken)` serializes bounded events to local JSONL and durably flushes them before marking them ready to upload.
- `SparkyTraceUploader.RunAsync(CancellationToken cancellationToken)` uploads batches to `POST /v1/trace-events`, retries transient failures with bounded backoff, and removes only acknowledged event IDs.
- `SparkyTraceStore.AppendAsync(SparkyStoredTraceEvent traceEvent, CancellationToken cancellationToken)` inserts by unique `EventId` and is idempotent. Each stored event receives the authenticated environment on AI.
- `SparkyProfileManager.GetSnapshot(string environment)` returns an immutable active profile snapshot containing config revision, bundle version, route/retrieval/style settings, model settings, and content-capture policy.
- `SparkyProfileManager.TryActivateAsync(string environment, SparkyProfileDraft draft, CancellationToken cancellationToken)` validates first, atomically activates a new immutable revision, and leaves the old revision active on failure.
- `SparkyApiClient.AnswerAsync(Guid requestId, string question, string? contextItem, int? contextLanguageId, CancellationToken cancellationToken)` sends the trace ID and request body; it never sends game/Discord caller IDs.

## Task 1: Add request correlation and profile identity

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkySettings.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyRequestCoordinator.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyApiClient.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyClientOptions.cs`
- Modify: `src/Perpetuum/Services/EventServices/EventMessages/SparkyResponseMessage.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyRequestCorrelationTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyProfileCredentialResolverTests.cs`

**Interfaces:**
- Game requests use `SparkyApiRequest.RequestId: Guid`.
- `SparkyWorkItem` and `SparkyResponseMessage` carry that same GUID in addition to the existing local numeric sequence used in human-readable logs.
- The game protected client settings become version 2 with `profile` (`dev` or `live`) and the DPAPI-protected token. Version 1 remains readable only as legacy Dev configuration until migrated.
- AI resolves API credentials with fixed-time comparison and returns an environment/profile identity from the matched configured credential; unknown credentials return 401. The request body has no environment selector.

- [x] **Step 1: Check graph impact before editing existing service types**

  From the repository root, run and inspect the direct dependents before changing public/widely-used classes:

  ```powershell
  .\tools\query-graph.ps1 SparkyRequestCoordinator -Direction in
  .\tools\query-graph.ps1 SparkyApiClient -Direction in
  .\tools\query-graph.ps1 SparkyHttpService -Direction in
  .\tools\query-graph.ps1 GlobalConfiguration -Direction in
  ```

  The current graph lists direct nodes for `SparkyRequestCoordinator`, `SparkyApiClient`, and `SparkyHttpService`; record the call sites actually affected. `GlobalConfiguration` has a small graph result but is a broad settings object, so review all reads before adding fields.

- [x] **Step 2: Pin correlation and profile-auth behavior in focused tests**

  Add tests that assert a request carries one GUID unchanged through API serialization and response message creation; two simultaneous requests receive different GUIDs; malformed/empty GUID requests are rejected or server-generated only for legacy CLI callers; Dev credentials resolve only to Dev; Live credentials resolve only to Live; a Dev credential cannot select Live by adding a body property; and comparison failures reveal no secret.

- [x] **Step 3: Add the v2 protected client-settings model and preserve v1 migration**

  Update `SparkyClientOptions.TryCreate` to read `version = 2`, `profile`, and the protected token. Keep v1 loading as `Dev` only, and emit one explicit migration warning without printing token data. Reject unknown profile names and malformed DPAPI content without preventing unrelated server startup.

- [x] **Step 4: Generate and propagate one trace GUID**

  In `SparkyRequestCoordinator.Submit`, call `Guid.NewGuid()` once per accepted question and add it to `SparkyWorkItem`. Pass it to `SparkyApiClient.AnswerAsync` and include it in `SparkyResponseMessage`. Keep the existing numeric sequence for operator-readable local logs but never use it as the cross-host identity.

- [x] **Step 5: Extend the API request model and resolve environment from the bearer token**

  Add `RequestId` to the AI `SparkyApiRequest`, validate it, and use it for service trace events instead of relying only on `HttpContext.TraceIdentifier`. Preserve the existing CLI/manual API path by generating an AI-side GUID when the request omits it only in the explicitly supported legacy/manual request mode. Add per-profile token options with Dev and Live protected settings; keep the single legacy token path until the operator provisions v2 settings.

- [x] **Step 6: Build and run the correlation/profile unit tests**

  Run:

  ```powershell
  dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyRequestCorrelationTests|FullyQualifiedName~SparkyProfileCredentialResolverTests"
  ```

  Expected: all focused tests pass; the existing v1 protected settings fixture still resolves as Dev; no caller identity is serialized.

## Task 2: Persist AI traces and accept game-side event uploads

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyTraceModels.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyTraceStore.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyTraceStoreTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyTraceEventValidationTests.cs`

**Interfaces:**
- `SparkyTraceStore` owns the local AI SQLite file under `C:\ProgramData\Sparky\traces\` and exposes `AppendAsync`, `GetTraceAsync`, `QueryTracesAsync`, `AddFeedbackAsync`, and retention methods.
- `POST /v1/trace-events` accepts at most 100 `SparkyTraceEventRequest` records and uses the authenticated profile as the stored environment.
- AI-generated events use the same `RequestId` and include stage, outcome, elapsed time, intent/focus, selected source IDs, model ID, config revision, and bundle version as applicable.

- [x] **Step 1: Add SQLite dependency and event schema tests**

  Add `Microsoft.Data.Sqlite` to `Perpetuum.ItemStatsExporter.csproj`. Test unique `EventId` behavior, ordered request timelines, allowed source/stage values, maximum batch size, and bounded detail text. Reject unknown JSON fields and oversized bodies using the existing API serializer rules.

- [x] **Step 2: Implement the local trace store and schema initialization**

  Create the trace, feedback, evaluation-example, profile-revision, and audit tables with explicit columns and indexes on `RequestId`, `Environment`, `RecordedAtUtc`, and `EventId`. Enable SQLite WAL and a bounded busy timeout. Insert with an `EventId` uniqueness constraint so retrying the same event is harmless. Store question/answer content only in the AI trace row when the matched profile enables content capture.

- [x] **Step 3: Add bounded trace-event ingestion**

  Map `POST /v1/trace-events` on the game API listener. Require the same profile credential as `/v1/answer`, cap batch size and content length, validate all enums/lengths, assign environment from authentication, and return acknowledged event IDs only after durable store commit. Do not accept caller IDs or a caller-selected environment.

- [x] **Step 4: Persist AI request, interpretation, retrieval, and answer stages**

  Before inference, append request-received metadata and conditionally capture the question. After interpretation, append intent, focus, normalized query, and entity candidates. After routing, append selected source IDs, route/outcome, model ID, bundle/config revisions, finish status, and elapsed time. Before returning an answer, append the final outcome and conditionally capture the final formatted answer. If telemetry storage fails, log a redacted diagnostic and keep answering when inference itself is available.

- [x] **Step 5: Verify idempotency, profile separation, and failure isolation**

  Run the Task 2 unit tests, then add a local API check that sends the same event twice and observes one stored row; sends it under Dev and observes Dev; then attempts with the Live token and observes Live. Verify unauthorized requests and invalid events do not create rows, and a store write failure does not return SQLite exception details or leak content into logs.

## Task 3: Add the game-side local spool and delivery events

**Files:**
- Create: `src/Perpetuum/Services/Sparky/SparkyTraceEvent.cs`
- Create: `src/Perpetuum/Services/Sparky/SparkyTraceSpool.cs`
- Create: `src/Perpetuum/Services/Sparky/SparkyTraceUploader.cs`
- Modify: `src/Perpetuum/GlobalConfiguration.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyRequestCoordinator.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyApiClient.cs`
- Modify: `src/Perpetuum/Services/EventServices/EventMessages/SparkyResponseMessage.cs`
- Modify: `src/Perpetuum/Services/EventServices/EventProcessors/SparkyResponseProcessor.cs`
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyTraceSpoolTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyTraceUploaderTests.cs`

**Interfaces:**
- `SparkyTraceSpool` writes only bounded metadata events; it never stores caller IDs, channel IDs, question text, or answer text.
- `SparkyTraceUploader` posts batches to the API endpoint selected from `SparkyClientOptions` and retains every unacknowledged `EventId` across process restart.
- `SparkyResponseMessage.TraceId` stays attached through the event queue and delivery processor.

- [x] **Step 1: Check graph impact before editing coordinator/bootstrapper/response processor**

  Run and inspect:

  ```powershell
  .\tools\query-graph.ps1 SparkyRequestCoordinator -Direction in
  .\tools\query-graph.ps1 SparkyResponseMessage -Direction in
  .\tools\query-graph.ps1 SparkyResponseProcessor -Direction in
  .\tools\query-graph.ps1 PerpetuumBootstrapper -Direction in
  ```

  Confirm the coordinator and response processor remain leaf-oriented services and that registering a spool/uploader does not add a dependency cycle.

- [x] **Step 2: Pin spool recovery, deduplication, and capacity behavior**

  Test appending/reopening the JSONL file, an incomplete final line, duplicate event IDs, an invalid event line, and the configured disk bound. When the bound is reached, keep already-spooled events, stop accepting new trace metadata, emit a durable `trace_gap` marker when capacity returns, and keep Sparky request/answer delivery functioning.

- [x] **Step 3: Implement the bounded durable spool**

  Store event records at a configurable local path under the game logs directory by default. Use a single writer and compact JSONL. Flush each accepted record to disk before it is marked ready for upload. On startup, recover complete lines and truncate only an incomplete final record. Enforce maximum line bytes, maximum spool bytes, and no-reparse-point path validation. Write failures produce a redacted explicit warning and disable trace upload only; they do not disable Sparky.

- [x] **Step 4: Add the background uploader to the coordinator lifecycle**

  Start one tracked uploader task when the coordinator starts and cancel/drain it during `Stop()`. Use bounded batches, exponential backoff with maximum delay, cancellation on shutdown, no automatic re-submission of answer requests, and `EventId` acknowledgments to delete only committed records. Do not perform disk I/O, HTTP, or waits in `Update()` or channel-delivery code.

- [x] **Step 5: Emit metadata at request and delivery stages**

  Record trigger accepted/rejected, queue outcome, API outcome/latency, response queued, and final delivery result with the same GUID. Add a stage event to `SparkyResponseProcessor` for game chat, in-game mail, or Discord delivery; omit player, channel, and message IDs. Remove question/answer text from ordinary flat logs; content is captured only by the AI profile policy.

- [ ] **Step 6: Finish Main/AI failure-and-recovery validation in Task 7**

  Register the spool and uploader through Autofac constructor injection without adding a `GlobalConfiguration` dependency to request or delivery paths. Focused and full unit tests pass, and `Perpetuum.Bootstrapper` builds. The outage/restart/ack behavior is covered with a local HTTP test; the live Main/AI tunnel check remains in Task 7.

## Task 4: Add the authenticated admin listener, panel UI, and Main access tunnel

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyAdminAuthentication.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyAdminEndpoints.cs`
- Create: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/index.html`
- Create: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/app.js`
- Create: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/styles.css`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyTunnelSupervisor.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkySettings.cs`
- Modify: `tools/Configure-SparkyServiceSettings.ps1`
- Modify: `src/Perpetuum.ItemStatsExporterInstaller/SparkyServiceComponents.wxs`
- Modify: `src/Perpetuum.ItemStatsExporterInstaller/Perpetuum.ItemStatsExporterInstaller.wixproj`
- Test: `src/Perpetuum.Tests/Unit/SparkyAdminAuthenticationTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyTunnelOptionsTests.cs`

**Interfaces:**
- Add a distinct HTTPS Kestrel listener on AI loopback for the panel (default port 5081); the game API listener remains port 5080.
- Admin API endpoints include `GET /admin/api/health`, `GET /admin/api/traces`, `GET /admin/api/traces/{requestId}`, and `GET /admin/api/tunnels`.
- Admin endpoints require `SPARKY_ADMIN_API_TOKEN`; they do not accept Dev or Live API tokens.
- Panel HTML/CSS/JS is served locally without external CDN/runtime dependencies.
- An AI-initiated SSH tunnel maps the AI panel listener to a loopback-only Main port. It uses a separate key/config from game API tunnels and has no shell/agent/unrelated forwarding.

- [ ] **Step 1: Pin listener isolation and admin auth before UI work**

  Test that an admin endpoint returns 404 on the game API listener, that the panel listener returns 404 for game API-only routes where appropriate, that game tokens cannot access admin endpoints, and that missing/wrong admin tokens return 401. Confirm both Kestrel listeners bind only to `IPAddress.Loopback`.

- [ ] **Step 2: Add separate listener configuration and admin token loading**

  Add validated `SPARKY_ADMIN_SERVICE_URL`, admin token, certificate reuse, and admin SSH tunnel settings. Keep API and admin auth fixed-time compared and distinct. The admin API must not be reachable through the game API remote forward. Extend the tunnel supervisor with an independent admin tunnel to Main; do not couple its reconnect state to Dev/Live game tunnels.

- [ ] **Step 3: Implement read-only trace and operations endpoints**

  Add bounded query filters for date, environment, source, status, and label; cap page size; return only configured metadata/content fields; never return DPAPI material or exception text. Add endpoints for selected trace timelines and health/tunnel/profile status.

- [ ] **Step 4: Build the browser UI with a session-only admin token**

  Serve a static responsive interface from the packaged `wwwroot/sparky-admin` directory. Prompt for the admin token, retain it only in `sessionStorage`, send it as an Authorization bearer header, and provide logout that clears it. Implement request list/filter, request timeline, content-visible indicator, service/model/tunnel status, and a redacted JSON export toggle. Do not use external JavaScript, fonts, analytics, or CDN assets.

- [ ] **Step 5: Configure the AI-to-Main panel tunnel and installer settings**

  Extend `Configure-SparkyServiceSettings.ps1` to create protected admin token/tunnel settings, validate key/known-host paths, and set LocalService ACLs. Update the WiX package to include the panel assets and preserve existing service command-line/config behavior. Document the Main OpenSSH change: permit only the exact loopback admin listen port for the admin key; retain the existing game API restriction and host-key pinning.

- [ ] **Step 6: Build and manually verify read-only panel separation**

  Build the service and installer projects. On AI, verify the panel works at its loopback URL. On Main, verify it works at the dedicated reverse-forwarded loopback URL after host-key validation. Confirm game API authentication still works on 5080, admin APIs fail on 5080, the panel fails when the admin tunnel is stopped, and it is not reachable on the AI LAN/public addresses.

## Task 5: Add isolated Dev/Live profiles, hot reload, bundle staging, and promotion

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyProfileSnapshot.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyProfileManager.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyProfileValidator.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyBundleCatalog.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkySettings.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs`
- Modify: `tools/Refresh-SparkyKnowledge.ps1`
- Modify: `tools/Configure-SparkyServiceSettings.ps1`
- Modify: `tools/Configure-SparkyVdmClientSettings.ps1`
- Test: `src/Perpetuum.Tests/Unit/SparkyProfileValidatorTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyProfileManagerTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyBundleCatalogTests.cs`

**Interfaces:**
- A Dev/Live profile snapshot contains a profile ID, immutable revision ID, bundle version/path, content-capture policy, route/retrieval/style configuration, model ID, inference limits, and profile queue limits.
- AI credentials map to immutable profile IDs. The game request body cannot override the authenticated profile.
- Admin APIs include `GET /admin/api/profiles`, `GET /admin/api/profiles/{profile}`, `PUT /admin/api/profiles/{profile}/draft`, `POST /admin/api/profiles/{profile}/validate`, `POST /admin/api/profiles/{profile}/activate`, `POST /admin/api/profiles/live/promote`, and `POST /admin/api/profiles/{profile}/rollback`.
- `SparkyQuestionInterpreter` and `SparkyAnswerRouter` consume the profile snapshot passed for a request rather than startup-global mutable settings.

- [ ] **Step 1: Pin profile isolation and atomic-snapshot rules**

  Test that Dev and Live resolve distinct revisions/bundles; a Dev activation does not mutate Live; malformed config does not replace active state; and a request already holding snapshot A continues on A after snapshot B activates. Test that profile concurrency bounds cannot exceed the code-owned global bound.

- [ ] **Step 2: Implement immutable profile models and validation**

  Define a versioned JSON schema. Validate route names against deployed intents; constrain phrase/alias lengths, counts, locale values, candidate limits, scoring values, answer length, model ID, timeout, token bounds, and content/retention settings. Reject scripts, SQL, arbitrary regex, unknown fields, bundle paths outside the protected bundle root, and non-loopback HTTP model URLs unless HTTPS is configured.

- [ ] **Step 3: Load profile snapshots and maintain legacy operation**

  Load Dev and Live settings from the protected local profile directory. If no profile file exists, load the existing single-profile settings as `legacy` and keep `/v1/answer` behavior intact. After profiles are initialized, map the prior single token to Dev only; require a separately provisioned Live token before Live requests are accepted. Never silently promote the currently active `output.json` bundle to Live.

- [ ] **Step 4: Make interpretation, routing, and answer generation snapshot-aware**

  Resolve the profile once at request start and pass that immutable snapshot to interpreter, answer router, knowledge bundle, and model call. Replace hard-coded persona/style and supported inference tunables with validated profile values, but keep grounding, privacy, maximum limits, and fixed safety instructions in code. Record the snapshot revision and bundle version in all stages.

- [ ] **Step 5: Add atomic activation and audit revisions**

  Persist drafts/revisions transactionally in SQLite. Validate the entire revision and bundle before activation. Swap one immutable profile snapshot atomically; continue existing requests on their captured snapshot. Record admin actor, UTC timestamp, profile, source revision, result, and changed field names. Rollback activates a previous revision as a new revision pointing to its previous values.

- [ ] **Step 6: Stage, validate, and promote knowledge bundles per profile**

  Change `Refresh-SparkyKnowledge.ps1` to transfer a bundle into an immutable versioned bundle directory and validate it without replacing the active profile bundle. Keep the existing `output.json` legacy path readable during migration. Let the admin panel select a validated bundle for Dev and promote that exact bundle version with the selected config revision to Live. Keep the current bundle intact on validation failure and preserve the existing rollback behavior.

- [ ] **Step 7: Add Dev-first edit, activate, promote, and rollback UI**

  Add forms for supported route aliases/priorities, retrieval settings, persona/style, bounded inference values, and bundle selection. Show validation output and a diff. Require a second explicit action for Live promotion that names both revisions. Disable Live editing when its credential/profile is absent. Add a rollback action that identifies the previous active revision before applying it.

- [ ] **Step 8: Verify the critical profile failure cases**

  Run focused tests. Manually make one valid Dev route/style edit and verify it is active on the next request without restarting; verify Live output/revision is unchanged; apply an invalid config and invalid bundle and verify old revisions still answer; promote a known Dev revision to Live; roll it back; and confirm every request reports the revision/bundle it actually used.

## Task 6: Add feedback, replay, and diagnostic export

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyTraceStore.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyAdminEndpoints.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/index.html`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/app.js`
- Modify: `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/styles.css`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyReplayService.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyDiagnosticExporter.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyFeedbackTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyReplayServiceTests.cs`
- Test: `src/Perpetuum.Tests/Unit/SparkyDiagnosticExporterTests.cs`

**Interfaces:**
- `POST /admin/api/traces/{requestId}/feedback` accepts label values `wrong_route`, `missed_entity`, `retrieval_miss`, `incorrect_fact`, `language`, `style`, `context`, `delivery`, or `infrastructure` plus a bounded optional note.
- `POST /admin/api/evaluations` creates a human-reviewed evaluation example from a trace and expected route/result properties.
- `POST /admin/api/replays` accepts a draft profile revision and at most 100 saved examples; it never performs platform delivery.
- `GET /admin/api/replays/{id}` returns old/new route, intent, entities, source IDs, status, and answer side-by-side.
- `GET /admin/api/traces/{requestId}/export?includeContent=false` returns a bounded redacted JSON export.

- [ ] **Step 1: Pin labels, evaluation isolation, and export redaction**

  Test accepted/rejected labels, maximum note/export sizes, replay cap, and that replay cannot reach Discord/game delivery. Test the default export omits question/answer content, all exports omit user/platform/account/character IDs, secrets, raw prompts, and model reasoning, and include only selected source IDs/config metadata.

- [ ] **Step 2: Add feedback and reviewed evaluation records**

  Add normalized SQLite records for feedback and evaluations, with trace ID/revision references. Feedback must not mutate profile files or the active revision. An evaluation example is created only by an explicit human action and stores expected intent/route/result properties, not an automatically inferred answer.

- [ ] **Step 3: Implement side-by-side replay without transport delivery**

  Execute saved questions against an explicitly selected draft revision using the same bounded inference semaphore and profile limits as live requests. Persist replay results separately from request traces. Disable chat/Discord senders for replay code paths. Stop replay safely on cancellation or shutdown.

- [ ] **Step 4: Add feedback and replay UI**

  Add the bounded label selector and note box to a trace view, an action to add a reviewed evaluation example, and a replay page that shows the active and draft outputs side by side. Display route/source changes distinctly from wording changes; do not label model output as factually verified unless the selected source set supports it.

- [ ] **Step 5: Implement sanitized diagnostic export and retention**

  Export the selected trace timeline, relevant revision identifiers, selected source IDs, feedback, and redacted error categories. Honor the `includeContent` choice and profile content-capture policy. Apply content and metadata retention independently and log deletion counts without logging deleted text.

- [ ] **Step 6: Verify feedback is advisory only**

  Run focused tests; label a trace, create an evaluation example, replay it with a draft route, and confirm the active profile remains unchanged until explicit activation/promotion. Export with and without content and inspect the JSON for excluded fields.

## Task 7: Package, document, and validate on Main/AI

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/README.md`
- Modify: `docs/codebase/ARCHITECTURE.md`
- Modify: `docs/codebase/STRUCTURE.md`
- Modify: `docs/codebase/INTEGRATIONS.md`
- Modify: `src/Perpetuum.ItemStatsExporterInstaller/SparkyServiceComponents.wxs`
- Modify: `src/Perpetuum.ItemStatsExporterInstaller/Perpetuum.ItemStatsExporterInstaller.wixproj`
- Create: `docs/superpowers/sparky-admin-panel-operations.md`

- [ ] **Step 1: Verify the complete installer and self-contained publish**

  Publish `Perpetuum.ItemStatsExporter` self-contained as x64, build the WiX installer, and inspect the payload. Confirm it includes panel static assets and the service executable but excludes machine-specific profile tokens, admin token, SSH private keys, known-hosts files, PFX private keys, databases, and live bundles.

- [ ] **Step 2: Write the operator runbook**

  Document: installing/updating on AI; setting the Dev/Live protected credentials; configuring one game reverse tunnel per enabled environment; configuring the dedicated Main admin tunnel and its loopback-only OpenSSH restriction; trusting the local TLS certificate on Main; viewing traces; toggling content capture/retention; editing Dev; replaying examples; promoting to Live; rolling back; and preserving the current legacy configuration until migration is confirmed.

- [ ] **Step 3: Update architecture and support documentation**

  Describe the two AI loopback listeners, request ID lifecycle, AI-side profile selection, local game spool, AI SQLite store, content retention rules, protected profile settings, and the fact that no SQL Server connection is made by Sparky or the panel.

- [ ] **Step 4: Build the full solution and run focused automated tests after approval**

  Run the focused Sparky test filter used by Tasks 1–6, then build:

  ```powershell
  dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64 -m:1
  ```

  Expected: focused tests and build complete successfully. Report existing WiX/Windows Installer or NuGet-feed environment failures distinctly from source errors. Do not deploy to or restart Live.

- [ ] **Step 5: Perform the Main/AI end-to-end validation**

  Use Main's debug server and AI's installed service. Verify:

  1. Main Dev requests appear with one joined request ID from trigger through game/Discord delivery; no caller IDs are visible in the panel, SQLite rows, or spool.
  2. Stop the Dev game tunnel; an answer request fails safely while metadata events remain locally spooled. Restart the tunnel and confirm retry uploads once.
  3. Open the panel on AI loopback and on Main through the admin tunnel. Confirm wrong token fails, game token fails admin auth, game API port returns no panel route, and neither listener binds to LAN/public interfaces.
  4. Enable Dev content capture, confirm question and answer are visible there; disable it and confirm only metadata is stored. Confirm Live defaults off even when Dev is on.
  5. Change a Dev alias/style setting, reload a question, and confirm no service restart is required. Confirm Live's active revision and bundle do not change.
  6. Replay a reviewed case with a draft and confirm no message is posted to chat/Discord. Promote a known Dev revision and bundle in the local test setup, verify the audit entry, then roll back.
  7. Stop the Main admin tunnel and confirm the panel remains available locally on AI; restore it and confirm Main access returns.
  8. Expire content and metadata independently in a disposable test store; confirm configured cleanup and no unrelated runtime data is removed.

  Do not connect to, configure, or restart the Live VDM host during this validation. The later Live rollout requires a separate explicit approval and maintenance plan.

## Plan Self-Review

- **Spec coverage:** topology and tunnel separation (Tasks 4 and 7); correlation and profile authentication (Tasks 1–2); durable local spool and AI journal (Tasks 2–3); hot configuration and bundles (Task 5); feedback, replay, and export (Task 6); retention, privacy, and operations (Tasks 2, 4, 6, and 7); packaging and docs (Task 7).
- **Failure cases:** invalid IDs, duplicate events, authentication crossover, store failure, spool overflow, tunnel outage, invalid config/bundle, in-flight revision swap, and retention are each assigned a test or manual check.
- **Compatibility:** the current single-profile CLI/config path stays active until v2 profiles are explicitly provisioned; legacy credentials do not grant Live access.
- **Open implementation choice for review:** the UI is a separate browser panel and listener but is hosted in the existing Sparky Windows service process to share its store/profile runtime and avoid another Windows service. If “separate panel” was intended to mean a separate executable/service, revise this plan before implementation.
- **No work executed from this plan:** it contains proposed code, test, build, and manual-validation steps only. No runtime files or tests were changed while writing it.
