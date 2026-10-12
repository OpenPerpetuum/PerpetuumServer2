# Sparky Knowledge Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand Sparky into a multilingual, source-grounded Perpetuum assistant with natural address forms, verified item data, local mechanics/lore retrieval, bounded small talk, and a safe manual knowledge refresh.

**Architecture:** Build a versioned self-contained knowledge bundle on the Main development machine from the local database copy, repository, available custom dictionaries, and explicitly allowlisted lore files under `E:\Perpetuum-lore`. Transfer only that validated bundle to the AI machine, where the existing loopback HTTPS service uses LM Studio for language interpretation and grounded phrasing. Keep caller identity, chat delivery, and short-lived context on the game server; do not add a live database path or public listener.

**Tech Stack:** .NET 8 / C# 12, existing ASP.NET Core loopback service, existing LM Studio OpenAI-compatible API, current server entity and production calculations, PowerShell refresh tooling, JSON bundle.

**Spec:** `docs/superpowers/specs/2026-10-12-sparky-knowledge-expansion-design.md`

## Global Constraints

- Use only custom dictionary languages included and validated in the bundle; `customDictionary/0.json` is the canonical public translation-key inventory.
- Use the local database copy, Main-machine repository, and the explicitly named lore files under `E:\Perpetuum-lore` only while building the bundle; the AI machine reads the bundle and has no direct database, repository, or lore-directory access.
- Keep the existing reverse-SSH tunnel and authenticated HTTPS path; do not add a public listener, third-party inference, external search, or direct live database access.
- Do not send account IDs, character IDs, Discord IDs, online state, account data, or private player data to the AI machine or LM Studio.
- Do not retain full conversation history or persistent per-player facts; retain only the existing bounded, volatile last-item and response-language hints.
- Preserve the 2,000-character response cap on game and Discord output.
- Do not expose source paths, internal definition names, SQL identifiers, or implementation details in player responses.
- Local acceptance uses the Main machine's debug server; do not deploy to or restart the live VDI.
- A missing term, missing translation, weak evidence, conflicting source, or unsupported question must not produce a guessed answer or a partial recipe represented as complete.

## Review Focus

1. A partial dictionary must not create a partial recipe or invented field/material name. Manually inspect a language with incomplete coverage and verify the localized missing-term response asks for the English term.
2. An in-game mention of “Sparky” outside a direct start/end address must remain ordinary chat, while a real Discord bot mention in natural wording is handled and a plain-text name is ignored.
3. A short follow-up must reuse the previous supported language, while an explicit language switch updates it without retaining conversation text.
4. A mechanics/lore query with no relevant or conflicting passages must return a localized clarification/abstention, not unsupported game facts.
5. A malformed bundle or failed activation must leave the previous known-good bundle usable and must not require restarting the game server.

---

## File Map

**Modify:**

- `src/Perpetuum/Services/Channels/ChannelManager.cs` — recognize natural in-game direct-address forms after existing membership, mute, and admin-command checks.
- `src/Perpetuum/Services/EventServices/EventListenerService.cs` — keep Discord admission tied to the actual bot account mention and clean the mention's adjacent punctuation before forwarding the question.
- `src/Perpetuum/Services/Sparky/SparkyApiClient.cs` and `SparkyRequestCoordinator.cs` — carry only optional language context and return the selected language to local volatile context.
- `src/Perpetuum/Services/Sparky/SparkyContextStore.cs` — store the last public item name and supported language ID with the existing expiry/capacity behavior.
- `src/Perpetuum/Services/EventServices/EventProcessors/SparkyResponseProcessor.cs` — render localized fallback outcomes while preserving chat/mail/Discord delivery rules.
- `src/Perpetuum.ItemStatsExporter/Program.cs` — add bundle build/validation CLI commands while preserving existing item export, `--answer`, `--ask`, and `--serve` usage where practical.
- `src/Perpetuum.ItemStatsExporter/ItemStatsLookup.cs` — resolve aliases and display data from the selected dictionary without changing server-derived stats or recursive recipe math.
- `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs` and `SparkyHttpService.cs` — route supported request types, send only bounded verified facts/passages to LM Studio, return response language, expose bundle version on health, and retain existing HTTPS/auth/logging/length limits.
- `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs` — version-compatible request/response fields for optional context language and returned language ID.
- `src/Perpetuum.ItemStatsExporter/README.md`, `docs/codebase/ARCHITECTURE.md`, `docs/codebase/INTEGRATIONS.md`, and `docs/codebase/STRUCTURE.md` — document the new local build/runtime/refresh responsibilities.

