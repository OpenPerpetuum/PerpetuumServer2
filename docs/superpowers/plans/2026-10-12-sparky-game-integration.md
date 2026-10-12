# Sparky Game and Discord Integration Implementation Plan

> **Execution approach:** Native, inline in the current workspace, as previously selected. Do not create a worktree or delegate this implementation.

**Goal:** Integrate the approved Sparky item-stat/recipe API with ordinary-player in-game chat and Discord, with bounded asynchronous processing, source-only replies, and short-lived in-memory follow-up context.

**Architecture:** Keep item lookup and model calls on the AI PC. Add an optional context-item hint to the existing HTTPS API, then add a game-server coordinator that admits requests to a bounded background queue and returns response events through `EventListenerService`. Game/Discord ingress remains non-blocking. Resolve in-game delivery at response time: game-only post if the character is online, otherwise mail. Disable the feature by default.

**Tech Stack:** Existing .NET 8 server and `Perpetuum.ItemStatsExporter`; Autofac; `System.Threading.Channels` or the closest existing bounded-queue pattern; `HttpClient`; existing `EventListenerService`, channel manager, session manager, mail handler, and Discord.Net client; Windows DPAPI for the VDM-side API token.

**Spec:** `docs/superpowers/specs/2026-10-12-sparky-game-integration-design.md`

## Global constraints

- Do not write product code until the user approves this implementation plan.
- Keep the Sparky feature disabled by default and fail closed without affecting unrelated game-server startup.
- Do not block channel request handlers, Discord callbacks, zone updates, or the `ProcessManager` thread on network/model work.
- Do not send game character IDs, Discord IDs, account data, online state, or live-database records to the AI service. Only the question and optional public item display name cross the API boundary.
- Keep API token outside `perpetuum.ini`, source control, URLs, command lines, and logs. Use a machine-protected file with ACLs for the game-server service identity and administrators.
- Keep replies on the source platform. Sparky game answers must bypass the existing game-to-Discord announcement mirror. Discord replies must disable user mentions.
- Preserve the current forwarding behavior for original chat messages, Discord channel mapping, chat permissions, and admin command routing.
- Preserve the approved 2,000-character final-message cap, 15-minute configurable inactivity expiry, English-only POC, and selected Sparky sender character.
- Update and publish the API on the AI PC before deploying a client that sends `contextItem`.
- No code, configuration, reboot, or service restart on the live VDI as part of development. Rollout requires a non-live validation first and a separate maintenance-window decision.
- No automated tests unless the user asks. Use the approved spec's manual checks in a non-live environment before any VDI rollout. Do not commit unless asked.

## Preflight: repository and impact checks

**Files/docs:** approved spec, `CLAUDE.md`, `docs/codebase/ARCHITECTURE.md`, `docs/codebase/STRUCTURE.md`, `docs/codebase/INTEGRATIONS.md`, `docs/codebase/CONCERNS.md`, `docs/codebase/CONVENTIONS.md`, `docs/codebase/TESTING.md`, and `docs/backlog/issues.md` / `docs/backlog/improvements.md`.

- [x] Confirm there is no existing Sparky backlog entry to update; do not add a duplicate.
- [x] Check whether `docs/graph/graph.json` is available. If it is, run the following before changing any of these symbols and inspect each result:

  ```powershell
  .\tools\query-graph.ps1 ChannelManager -Direction in
  .\tools\query-graph.ps1 IChannelManager -Direction in
  .\tools\query-graph.ps1 EventListenerService -Direction in
  .\tools\query-graph.ps1 GlobalConfiguration -Direction in
  ```

- [x] If the graph data is absent, record that and continue with targeted `rg` searches and constructor/registration inspection. The graph was present; I inspected the exact ChannelManager graph node and used targeted source/registration searches.
- [ ] Confirm the actual game-server service identity on the non-live host before generating the VDM secret ACL instructions. Do not infer it from an interactive administrator session. The local CIM query was denied; the helper therefore requires an explicit identity and the guide keeps the example clearly replaceable.
- [x] Keep existing unrelated working-tree changes and generated files untouched.

## File map

- Modify `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`, `SparkyHttpService.cs`, and `ItemStatsLookup.cs` for optional context-item lookup.
- Add focused game-server service files under `src/Perpetuum/Services/Sparky/` for options, protected-token loading, context storage, request coordination, and event/result routing.
- Modify `src/Perpetuum/Services/Channels/ChannelManager.cs` and `IChannelManager.cs` for trigger admission and game-only answer posting.
- Modify `src/Perpetuum/Services/EventServices/EventListenerService.cs`, its event message types, and a focused event processor for Discord mention capture and queued result delivery.
- Modify `src/Perpetuum/GlobalConfiguration.cs` and `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` for disabled-by-default configuration and DI/process registration.
- Use `src/Perpetuum/Services/Sessions/ISessionManager.cs` and `src/Perpetuum/Services/Mail/MailHandler.cs` for response-time online checks and offline mail.
- Add `tools/Configure-SparkyVdmClientSettings.ps1` or an equivalent small provisioning helper. It must work in Windows PowerShell 5.1 on Windows Server 2016, prompt for the token securely, encrypt with machine-scope DPAPI, and apply a restrictive ACL.
- Update `docs/codebase/ARCHITECTURE.md`, `docs/codebase/STRUCTURE.md`, and `docs/codebase/INTEGRATIONS.md`; add a concise VDM client-configuration and rollout guide under `docs/superpowers/`.

