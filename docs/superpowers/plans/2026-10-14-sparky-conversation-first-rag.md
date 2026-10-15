# Sparky Conversation-First RAG Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Use `superpowers:test-driven-development` for implementation tasks and `superpowers:verification-before-completion` before reporting completion. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Evolve Sparky into a natural, multi-turn, in-character game assistant that uses bounded conversation context, retrieves approved evidence, preserves exact structured facts, respects caller rank for wording only, and can be tuned and evaluated through the Admin Panel without routine code changes.

**Architecture:** Keep caller-keyed volatile conversation state on the game server. Send a bounded, source-scoped context packet and one normalized rank to the existing local Sparky API. The AI service classifies scope and intent, resolves follow-ups, performs only approved bundle retrieval or structured lookups, and asks the local model to compose normal answers. Code validates context, candidate selections, domain policy, exact facts, privacy, and response bounds. The Admin Panel stores versioned AI profile settings and ordered evaluation conversations, then replays each sequence in isolated context against active and draft revisions.

**Tech Stack:** .NET 8, C# 12, ASP.NET Core, System.Text.Json, existing LM Studio OpenAI-compatible API, SQLite trace/profile store, existing game request queue and event delivery, vanilla HTML/CSS/JavaScript Admin Panel.

**Spec:** `docs/superpowers/specs/2026-10-14-sparky-conversation-first-rag-design.md` (approved by the user on 2026-10-14).

**Execution approach:** Native inline implementation in the current checkout, consistent with the user's previously selected approach. Implementation and local Dev verification only; do not promote or deploy to Live.

## Global Constraints

- AI, Main, and Live remain separate Windows hosts. Sparky and LM Studio run on AI; Main is the local debug environment; Live remains the VDM production host.
- Continue using offline-built knowledge bundles. The AI service receives no live database access, arbitrary filesystem access, SQL, game-control, or administrative tools.
- Keep Discord and game conversations separate. Caller IDs, account/character IDs, channel IDs, presence, delivery state, and raw permissions/role IDs stay on the game server.
- Send only a bounded conversation packet and a normalized rank label to AI. Rank can change phrasing but cannot change access, retrieval, or factual policy.
- Context remains volatile, keyed by `(source, caller)` on the game server. Do not persist transcripts or caller profiles in this phase. Clear sessions on expiry, reset, or game-server restart.
- Preserve existing queue limits, cancellation, source-only delivery, chat/email behavior, 2,000-character response cap, bearer authentication, HTTPS, and reverse SSH topology.
- The real-world sensitive/political-topic boundary is code-owned and cannot be relaxed through persona or profile editing. In-game politics remains answerable only from approved evidence.
- Player turns, conversation summaries, and retrieved passages are untrusted data. None can override system instructions or become factual evidence without retrieval/validation.
- Normal answers, clarification, small talk, evidence gaps, and refusals must be composed by the local model when available. Static templates are for unavailable/timed-out/unusable Sparky or model responses after bounded recovery.
- Preserve exact item/robot/recipe facts, units, localized labels, and recursive-material completeness. The model may phrase facts but cannot invent, recalculate, or alter them.
- Keep content capture governed by the existing per-profile controls and retention policy. Never log hidden reasoning. Context must not be captured merely because debug logging is enabled.
- Dev and Live profiles, revisions, and bundles remain isolated. Every replay is offline from game/Discord delivery and uses fresh ephemeral context. Do not change Live configuration or restart the VDM server.
- Preserve unrelated user changes and generated local artifacts. Do not commit, merge, or push as part of implementation unless the user asks later.

## Review Focus