**Create:**

- `src/Perpetuum/Services/Sparky/SparkyQuestionParser.cs` — in-game natural address extraction with one explicit question-only output.
- `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs` — versioned bundle, supported-language records, source manifest, and provenance models.
- `src/Perpetuum.ItemStatsExporter/SparkyDictionaryCatalog.cs` — language-ID map and loaded dictionary access, using the mapping in `src/Perpetuum/CustomDictionary.cs`.
- `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs` — local language/intent/search-query interpretation with a typed, validated result.
- `src/Perpetuum.ItemStatsExporter/SparkyResponseTemplates.cs` — localized fixed fallback and small-talk templates keyed by supported game language ID.
- `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeSourceCatalog.cs` — repository-relative allowlist and filters for approved public knowledge sources.
- `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeIndexBuilder.cs` and `SparkyKnowledgeRetriever.cs` — bounded offline index creation and deterministic top-passage retrieval.
- `tools/sparky-knowledge-sources.json` — reviewed allowlist for mechanics/lore documents, server-code files, and approved public database extracts.
- `tools/Refresh-SparkyKnowledge.ps1` and `docs/superpowers/sparky-knowledge-refresh.md` — staged AI-machine activation, health verification, rollback, and operator instructions.

The knowledge bundle is the shared boundary between the Main machine and AI machine. Keep the deterministic item calculations in the existing exporter path; do not move game calculations into LM Studio prompts or add a second database reader.

---

### Task 1: Natural Addressing on Game Chat and Discord

**Files:**

- Create: `src/Perpetuum/Services/Sparky/SparkyQuestionParser.cs`
- Modify: `src/Perpetuum/Services/Channels/ChannelManager.cs`
- Modify: `src/Perpetuum/Services/EventServices/EventListenerService.cs`

**Interfaces:**

- Produces: `internal static bool TryExtractGameQuestion(string message, out string question)`.
- Consumes: the current in-game channel checks and Discord `MentionedUsers` account-ID check.

- [ ] **Step 1: Check existing channel dependencies before changing ingress.**

Run:

```powershell
.\tools\query-graph.ps1 ChannelManager -Direction in
.\tools\query-graph.ps1 EventListenerService -Direction in
```

Record the `ChannelManager.cs` node separately if the first command lists interface/extension candidates. Preserve the current `ChannelManager` check order: membership, mute, admin-command, broadcast/bridge, then Sparky submission.

- [ ] **Step 2: Add a parser that recognizes only direct game address forms.**

Implement `TryExtractGameQuestion` to recognize case-insensitive leading `Sparky,`, leading `Hey Sparky,` (also `Hi` and `Hello`), and trailing `, Sparky` vocatives. Preserve final `?`, `!`, or `.` when removing a trailing vocative. Return `false` for a plain name match in the middle of a message. Return `true` with an empty question for a bare address so the existing usage result can be used without an AI request.

Use invariant case folding and anchored patterns; do not accept a substring match such as `sparkytech`.

- [ ] **Step 3: Replace the literal `Sparky,` prefix check in `ChannelManager`.**

Call `TryExtractGameQuestion(message, out var question)` at the current trigger location and submit the extracted question through the existing coordinator. Leave original chat broadcast and existing Discord mirroring behavior unchanged.

- [ ] **Step 4: Normalize Discord mention text without weakening mention admission.**