## Task 1: Extend the existing Sparky API for follow-up lookup

**Files:** `SparkyApiModels.cs`, `SparkyHttpService.cs`, `ItemStatsLookup.cs`.

- [x] Add an optional bounded `ContextItem` field to `SparkyApiRequest`. Requests containing only the existing `Question` field must remain valid.
- [x] Validate the context value's length and require it to match a public display name in the loaded export. Ignore or reject invalid context without exposing internal definition names.
- [x] Update deterministic lookup so an explicit item named in the current question takes precedence. Apply the context item only when the question is an item-related follow-up with no explicit item match. Preserve normal `not_found` and `ambiguous` behavior when context is absent or invalid.
- [x] Keep the item definition resolution, translations, verified stats, recipes, and full-material calculation grounded in the existing export. Do not add caller identity or arbitrary message-history fields to the API.
- [x] Keep context content out of all logs. Preserve the existing API authentication, HTTPS, request-size limits, inference timeout, and concurrency behavior.

**Manual checkpoint:** Start the updated API on the AI PC's non-live setup. Manually send an existing question-only request, a follow-up with a valid context display name, an invalid context name, and a question explicitly naming a different item. Confirm the context hint does not leak in responses or logs.

## Task 2: Add safe VDM client settings and HTTP transport

**Files:** `GlobalConfiguration.cs`, a focused Sparky options/secret loader, `Configure-SparkyVdmClientSettings.ps1`.

- [x] Add optional Sparky settings with `EnableSparky` defaulting to `false`; configure the loopback-forwarded HTTPS URL, request timeout, answer cap, queue size, worker count, per-caller pending limit, context expiry/capacity, and selected sender nickname.
- [x] Keep the bearer token in a separate DPAPI machine-protected settings file, not in `perpetuum.ini`. Read it into memory only for authenticated HTTP requests and never log it.
- [x] Provide a provisioning helper parsed under Windows PowerShell 5.1 conventions. It does not require a newer PowerShell API, put token text in command history/arguments, or use the earlier `RandomNumberGenerator.Fill`/assembly pattern.
- [ ] Restrict the token file ACL to the actual game-server service identity and local administrators. The helper applies least-privilege ACLs when run with the verified identity; the actual non-live service identity and read test are pending.
- [x] Validate the URL is HTTPS and loopback-only, ports and numeric limits are valid, and the token is present when enabled. If Sparky-only settings are invalid, disable Sparky and emit a redacted configuration error without preventing the game server from starting.
- [x] Use a long-lived `HttpClient` with normal certificate validation, bearer authentication, response-size bounds, and cancellation/timeout propagation. Do not add a permissive TLS callback or automatic HTTP retry.

**Manual checkpoint:** On a non-live Windows host, create/read the protected settings as the intended service identity, confirm a wrong ACL fails, confirm the token does not appear in `perpetuum.ini`, process arguments, or logs, and confirm the client rejects non-HTTPS/non-loopback URLs.

## Task 3: Implement bounded coordination and in-memory context

**Files:** focused types under `src/Perpetuum/Services/Sparky/`; `EventType.cs` and a new response event message as needed.

- [x] Implement a singleton coordinator with a bounded, thread-safe queue and a configurable small worker count. `TrySubmit` must never wait for queue space or network activity.
- [x] Track worker tasks and cancel/drain them according to the game-server lifecycle. Do not introduce untracked fire-and-forget tasks. Do not perform HTTP calls from `Update` or a zone tick.
- [x] Define a local request snapshot containing source kind, origin channel, internal caller key, question, and a response-time delivery handle. Keep these values inside the VDM process. Send only the question and optional context display name to the API.
- [x] Keep context in a bounded in-memory store keyed independently by in-game character ID or Discord author ID. Store only the last successful display item and last-activity time; use configurable sliding expiry (15 minutes by default), and clear all entries at process shutdown.
- [x] Preserve context after a successful `answered` result only. Do not replace context on missing, ambiguous, timeout, or unavailable results. Ensure an explicit different item supersedes old context.
- [x] Prevent unbounded requests per caller; return a short generic busy result for a full queue or pending request. Do not log question text, answer text, or caller IDs.
- [x] Publish each completion as a result event to `EventListenerService`; the background worker must not call chat, mail, or Discord APIs directly.

**Manual checkpoint:** With a non-live local stub or service endpoint, demonstrate bounded admission, cancellation on shutdown, context expiry/restart clearing, and that completion is processed through the event queue.

## Task 4: Wire in-game trigger and response delivery

**Files:** `ChannelManager.cs`, `IChannelManager.cs`, Sparky ingress/result handling, and bootstrapper registration.