- **Conversation races:** Multiple messages from one caller can currently be in flight on different workers. Ensure each source/caller lane observes accepted-message order and appends exactly one user/assistant pair, while unrelated callers remain concurrent.
- **Clarification safety:** A short reply such as “Da Gama” may select only a candidate Sparky previously offered in that same source-scoped session. A guessed or stale candidate must produce a clarification, not a lookup.
- **Context growth and drift:** Enforce hard turn/character limits, keep pending clarification state when pruning, treat summaries as non-evidence, and ask naturally when context is expired, contradictory, or insufficient.
- **Prompt injection and scope:** Test hostile instructions in current text, old turns, summaries, and retrieved passages. Real-world political/sensitive prompts must fail closed; supported in-game politics must still retrieve and answer from approved evidence.
- **Factual fidelity:** Structured stats, recipes, robot assembly and lore claims must remain grounded. Model phrasing must not change supplied values, translations, units, or completeness.
- **Rank mapping/privacy:** Unmapped callers default to Agent. Configured privileged roles may affect address only. No source IDs, raw permissions, or cross-platform identity guesses reach AI or traces.
- **Fallback boundary:** While LM Studio works, missing evidence and ambiguity should receive natural model-generated responses. Static templates are permitted only for service/model failure or unusable generation after bounded recovery.
- **Profile compatibility:** Existing schema-v1 active/draft profiles and stored single-question evaluations must continue to load after additive migrations, with conservative defaults and no activation of a partially migrated profile.
- **Evaluation isolation:** Each active and draft replay starts with separate context; one sequence cannot leak into another or into a live player session.
- **Resource budget:** Verify prompt budgets against the currently tested 4,096-token LM Studio context and preserve room for instructions, retrieved evidence, and output. Do not assume the whole window belongs to history.

---

## File Map

### Game server context and identity

- Modify `src/Perpetuum/Services/Sparky/SparkyContextStore.cs` to hold bounded turns, a compact summary, language/topic continuity, and a validated pending clarification per `(source, caller)` key.
- Modify `src/Perpetuum/Services/Sparky/SparkyRequestCoordinator.cs` to process each caller's work in FIFO order, build the API context at execution time, append the completed exchange, preserve source-only delivery, and apply API context updates.
- Modify `src/Perpetuum/Services/Sparky/SparkyApiClient.cs` and `SparkyClientOptions.cs` to serialize/validate the bounded context and normalized rank without including platform identifiers.
- Modify `src/Perpetuum/GlobalConfiguration.cs` and `src/Perpetuum/Services/Sparky/SparkyClientOptions.cs` for configurable retained turns/context characters and game-side rank mappings, with strict bounds and safe defaults.
- Modify `src/Perpetuum/Services/Channels/ChannelManager.cs` and `src/Perpetuum/Services/EventServices/EventListenerService.cs` to resolve source-specific rank from the authoritative game character access level or Discord guild member/role data before submitting. Keep raw role IDs on the game host.
- Add focused game-side tests under `src/Perpetuum.Tests/Unit/` for context expiry, source separation, bounded pruning, FIFO same-caller requests, and rank mapping.

### AI API, interpretation, retrieval, and generation

- Modify `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs` to add optional bounded context, pending-clarification, normalized-rank, and context-update fields while retaining legacy request fields for compatibility.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs` to validate request limits, load the immutable profile snapshot, classify scope, invoke the new conversation flow, return a bounded context update, and keep request traces free of caller identity.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs` to interpret the latest message with recent turns and summary, return a structured scope/intent/query/follow-up action, and validate any clarification selection against the exact pending choices.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs` and `SparkyKnowledgeRetriever.cs` so only game-scope requests retrieve facts, context-resolved queries search the approved bundle, and selected candidates/structured lookups are validated before generation.
- Create `src/Perpetuum.ItemStatsExporter/SparkyScopePolicy.cs` for code-owned game/small-talk/out-of-scope/ambiguous handling and hard refusal constraints. Persona text cannot weaken the policy.
- Create `src/Perpetuum.ItemStatsExporter/SparkyConversationSummarizer.cs` for summary generation only when pruning is required; summaries are bounded context, not evidence. If it fails, answer with the latest retained turns and preserve structured pending state.
- Refactor `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs` into a constrained response composer for factual answers, clarifications, small talk, evidence gaps, and refusals. Pass verified facts and the selected outcome; parse structured output, validate its language and size, and use only bounded recovery before reporting generation failure.
- Keep `src/Perpetuum/Services/Sparky/SparkyResponseTemplates.cs` as the localized fallback source for genuine service/model unavailability, timeouts, or unusable output. Remove normal successful router branches that return canned responses while the model is healthy.
- Add AI-side tests under `src/Perpetuum.Tests/Unit/` for scope policy, interpretation validation, pending-choice validation, summary fallback, natural response generation contracts, and fact-preservation checks.

### Profile settings and Admin Panel evaluation

- Modify `src/Perpetuum.ItemStatsExporter/SparkyProfileSnapshot.cs`, `SparkyProfileValidator.cs`, and `SparkyProfileManager.cs` to add bounded conversation/history settings and answer-generation controls (`temperature`, `top_p`, existing completion-token and timeout settings). Keep interpretation deterministic and separate from answer-generation tuning.
- Add a schema-v1-to-current profile migration in `SparkyProfileManager.cs`; use conservative defaults for missing fields and preserve active/draft revision and Dev/Live isolation.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyTraceModels.cs` to represent an evaluation case as an ordered list of turns, with optional expectations attached to each turn and a sequence-level note.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyTraceStore.cs` with an additive SQLite migration that preserves current one-question cases as one-turn sequences. Bound sequence length, turn text, expectation size, and serialized replay result size.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyReplayService.cs` so each case runs sequentially with its own ephemeral context against active and draft snapshots, recording each turn's scope, interpretation, contextual query, entities, selected sources, and answer.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyAdminEndpoints.cs` to validate and expose sequence creation/replay without changing the active profile or contacting game/Discord.
- Modify `src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/index.html`, `app.js`, and `styles.css` with: profile controls for bounded context and inference tuning; an ordered-turn evaluation editor prefilled from a selected trace; add/remove/reorder turn controls; per-turn expected route/entity/source/fact fields; and side-by-side active/draft turn results.
- Add tests for profile bounds/migration, SQLite legacy-case compatibility, multi-turn replay order, per-case context isolation, and Dev/Live revision isolation.