Keep `MentionedUsers.Any(user => user.Id == botUser.Id)` as the admission condition. Strip the actual bot mention and the separator adjacent to it from `CleanContent`; preserve final question punctuation. Do not introduce a plain-text `Sparky` trigger for Discord, and do not forward raw Discord mention IDs to the API.

- [ ] **Step 5: Manually verify address behavior on the Main debug server.**

Check game chat with `Sparky, what are the stats for Standard medium HCL laser?`, `Hey Sparky, how about its recipe?`, `What is the full recipe, right, Sparky?`, and `The NPC mentioned Sparky earlier.` Confirm the last message is ordinary chat. In mapped and unmapped Discord channels, check `Hey, @Sparky, how about ...?`, a trailing actual bot mention, a plain-text `Sparky` without a mention, and a bare mention. Confirm answers remain on their source platform.

---

### Task 2: Versioned Multilingual Bundle and Structured Item Data

**Files:**

- Create: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyDictionaryCatalog.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/ItemStatsLookup.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/README.md`

**Interfaces:**

- Produces: `static SparkyDictionaryCatalog Load(string customDictionaryDirectory)` and `SparkyKnowledgeBundle(int SchemaVersion, SparkyBundleManifest Manifest, IReadOnlyList<SparkySupportedLanguage> Languages, IReadOnlyList<WeaponItem> Items, IReadOnlyList<ProductionRecipe> ProductionRecipes, IReadOnlyList<SparkyKnowledgePassage> KnowledgePassages, SparkySearchIndex SearchIndex)`.
- Bundle metadata records `BundleVersion`, `GameContentRevision`, `BuiltAtUtc`, `DictionaryHashes`, and `SourceHashes`. Each `SparkySupportedLanguage` contains `LanguageId`, `DisplayName`, and its dictionary hash. Each passage contains `SourceId`, `SourceCategory`, `SourceRevision`, `Sha256`, and bounded `Text`.
- Consumes: `OfflineContentRuntime.Initialize(gameRoot)`, existing entity/aggregate export code, and existing recipe/full-material projection.

- [ ] **Step 1: Define the bundle schema and manifest before changing the exporter.**

Add a schema version and a manifest containing the server-content revision, each included dictionary ID and SHA-256, index build time, source identifiers/hashes, and bundle version. Include no machine credentials, private keys, certificates, raw connection strings, or player/account records.

- [ ] **Step 2: Load dictionaries from the directory and validate language IDs.**

Use `CustomDictionary.cs`'s authoritative game-ID mapping; accept only numeric `*.json` filenames with valid JSON objects. Require `0.json`, use its keys as the canonical translation-key inventory, and preserve the current rule that an untranslated optional key is omitted. Do not invent a locale name or game-language mapping from a filename.

- [ ] **Step 3: Preserve existing entity calculations while projecting localized views.**

Keep `OfflineContentRuntime`, `Entity.Factory`, `WeaponModule` property calculation, `AggregateFieldMetadata` conversion, `ProductionComponent.EffectiveAmount`, and recursive recipe expansion as the sources of numeric values. For each included dictionary, create localized item aliases, stat labels, units, ammo names, recipe component names, and terminal-material names from matching translation keys.

Build a recipe answer view only when every required displayed component name exists in that language. Mark missing localized data explicitly so lookup can ask for an English item term rather than returning a partial recipe as complete.

- [ ] **Step 4: Add explicit build and validate commands.**

Keep `--serve <bundle> [--config <settings>]`, `--answer`, and `--ask`; add `--build-knowledge <game-root> <custom-dictionary-dir> <source-root> <lore-root> <bundle-path>` and `--validate-bundle <bundle-path>`. The existing Main-machine paths are:

```powershell
$gameRoot = 'G:\PerpetuumServer\data'
$dictionaryDirectory = 'E:\MyStuff\Projects\OPDB\Patches\Live_36\Server\data\customDictionary'
$sourceRoot = 'E:\MyStuff\Projects\PerpetuumServer2'
$loreRoot = 'E:\Perpetuum-lore'
$bundlePath = 'E:\MyStuff\Projects\PerpetuumServer2\sparky-knowledge.bundle.json'
```

The build command must reject absent `0.json`, malformed dictionaries, unsupported language IDs, invalid bundle schema, and an incomplete manifest with actionable errors.

- [ ] **Step 5: Manually compare English structured answers to the accepted baseline.**

Build the bundle from the local database copy and use `--answer` or the secured local API for Standard medium HCL laser, an ammo variant, a direct recipe, and full recursive materials. Confirm numeric values match the current accepted `output.json` projection. Check one other language with a complete translated item and one with a missing required recipe term; the latter must not produce a partial recipe.

---

### Task 3: Response Language, Follow-up Context, and Localized Outcomes

**Files:**

- Create: `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyResponseTemplates.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/ItemStatsLookup.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyApiClient.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyContextStore.cs`
- Modify: `src/Perpetuum/Services/Sparky/SparkyRequestCoordinator.cs`
- Modify: `src/Perpetuum/Services/EventServices/EventProcessors/SparkyResponseProcessor.cs`

**Interfaces:**

- Produces: `SparkyQuestionInterpretation(int LanguageId, SparkyIntent Intent, string? EnglishSearchQuery)`.
- `SparkyIntent` values are `StructuredItem`, `MechanicsLore`, `Combined`, `SmallTalk`, and `Unsupported`.
- Produces: `Task<SparkyQuestionInterpretation> InterpretAsync(string question, IReadOnlyList<SparkySupportedLanguage> supportedLanguages, int? contextLanguageId, CancellationToken cancellationToken)`.
- Produces: `SparkyContextEntry(string? ItemDisplayName, int? LanguageId)` held only in the game server's existing bounded volatile store.
- `SparkyApiRequest(string? Question, string? ContextItem = null, int? ContextLanguageId = null)` adds optional language context; `SparkyApiResponse` adds nullable `LanguageId`. Never add caller, character, account, or Discord identifiers to this API model.
- `SparkyContextStore.GetAndTouch(SparkyRequestIdentity identity, DateTimeOffset now)` returns `SparkyContextEntry?`; `Set(SparkyRequestIdentity identity, string? itemDisplayName, int? languageId, DateTimeOffset now)` updates the bounded entry.
- `SparkyApiClient.AnswerAsync(string question, string? contextItem, int? contextLanguageId, CancellationToken cancellationToken)` carries that optional context and returns the selected language in `SparkyApiResult`.

- [ ] **Step 1: Resolve language IDs from bundle metadata and validate model interpretation.**

Use the configured local LM Studio model to identify the response language and request route for each message, and to normalize mechanics/lore queries into bounded English search terms. Combine these in one compact interpretation call capped at 128 completion tokens. Give it only the question, supported language IDs/names, and optional prior language ID. Require a parseable supported ID and a bounded query string; on malformed model output, use a localized unsupported/clarification template instead of guessing. Keep the interpretation call within the existing configurable timeout and measure added latency during local acceptance.

Use prior context only when the current message is too short or ambiguous to classify. If a substantive later message clearly uses another supported language, update context to that language.

- [ ] **Step 2: Resolve structured aliases in the selected language, then English.**

Change `ItemStatsLookup.Resolve` to consume the bundle and selected language ID. Match aliases from that language first, then from language 0. Return the chosen language's verified display data. For a missing term, respond in the caller language and ask for an English term. Do not expose definition names or dictionary keys.

- [ ] **Step 3: Add fixed localized outcome templates.**

Add the required supported-language templates for missing term, ambiguous item, unsupported evidence, busy, unavailable, question too long, usage, greeting, acknowledgement, thanks, and short in-character banter. Validate that every language advertised as supported has these templates. Preserve existing English behavior for language 0.

For game callers, use the caller's existing language setting only for local pre-admission failures that cannot reach the AI service. For Discord, use the last language context when available; if neither source has a language yet, use the English fallback and retain no external account/profile data.

- [ ] **Step 4: Carry only selected language and public item context over existing API.**

Extend request/response DTOs and `SparkyApiClient.AnswerAsync` with optional `ContextLanguageId` and returned `LanguageId`. Update `SparkyContextStore` to return and store `(ItemDisplayName, LanguageId)` under the same source identity, expiry, and capacity. Keep `SparkyRequestIdentity` local to the game server.

- [ ] **Step 5: Make the answer prompt return the caller-language answer.**

Pass the selected language and localized verified facts to LM Studio. Request a bounded answer and language ID in the response. Reject an unsupported returned language ID, empty content, invalid structured output, or output over the configured character cap; use a localized template or the existing generic service error path.

Preserve content-free local diagnostics that distinguish queue saturation, timeout, tunnel failure, certificate/authentication failure, model failure, and Discord delivery failure. Keep player-visible errors generic and source-platform-only.

- [ ] **Step 6: Manually check language continuity and term fallback.**

For each bundle-supported language, ask for an item, its stats, direct recipe, full materials, and a short follow-up. Include an English item name inside a non-English question and confirm the answer stays in the question language. Use a missing localized name and confirm the answer asks for its English term. Switch language in a later message and confirm the new language is used. Check that the local log contains no question or answer text.

---

### Task 4: Curated Offline Mechanics and Lore Index

**Files:**

- Create: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeSourceCatalog.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeIndexBuilder.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeRetriever.cs`
- Create: `tools/sparky-knowledge-sources.json`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs`

**Interfaces:**

- Produces: `SparkyKnowledgePassage` with stable source ID, source category, revision/hash, and bounded text.
- Produces: `IReadOnlyList<SparkyKnowledgePassage> Search(string englishQuery, int maxPassages, int maxCharacters)`.
- `SparkyKnowledgeIndexBuilder.Build(string repositoryRoot, string loreRoot, SparkyKnowledgeSourceCatalog sources, CancellationToken cancellationToken)` creates the bundle's index from the reviewed allowlist.
- Consumes: repository-relative mechanics sources, exact lore filenames from the supplied lore root, local Main-machine repository, and local database copy only.

- [ ] **Step 1: Review and constrain candidate knowledge sources.**

Start from `docs/` and mechanics-related server code. Add only reviewed repository-relative files to `tools/sparky-knowledge-sources.json`. Lore entries must name a direct-child `.txt` file under the explicitly supplied lore root; the builder must not enumerate that folder. Exclude account/character data, secrets, credentials, authentication/authorization internals, administrative-only behavior, operational files, generated output, build output, and logs. The builder must reject paths outside their configured roots and must not recursively index either root by default.

- [ ] **Step 2: Verify any database source against authoritative schema documentation before extraction.**

Before adding a query, read the relevant `docs/db_structure/` table documentation and confirm each join and column. Reuse existing entity/recipe extraction when it already represents player-visible content. Add only explicitly allowlisted public lore/mechanics data from the local database copy; never connect to VDI or accept SQL supplied by a player/model.

- [ ] **Step 3: Build bounded chunks and provenance into the bundle.**

Normalize approved text, split it into stable bounded passages, and store a source ID plus SHA-256 and source revision for each passage. Do not include filesystem paths in player-facing content. Keep the index JSON size and passage count bounded by explicit build limits.

- [ ] **Step 4: Add deterministic retrieval with bounded results.**

Use a local lexical index with token normalization and a deterministic relevance score; do not add an external search or vector service. Search using the English query returned by `SparkyQuestionInterpreter`. Return only a small top-ranked passage set within a configured character budget appropriate for the existing 4,096-token model context. If no passage clears the relevance threshold, return no evidence.

- [ ] **Step 5: Manually inspect provenance and retrieval outcomes.**

For representative mechanics/lore topics, confirm the returned passages support the answer and have source hashes. Ask a question whose answer is absent and one whose sources conflict; verify both yield clarification/abstention. Inspect the finished bundle for excluded file categories, raw paths, account rows, and credentials before transfer.

---

### Task 5: Grounded Answer Routing and Small Talk

**Files:**

- Create: `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyResponseTemplates.cs`

**Interfaces:**

- Produces: `SparkyAnswerResult(string Status, string? Answer, string? Item, int? LanguageId)`.
- Produces: `Task<SparkyAnswerResult> AnswerAsync(string question, SparkyQuestionInterpretation interpretation, SparkyKnowledgeBundle bundle, string? contextItem, CancellationToken cancellationToken)`.
- `SparkyResponseTemplates.Get(int languageId, SparkyResponseTemplateKey key)` returns only a prevalidated template for a supported bundle language.
- Consumes: interpreted intent, structured item facts, retrieved source passages, and localized templates.

- [ ] **Step 1: Route question classes before answer generation.**

Try the deterministic item/recipe route first. Route mechanics/lore to retrieved passages, combined mechanics plus item questions to both verified facts and passages, greetings/acknowledgements/thanks/light banter to the bounded small-talk route, and unrelated general questions to the localized unsupported template. A route classification never counts as evidence.

- [ ] **Step 2: Supply only the evidence required for that route.**

For structured answers, send the localized `AnswerView` data. For mechanics/lore, send only top-ranked approved passages plus the normalized question and response language. For combined answers, send both evidence sets. For small talk, send no game or database excerpts. Treat retrieved text and user text as untrusted content; neither may override the system's source-only answer rules.

- [ ] **Step 3: Enforce abstention and output limits after generation.**

If retrieval is empty, weak, or conflicting, do not ask the model to guess. Use the localized unsupported/clarification template. Limit the answer to 2,000 characters for both platforms and keep it concise enough for the existing in-game mail/chat transport.

- [ ] **Step 4: Manually check route boundaries.**

Check item stats, direct recipe, full materials, “what is the full recipe to create it?” follow-up, mechanics, lore, a combined item/mechanics question, greeting, thanks, light banter, unrelated factual query, and a prompt attempting to override instructions. Confirm unsupported or ungrounded questions do not receive factual claims.

---

### Task 6: Validate, Transfer, Activate, and Roll Back Bundles

**Files:**

- Create: `tools/Refresh-SparkyKnowledge.ps1`
- Create: `docs/superpowers/sparky-knowledge-refresh.md`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs` only if bundle-validation limits need explicit configuration.
- Modify: `src/Perpetuum.ItemStatsExporter/README.md`