- [x] Run the trigger through the existing channel membership, mute, and admin-command checks. Recognize only a case-insensitive `Sparky,` prefix; strip the prefix before submission. Keep ordinary messages, invalid-channel checks, and admin commands unchanged.
- [x] Continue broadcasting and mirroring the original question according to existing channel behavior. Make the bounded coordinator admission attempt without waiting.
- [x] Add an explicit game-only channel announcement operation so answers on linked channels are not forwarded to Discord. Include the normal game chat logging behavior for the outgoing answer.
- [x] Resolve `[OPP] Sparky - The Syndicate Welcome Agent` as the sender after DB initialization. If the sender is unavailable, keep the feature disabled rather than using a different identity or preventing game-server startup.
- [x] When the response event is processed, check `ISessionManager.IsOnline` at that time. If online, post to the originating channel. If offline, use `MailHandler.SendMail` to send in-game mail from the selected sender. If the originating channel is no longer available, use mail where possible.
- [x] Cap the final formatted chat/mail response at 2,000 characters. Use the existing `cmails.body nvarchar(2000)` storage path and do not bypass mail validation.
- [x] Route queue-full and generic failure messages through the same game-only delivery path. Keep the feature harmless while disabled.

**Manual checkpoint:** In a non-live server copy, verify ordinary chat and admin commands are unchanged; an online caller gets a channel post; logout during inference leads to mail; the answer does not appear in linked Discord; and formatted output stays within 2,000 characters.

## Task 5: Wire Discord mentions and source-only replies

**Files:** `EventListenerService.cs`, focused Discord request/result event types and processor, bootstrapper registration.

- [x] Detect an actual mention of the bot in a non-bot message, strip the mention from `CleanContent`, and capture the author ID/channel ID only as local routing/context metadata.
- [x] Accept mentions in every channel the bot can access, whether or not a game channel mapping exists. Preserve current Discord-to-game forwarding for mapped channels.
- [x] Admit the question to the same bounded coordinator. A bare mention or busy/unavailable response returns only to that Discord channel.
- [x] Send the completed answer to the original Discord channel through the event queue. Disable all allowed mentions on Sparky output so model text cannot ping users. Do not mirror the answer into game chat.
- [x] Apply the 2,000-character cap to the whole Discord message and do not include player/account identifiers in the HTTP API request or logs.

**Manual checkpoint:** Verify mention handling in both mapped and unmapped channels, mention stripping, ignored bot authors, no pings from answer text, and no Sparky answer mirrored to game chat.

## Task 6: Register, document, and validate the staged rollout

**Files:** `PerpetuumBootstrapper.cs`, authoritative codebase docs, and a focused Sparky VDM operator guide.

- [x] Register coordinator and response processors through the existing Autofac and `EventListenerService.AttachListener` patterns. Start/stop the coordinator with the server lifecycle.
- [x] Update architecture, structure, and integration docs to explain the game-side HTTP client, bounded worker boundary, result queue, credential protection, and feature-disabled default.
- [x] Document publish order: update and republish the self-contained `Perpetuum.ItemStatsExporter` on the AI PC first; then provision VDM client configuration; then validate the game-server client on the non-live host; schedule any live VDI deployment for a maintenance window.
- [x] Document secure secret provisioning, expected service identity/ACL, TLS root trust, operational errors, feature disable/rollback, and the fact that no VDI reboot or change is part of development validation.
- [ ] Manually validate the approved spec's scenarios on a non-live environment: triggers, supported stats/recipes/full materials, follow-up and explicit-item override, in-game chat vs offline mail, Discord source-channel reply, queue saturation, network/TLS/auth failures, 2,000-character cap, and privacy-safe logs.
- [x] Do not run automated tests unless requested. Do not touch the live VDI or restart a live game server during implementation. Automated tests were not run and the VDI was untouched.
- [ ] After non-live validation, present a concrete deployment package and manual results for separate rollout approval. Non-live end-to-end validation still depends on the user's separate AI/game machines.

## Risks and review points

- `ChannelManager` and `IChannelManager` are broad-surface APIs. Graph/dependent checks must happen before any edit, and the new game-only announcement must preserve current membership/session delivery semantics while omitting only the Discord mirror.
- `EventListenerService` is a `Process` with its own queue and async Discord callbacks. The request queue and result event route must remain bounded where Sparky work enters and must not let model/network latency consume the process-loop budget.
- The Sparky HTTP API currently accepts only `question`; adding `contextItem` is an additive contract change but the current JSON configuration disallows unknown members. Publish the updated AI service before any VDM game-server client that sends the new field.
- Character mail uses a 2,000-character body column. Formatting and truncation must happen before the existing mail write.
- Windows PowerShell and Windows Server 2016 have older APIs and service-account behavior than the development workstation. Validate the protected settings provisioner against the actual compatible runtime before scheduling VDI deployment.
- Existing worktree changes include Sparky service files/specs, documentation, exports, and publish output. Preserve them; review the exact diff before implementation and before presenting a deployment package.