### Documentation

- Update `src/Perpetuum.ItemStatsExporter/README.md` with the additive API contract, profile tuning fields, context ownership, evaluation-sequence workflow, and local Dev-only rollout procedure.
- Update `docs/codebase/ARCHITECTURE.md`, `docs/codebase/INTEGRATIONS.md`, and `docs/codebase/STRUCTURE.md` only where the implemented interfaces or trust boundaries change.
- Add `docs/superpowers/sparky-conversation-operations.md` covering rank mapping, context limits/expiry, model tuning, multi-turn evaluation, profile rollback, and the fact that no durable caller profile is created.

---

## Interfaces

Keep JSON field names additive and use the existing web JSON naming policy. The API must accept old clients that send only `requestId`, `question`, `contextItem`, and `contextLanguageId`.

```csharp
internal sealed record SparkyConversationTurn(string Role, string Content);

internal sealed record SparkyPendingClarification(
    string OriginalQuestion,
    string? RequestedAnswerType,
    IReadOnlyList<string> CandidateNames);

internal sealed record SparkyApiRequest(
    Guid? RequestId,
    string? Question,
    string? ContextItem = null,
    int? ContextLanguageId = null,
    IReadOnlyList<SparkyConversationTurn>? ContextTurns = null,
    string? ContextSummary = null,
    SparkyPendingClarification? PendingClarification = null,
    string? CallerRank = null);

internal sealed record SparkyConversationUpdate(
    string? Summary,
    string? ResolvedEntity,
    SparkyPendingClarification? PendingClarification);

internal sealed record SparkyApiResponse(
    string Status,
    string? Item = null,
    string? Answer = null,
    IReadOnlyList<string>? Candidates = null,
    int? LanguageId = null,
    SparkyConversationUpdate? ContextUpdate = null);

internal enum SparkyConversationScope { Game, SmallTalk, OutOfScope, Ambiguous }
internal enum SparkyCallerRank { Technician, Agent, Officer, CEO, CommandDirector }
```

Validation rules:

- Accept only `user` and `assistant` roles; cap the packet at 8 recent turns by default, 16 hard maximum, and 6,000 characters by default, 8,000 hard maximum. Each turn is at most 2,000 characters. Clamp rather than trust client-supplied counts.
- Bound summaries to 700 characters and candidate lists to 8 unique names, each at most 120 characters. Keep the pending clarification's original question and answer type within the same context budget.
- Keep the current 15-minute sliding expiry and capacity defaults. Make maximum turns and context characters configurable on the game server; make AI-side conversation budget, temperature, and top-p configurable in Dev/Live profiles with strict hard bounds.
- Normalize missing/unknown caller rank to `Agent`. Never serialize caller IDs, raw permissions, or Discord role IDs.
- Add an optional context-update object to responses. When absent, legacy behavior remains valid. Store a pending clarification only when the answer status and candidate list show that Sparky actually offered those choices.