**Interfaces:**

- Produces: `--validate-bundle <bundle-path>` exit status and safe `/health` metadata containing `status` and `bundleVersion` only.
- Refresh script accepts a staged bundle already transferred to the AI machine and defaults the installed service name to `Perpetuum.Sparky` and active bundle path to `C:\ProgramData\Sparky\output.json`.

- [ ] **Step 1: Validate the full bundle before service activation.**

The validator must check schema version, required sections, source/dictionary hashes, supported-language template coverage, alias uniqueness/ambiguity records, recipe completeness flags, passage bounds, and forbidden private-data markers. The service must refuse an invalid bundle at startup.

- [ ] **Step 2: Make health identify the active version without exposing internals.**

Extend `/health` to return only `status` and the bundle version/hash prefix. Do not return the active file path, source paths, credentials, model prompt, caller, or question/answer text.

- [ ] **Step 3: Stage and activate atomically on the AI machine.**

Copy the transferred bundle into a staging file on the same volume as the active bundle. Validate it before stopping `Perpetuum.Sparky`. Keep a timestamped known-good copy, replace the active file, start the service, and poll health for the expected bundle version. If startup or version verification fails, restore the previous bundle and restart the Sparky service. Do not restart the game server.

- [ ] **Step 4: Document repeatable refresh and recovery.**