---

## Implementation Tasks

### Task 1: Define bounded conversation contracts and game-side volatile state

**Files:** `SparkyContextStore.cs`, `SparkyRequestCoordinator.cs`, `SparkyApiClient.cs`, `SparkyClientOptions.cs`, `GlobalConfiguration.cs`, focused tests in `src/Perpetuum.Tests/Unit/`.

- [x] Add conversation-turn, pending-clarification, normalized-rank, and context-update data contracts with strict length/count validation. Keep existing request/response JSON fields compatible.
- [x] Extend `SparkyContextStore` with immutable snapshots and atomic append/update operations for recent turns, summary, language/entity continuity, and pending choices. Preserve the existing sliding expiry and bounded capacity.
- [x] Add FIFO processing per `SparkyRequestIdentity` so multiple messages from one caller cannot read the same stale snapshot or reorder follow-ups. Keep concurrent callers on independent lanes and preserve existing global queue/pending limits.
- [x] Build the context packet at the head of the caller's FIFO lane. After generation, append the user turn and final delivered answer exactly once, apply only bounded AI context updates, and retain pending choices only when they were offered in that conversation.
- [x] Add game-side configuration for maximum retained turns and context characters with safe defaults and hard bounds. On expiry/restart/reset, clear volatile context and pending state.
- [x] Ensure captured/logged context follows the existing Dev/Live capture policy. Never add raw context to default logs or upload caller identity with it.
- [x] Add unit tests for source/caller isolation, expiration, capacity eviction, bounds/pruning, turn append order, and concurrent same-caller submission ordering.
- [x] Run the focused game-side tests and build `Perpetuum`/`Perpetuum.Tests` in Release before proceeding.

**Acceptance:** Game context is volatile, bounded, ordered, and contains no cross-platform identifiers in serialized API payloads. Existing one-turn requests still serialize and are accepted.

### Task 2: Add authoritative source-specific rank mapping

**Files:** `ChannelManager.cs`, `EventListenerService.cs`, `GlobalConfiguration.cs`, `SparkyClientOptions.cs`, rank-resolution tests.

- [x] Add a shared normalized rank resolver. Sparky is `Technician`; ordinary/unmapped callers are `Agent`; map game access levels and explicitly configured Discord guild role IDs to the five-rank hierarchy.
- [x] Map configured administrator access/roles to `CommandDirector` by default where authoritative admin data exists. Allow explicit configuration for `Officer` and `CEO`; if multiple Discord roles match, select the highest configured rank.
- [x] Read game rank from the already-authenticated `Character.AccessLevel`; read Discord rank only from a guild member and configured role IDs/administrator permission. DM or unresolved Discord identity defaults to `Agent`.
- [x] Pass only the normalized enum/name into the work item and AI request. Keep names and platform IDs for local delivery only; do not send them to AI or include them in trace events.
- [x] Add tests for normal/admin game access, unmapped Discord roles, multiple matching roles, non-guild callers, and rank affecting wording metadata only.
- [x] Build the affected project and tests in Release.

**Acceptance:** A source adapter resolves a single rank from authoritative local data; no rank mapping grants tools, data, or retrieval access.

### Task 3: Interpret conversations and enforce scope/retrieval policy

**Files:** `SparkyApiModels.cs`, `SparkyHttpService.cs`, `SparkyQuestionInterpreter.cs`, new `SparkyScopePolicy.cs`, `SparkyAnswerRouter.cs`, `SparkyKnowledgeRetriever.cs`, conversation tests.

- [x] Extend interpretation input with bounded recent turns, summary, pending clarification, and current language. Extend output with scope, intent, contextualized search query, explicit follow-up action, selected candidate text when applicable, mentioned entities, and requested answer type.
- [x] Add strict parser validation: supported enum values only; bounded query/entities; no clarification selection unless it exactly matches one of the pending candidate names; current user text wins over stale summary when they conflict.
- [x] Add code-owned `SparkyScopePolicy` for `Game`, `SmallTalk`, `OutOfScope`, and `Ambiguous`. Do not allow persona or route aliases to relax the real-world sensitive/political boundary.
- [x] For `Game`, search both the latest player wording and the context-resolved query, then retrieve from the allowlisted active bundle or invoke existing verified structured lookup. For `SmallTalk`, `OutOfScope`, and `Ambiguous`, do not attach game-fact evidence.
- [x] When a clarification candidate is selected, reconstruct the original request plus the selected exact entity and requested answer type before retrieval. If the choice is stale, absent, or ambiguous, ask a clarification without querying the guessed entity.
- [x] Create a compact summary only when retained context needs pruning. Treat it as untrusted dialogue context, bound it to 700 characters, and keep structured pending clarification state separately. If summary generation fails, continue with the latest turns and do not block the answer.
- [x] Return a `SparkyConversationUpdate` with the new bounded summary, resolved entity, and pending clarification only after validation. Keep factual evidence sourced by bundle/structured lookup, never by summary.
- [x] Add tests for multilingual and context-dependent scope, game-vs-real-world ambiguity, instruction injection across history/summary/passages, exact candidate selection, context-resolved retrieval, missing/expired context, and summary failure.
- [x] Run focused interpretation/retrieval tests and Release build.

**Acceptance:** Follow-ups can resolve “Da Gama,” “it,” and “its full recipe” from same-source context; no stale or invented choice can select a fact; non-game scope never receives retrieved facts.

### Task 4: Make normal response phrasing model-generated with verified facts

**Files:** `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs`, `SparkyAnswerRouter.cs`, `src/Perpetuum/Services/Sparky/SparkyResponseTemplates.cs`, structured item/robot answer paths, new response-composer tests.

- [x] Build a single constrained response-composition path for factual answers, small talk, clarification, evidence gaps, and refusals. The input declares the already-selected outcome and includes only the context and approved evidence needed for that outcome.
- [x] Keep exact structured facts immutable in the prompt payload: stats, units, localized labels, direct recipe, recursively expanded materials, robot assembly, and component-derived values. Ask the model for 1–3 concise natural sentences or a concise readable list where exact fields require one.
- [x] For no evidence, ambiguity, or out-of-scope requests, prompt a natural in-character clarification/refusal without sending unrelated retrieved passages or allowing general-knowledge completion.
- [x] Add prompt sections that distinguish current user text, prior user/assistant turns, untrusted summary, verified facts, and style-only normalized rank. Do not give the model caller nick, identity, or roles.
- [x] Add bounded output validation for JSON shape, response language, answer length, forbidden internal identifiers, selected outcome, and preservation of required exact values. Retry once with tighter constraints when output is malformed or policy-inconsistent.
- [x] Use localized static templates only after LM Studio/service failure, timeout, or unusable response after recovery. Remove healthy-model template branches for greetings, unsupported scope, missing evidence, ambiguous matches, and deterministic catalog/recipe narration; verified facts still come from deterministic sources.
- [x] Keep response addressing/delivery formatting in the game/Discord adapters, not in model-generated caller identity text. Preserve 2,000-character delivery limits.
- [x] Add tests proving ordinary generated answers are selected on success, templates are used only for generation failure, and required verified values/units/translation strings cannot be dropped or changed.
- [x] Run focused response/router tests and Release build.

**Acceptance:** When the model is healthy, “I don’t know,” clarifications, small talk, refusals, stats, recipes, and lore all receive model-composed wording; facts remain grounded and exact. Static fallback is observable only on failure paths.

### Task 5: Add safe runtime profile tuning and migration

**Files:** `SparkyProfileSnapshot.cs`, `SparkyProfileValidator.cs`, `SparkyProfileManager.cs`, `LocalModelAnswerer.cs`, Admin profile endpoints/UI, profile tests.

- [x] Add versioned profile fields for bounded conversation history budget, answer temperature and top-p; preserve existing model ID, completion-token, timeout, retrieval, persona, and route-alias settings.
- [x] Keep interpreter temperature fixed/deterministic; apply tuning only to answer composition and summary generation. Clamp all settings in code even if the browser UI submits altered JSON.
- [x] Implement explicit schema-v1-to-current migration with conservative defaults. Keep Dev and Live rows/revisions isolated; invalid migration leaves the existing active snapshot available and rejects activation.
- [x] Expose context and inference settings in the existing profile editor with range hints and validation errors. Display which values are used by active and draft revisions.
- [x] Record model ID, profile revision, bundle version, and effective tuning fields in replay metadata; do not log private context or secrets.
- [x] Add profile-validator and manager tests for bounds, legacy migration, malformed drafts, active-snapshot stability, and Dev/Live separation.
- [x] Run profile tests and Release build.