Document the Main-machine build command, bundle hash verification during transfer, AI-machine staged refresh command, health result, backup retention, and rollback command. State that the same published `Perpetuum.ItemStatsExporter.exe` is used for command-line export/validation and the installed Windows service; deploy the updated executable to both existing AI-machine locations when the implementation changes.

- [ ] **Step 5: Manually simulate refresh success and failure locally.**

Activate a valid bundle while the Main debug game server remains running. Then stage a malformed bundle and confirm it is rejected before activation. Simulate a post-replacement service startup failure and confirm the script restores and verifies the previous bundle. Confirm no service token or certificate is copied to Main or included in the bundle.

---

### Task 7: Local End-to-End Acceptance and Documentation

**Files:**

- Modify: `src/Perpetuum.ItemStatsExporter/README.md`
- Modify: `docs/codebase/ARCHITECTURE.md`
- Modify: `docs/codebase/INTEGRATIONS.md`
- Modify: `docs/codebase/STRUCTURE.md`
- Create/update: `docs/superpowers/sparky-knowledge-refresh.md`

- [ ] **Step 1: Build and validate the Main-machine bundle.**

Use the paths from Task 2, inspect the build report for every included language and source, and run `--validate-bundle` before transferring anything to the AI machine.

- [ ] **Step 2: Update the AI-machine command-line and service copies.**

Publish the .NET 8 `win-x64` self-contained exporter/service from the reviewed source:

```powershell
dotnet publish src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o publish/SparkyItemStatsExporter
```

Copy that same published output to `D:\Sparky\SparkyItemStatsExporter` for CLI use and `C:\Program Files\PerpetuumSparky` for the Windows service. Use the refresh script for the bundle at `C:\ProgramData\Sparky\output.json`.

- [ ] **Step 3: Rebuild and run only the Main development server locally.**

Rebuild the local solution and launch the Main server from Visual Studio Debug using `G:\PerpetuumServer\data\perpetuum.ini`. Confirm `[Sparky] Game and Discord request adapter enabled.` and run all manual scenarios from the Review Focus section on this local instance only.

- [ ] **Step 4: Finish operator and architecture documentation.**

Document which files are built on Main, which files run on the AI machine, supported-language reporting, bundle refresh, health version, source-only response routing, and the explicit later approval gate for any VDI rollout.

---

## Manual Acceptance Checklist

1. Build from the local database copy and every configured dictionary; absent `0.json`, malformed JSON, and an unknown language ID each fail clearly.
2. For every supported language, inspect a translated item, stats, direct recipe, recursively expanded materials, and a terse follow-up; the numeric values remain identical across languages.
3. Ask with an English item term in a non-English question and with an untranslated localized term; confirm the first answer remains in the caller language and the second asks for an English term in that language.
4. Verify a missing required recipe translation never creates a partial recipe, and a missing aggregate translation follows the existing omit-unknown-key rule.
5. Verify source-backed mechanics/lore, combined questions, no-evidence questions, ambiguous sources, and an instruction-injection attempt. Confirm item answers use structured bundle facts and explanation passages retain source provenance.
6. Verify greetings, acknowledgements, thanks, and light banter in supported languages, plus an unrelated factual question that must stay outside the small-talk route.
7. Verify Main-machine bundle build, transfer hash, AI-machine activation, health version, malformed bundle rejection, rollback, and continued Main debug server operation. Confirm refresh restarts only the Sparky service.
8. Verify natural game address variants, Discord actual mentions in natural positions, ignored text-only Discord names, source-platform-only answers, online game chat delivery, offline game mail delivery, and the 2,000-character cap. Recheck busy/service/model failures and the previously silent Discord scenario locally.
9. Verify no live DB access, external services, player identifiers in API payloads, private/account data in the bundle, or question/answer content in new logs.
10. Keep VDI and live-server rollout out of this plan; that requires a separate acceptance decision after local reliability is proven.