**Acceptance:** Existing profiles load with safe defaults. New answer settings can be saved to a draft, replayed, activated in Dev, and rolled back without restarting Sparky; Live remains unchanged.

### Task 6: Upgrade evaluation cases and replay to ordered conversations

**Files:** `SparkyTraceModels.cs`, `SparkyTraceStore.cs`, `SparkyReplayService.cs`, `SparkyAdminEndpoints.cs`, Admin `index.html`, `app.js`, `styles.css`, replay/store tests.

- [x] Replace the single-question evaluation shape with an ordered list of user turns and optional per-turn expectations for scope, intent, resolved entity, source IDs, required facts, and forbidden claims. Keep an optional sequence-level note.
- [x] Add an additive SQLite schema migration that reads each existing stored question as a one-turn sequence. Do not delete or rewrite existing trace, feedback, review, or replay rows during migration.
- [x] Add API validation for sequence length, per-turn size, total text size, expectation count, and result serialization size. Preserve trace provenance when a case is seeded from a selected request.
- [x] Update the Admin Panel evaluation editor to prefill the selected trace as the first turn and support adding, deleting, and reordering follow-ups, plus per-turn expectations.
- [x] Replay each sequence in order with the same ephemeral context semantics as live, but use a new context for every case and separate contexts for active and draft. Never enqueue delivery or access live caller state.
- [x] Store and display per-turn scope, intent, contextualized query, selected source IDs, structured-fact summary, answer, and active-vs-draft differences.
- [x] Add tests for legacy one-turn reads, sequence persistence, ordering, context isolation between cases and profiles, active/draft comparison, and size limits.
- [x] Run SQLite/replay tests and Release build.
- [ ] Manually review the Admin Panel sequence flow in the browser.

**Acceptance:** A reviewed evaluation can encode “T3 sensor amplifier stats” → “Da Gama” and “and its full recipe?”; replay shows each turn and its resulting retrieval/answer for active and draft revisions without touching game/Discord.

### Task 7: Integrate, document, and verify on Dev only

**Files:** `src/Perpetuum.ItemStatsExporter/README.md`, `docs/codebase/ARCHITECTURE.md`, `docs/codebase/INTEGRATIONS.md`, `docs/codebase/STRUCTURE.md`, new operator guide.

- [x] Document API compatibility, context/rank privacy, expiry/reset behavior, source-specific rank mapping, profile tuning, evaluation-sequence review, fallback behavior, and rollback.
- [x] Run the complete relevant `Perpetuum.Tests` suite and Release builds for `Perpetuum`, `Perpetuum.ItemStatsExporter`, and the installer project. Do not deploy/restart Live.
- [ ] Run Dev evaluation sequences for: T3 sensor amplifier clarification and stats; follow-up full recipe; multi-turn lore references with two similar names; clear topic change; expired/missing context; multilingual switch; supported in-game politics; real-world politics refusal; small talk; prompt injection; privileged and ordinary rank wording; model timeout and malformed output.
- [ ] Inspect Dev traces for per-turn interpretation/query/source/answer alignment. Verify no IDs, raw roles, hidden reasoning, or context appear when content capture is disabled.
- [ ] Verify source-only delivery remains intact: game callers receive game chat/email under existing online/offline rules; Discord callers receive only the Discord reply; the 2,000-character cap remains enforced.
- [ ] Verify profile migration with a backup of the local Admin Panel database and prove rollback restores the previously active Dev revision. Keep Live disabled/unmodified during this work.

**Acceptance:** Local Dev passes the approved conversation criteria and existing item/robot/lore behavior; no runtime deployment to Live occurs as part of this plan.

## Suggested Verification Commands (implementation phase only)

Run from the repository root after implementation. These commands are part of the plan; do not run them while only reviewing this plan.

```powershell
dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release --no-restore
dotnet build src/Perpetuum/Perpetuum.csproj -c Release --no-restore
dotnet build src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj -c Release --no-restore
dotnet build src/Perpetuum.ItemStatsExporterInstaller/Perpetuum.ItemStatsExporterInstaller.wixproj -c Release --no-restore
```
