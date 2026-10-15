# Sparky Contextual Comparison and Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Make Sparky compare verified item and robot facts across bounded conversation turns, and make interpretation, resolution, and context handling diagnosable through the Admin Panel and sanitized trace exports.

**Architecture:** The interpreter will report comparison intent, subject references, metric phrase, and comparison direction. A shared coordinator will resolve references against the active bundle and recent source-scoped dialogue, then dispatch to item or robot providers that return common-field facts; code computes numeric differences and the answer model only phrases those verified facts. The AI-side trace pipeline will separately retain safe metadata and content-gated context/resolution details, with additive SQLite migration, versioned export, Admin Panel rendering, and multi-turn Dev replay coverage.

**Tech Stack:** .NET 8 / C#, existing local LM Studio chat-completions API, existing Sparky knowledge bundle, SQLite trace store, xUnit, and the existing vanilla JavaScript Admin Panel.

**Spec:** docs/superpowers/specs/2026-10-15-sparky-contextual-comparison-and-diagnostics-design.md

## Global Constraints

- First comparison domains are structured item facts and robot facts.
- Lore, mechanics, seasons, and other domains are deferred and may be added later through domain-specific evidence providers.
- Do not build a universal comparison framework or compare from arbitrary model knowledge.
- Use the existing local LM Studio model for contextual interpretation and natural phrasing; use code and the approved local knowledge bundle to validate subjects and calculate numeric comparisons.
- Preserve the current AI/Main/Live separation, short-lived source-scoped game-server context, local database-copy workflow, Dev/Live profiles, API security, response limits, and explicit promotion process.
- No Live deployment, live database access, player identity sent to AI, cross-platform identity linking, persistent player memory, hidden reasoning capture, or automatic model training.
- With content capture disabled, persist no question, answer, dialogue text, query, entity name, or comparison value; always retain only bounded diagnostic metadata and stable public source IDs.
- Diagnostic exports include content only when profile content capture is enabled and the operator selects include content.
- Do not commit changes unless the user explicitly asks.

## Review Focus

- “Which one is faster?” after two robot turns must resolve the contextual pair and must not invoke the global fastest ranking. Test in Task 2.
- More than one plausible pair or entity reference must produce clarification instead of selecting arbitrary recent subjects. Test in Task 2.
- “Which is better?” and unsupported or missing attributes must request a criterion or report unavailable data without inventing a winner. Test in Task 3.
- A clear new topic must prevent stale comparison subjects from being reused. Test in Task 2.
- Content capture disabled, export content unchecked, malformed or oversized diagnostic details, and existing trace databases must preserve privacy, size bounds, and readability. Test in Tasks 4 and 5.

---

### Task 1: Add a bounded comparison intent contract to interpretation

**Files:**
- Create: src/Perpetuum.ItemStatsExporter/SparkyComparisonModels.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs
- Modify: src/Perpetuum.Tests/Unit/SparkyConversationInterpretationTests.cs
- Create: src/Perpetuum.Tests/Unit/SparkyComparisonInterpretationTests.cs

**Interfaces:**
- Add SparkyComparisonIntent with IsComparison, IsContextual, SubjectReferences, MetricPhrase, Direction, and CompareAllSharedFields.
- Add SparkyComparisonDirection values Higher, Lower, and Unspecified.
- Add optional Comparison to SparkyQuestionInterpretation; do not put caller or platform identity in it.
- The parser accepts no more than eight subject references of at most 64 characters so it can represent ambiguity, a metric phrase of at most 80 characters, and only defined direction values. The intent is a proposal, not evidence.

- [ ] **Step 1: Write parser tests for valid contextual follow-ups and invalid comparison payloads**

Add tests in SparkyComparisonInterpretationTests for a valid JSON result representing “Which one is faster?” with contextual comparison, no explicit subject names, metric phrase “maximum speed,” and Higher direction. Verify three subjects parse so the resolver can clarify ambiguity; reject nine subjects, an overlong metric, an unsupported direction, and comparison metadata attached to an unrelated intent.

~~~~csharp
Assert.True(SparkyQuestionInterpreter.TryParseConversationInterpretation(
    comparisonJson, SupportedLanguages, 0, null, out var parsed));
Assert.True(parsed.Comparison!.IsContextual);
Assert.Equal("maximum speed", parsed.Comparison.MetricPhrase);
~~~~

- [ ] **Step 2: Run the focused test and confirm the new contract is missing**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyComparisonInterpretationTests

Expected: compilation or assertion failure because the comparison model and parser support do not exist yet.

- [ ] **Step 3: Implement the comparison DTO and extend the interpreter prompt and parser**

Add the comparison fields to the JSON contract documented in SparkyQuestionInterpreter.SystemPrompt. The prompt must distinguish contextual pair comparison from global ranking and instruct the model to return at most eight subject references, use bounded prior dialogue only for references, and request clarification when more than two plausible subjects remain. Parse and validate the bounded fields, preserving existing behavior when comparison is absent or invalid.

~~~~csharp
internal enum SparkyComparisonDirection { Higher, Lower, Unspecified }

internal sealed record SparkyComparisonIntent(
    bool IsComparison,
    bool IsContextual,
    IReadOnlyList<string> SubjectReferences,
    string? MetricPhrase,
    SparkyComparisonDirection Direction,
    bool CompareAllSharedFields);
~~~~

- [ ] **Step 4: Run focused and adjacent interpretation tests**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyComparisonInterpretationTests|FullyQualifiedName~SparkyConversationInterpretationTests"

Expected: PASS, with existing interpretation JSON still accepted when the optional comparison property is null.

---

### Task 2: Resolve contextual subjects and prevent stale or global-ranking route mistakes

**Files:**
- Create: src/Perpetuum.ItemStatsExporter/SparkyComparisonSubjectResolver.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyRobotIntentResolver.cs
- Modify: src/Perpetuum.Tests/Unit/SparkyRobotLookupTests.cs
- Create: src/Perpetuum.Tests/Unit/SparkyComparisonSubjectResolverTests.cs

**Interfaces:**
- Add SparkyComparisonSubject with Domain, DefinitionName, DisplayName, and ReferenceSource.
- Add SparkyComparisonSubjectResolution with Status, Domain, Subjects, and CandidateNames.
- Expose one resolver method: Resolve(SparkyComparisonIntent intent, string question, string? englishSearchQuery, IReadOnlyList<SparkyConversationTurn> contextTurns, SparkyKnowledgeBundle bundle, int languageId).
- A resolved comparison is exactly two subjects from the same supported domain. Zero, ambiguous, cross-domain, and more-than-two candidates return distinct non-success statuses for clarification.

- [ ] **Step 1: Write tests for the Kain/Tyrannos context and explicit entity pairs**

Build synthetic Kain and Tyrannos entries by cloning the robot fixture records and assigning their display names, definition names, and speed values. Build history with user/assistant turns about each robot, then resolve an empty-subject contextual comparison. Assert the subject definitions are the two fixture records in conversational order. Add explicit two-item and explicit two-robot cases using existing bundle fixtures.

~~~~csharp
var resolution = resolver.Resolve(intent, "Which one is faster?", null, history, bundle, 0);
Assert.Equal("resolved", resolution.Status);
Assert.Equal(new[] { "def_kain", "def_tyrannos" },
    resolution.Subjects.Select(subject => subject.DefinitionName));
~~~~

- [ ] **Step 2: Run the focused subject resolver tests and confirm the expected failure**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyComparisonSubjectResolverTests

Expected: FAIL until contextual subject resolution is implemented.

- [ ] **Step 3: Implement catalog-backed subject extraction and ambiguity handling**

For each bounded prior user turn, resolve item mentions through ItemStatsLookup and robot mentions through SparkyRobotLookup.FindMentionedRobots. Merge only unique definition names, ordered by their latest relevant user mention; ignore assistant prose as a source of new entities. For explicit references, resolve against the current question and interpreter-proposed names. Accept a pair only when both subjects are catalog-backed and in the same domain. Do not consult prior turns when IsContextual is false. Return clarification when more than two current candidates remain.

~~~~csharp
SparkyComparisonSubjectResolution Resolve(
    SparkyComparisonIntent intent,
    string question,
    string? englishSearchQuery,
    IReadOnlyList<SparkyConversationTurn> contextTurns,
    SparkyKnowledgeBundle bundle,
    int languageId);
~~~~

- [ ] **Step 4: Add route-separation and topic-change regression tests**

Assert “Which one is faster?” with a two-robot history becomes contextual comparison; “Which robot is fastest?” remains a global ranking; three plausible recent robots return ambiguous; and a comparison explicitly marked non-contextual cannot reuse old subjects. Extend SparkyRobotLookupTests for deterministic correction.

- [ ] **Step 5: Run the focused and existing robot-routing tests**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyComparisonSubjectResolverTests|FullyQualifiedName~SparkyRobotLookupTests"

Expected: PASS, including the existing global Fastest behavior.

---

### Task 3: Implement item and robot comparison providers and conversational answers

**Files:**
- Create: src/Perpetuum.ItemStatsExporter/SparkyComparisonCoordinator.cs
- Create: src/Perpetuum.ItemStatsExporter/SparkyItemComparisonResolver.cs
- Create: src/Perpetuum.ItemStatsExporter/SparkyRobotComparisonResolver.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs
- Create: src/Perpetuum.Tests/Unit/SparkyComparisonResolverTests.cs
- Modify: src/Perpetuum.Tests/Unit/SparkyAnswerCompositionTests.cs

**Interfaces:**
- Add SparkyComparisonRequest with Domain, two resolved DefinitionNames, MetricPhrase, Direction, and CompareAllSharedFields.
- Add ISparkyComparisonDomainResolver.Resolve(SparkyComparisonRequest request, SparkyKnowledgeBundle bundle, int languageId).
- Add SparkyComparisonResolution with Status, LocalizedSubjectNames, AttributeLabel, Values, Unit, non-negative Difference, nullable LeadingSubjectName, and DiagnosticCategory.
- Add SparkyComparisonCoordinator.Resolve(request, bundle, languageId, domainResolvers), dispatching only to the matching item or robot provider.
- Providers compare stable FieldId values only within their own domain. Values must parse as invariant-culture decimals and have matching units. The coordinator must not compare an item to a robot.

- [ ] **Step 1: Write provider tests for exact values, difference, missing data, and unit mismatch**

Use existing item and robot bundle fixtures. Verify the Kain/Tyrannos speed comparison returns 103.32 kph and 93.6 kph, a 9.72 kph difference, and Kain as faster. Add a shared-stat item pair, a broad shared-fields request, an unavailable field, and a mismatched-unit case.

~~~~csharp
Assert.Equal("103.32", result.Values[0].Value);
Assert.Equal("93.6", result.Values[1].Value);
Assert.Equal("9.72", result.Difference);
Assert.Equal("Kain", result.LeadingSubjectName);
~~~~

- [ ] **Step 2: Run focused comparison resolver tests and confirm they fail**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyComparisonResolverTests

Expected: FAIL until provider and coordinator behavior exists.

- [ ] **Step 3: Implement the shared resolver contract and domain-specific fact adapters**

Adapt item stats from ItemStatsLookupAnswer.Stats and robot stats from the localized robot AssembledStats. Identify fields by stable FieldId, require equal units, calculate absolute differences with decimal arithmetic, and preserve localized labels and values from the bundle. Set LeadingSubjectName only when the requested comparison direction identifies a leader. Map robot phrases such as “faster,” “speedier,” “top speed,” and “maximum speed” to the existing speed_max field. For other phrases, match normalized requested text against canonical and localized field labels; if zero or multiple shared fields match, return unavailable or ambiguous rather than guessing. For CompareAllSharedFields, return only the intersection of numeric fields with matching units.

- [ ] **Step 4: Route validated comparisons before ordinary item/robot answer branches**

In SparkyAnswerRouter, run subject resolution and provider dispatch when the parsed comparison intent requests a comparison, before deterministic corrections can turn a contextual pair into a global Fastest route. Return a clarification for ambiguity or a missing criterion; return a local-records limitation when the requested fact or value is absent. Pass only the verified resolution to the existing answer composer, attach exact subject names, values, units, and calculated difference as required exact strings, and add route/resolution fields to SparkyAnswerResult for Task 4 diagnostics. Ensure every answer return path carries the effective route using a local result factory or finalizer.

- [ ] **Step 5: Verify phrasing cannot change values and ordinary lookups remain unchanged**

Extend composition tests to require the exact verified values in generated comparison answers and reject altered numbers or an unsupported “better” conclusion. Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyComparisonResolverTests|FullyQualifiedName~SparkyAnswerCompositionTests|FullyQualifiedName~SparkyRobotLookupTests"

Expected: PASS.

---

### Task 4: Capture bounded context and effective resolution diagnostics in the trace store

**Files:**
- Modify: src/Perpetuum.ItemStatsExporter/SparkyTraceModels.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyTraceEventValidator.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyTraceStore.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs
- Modify: src/Perpetuum.Tests/Unit/SparkyTraceEventValidationTests.cs
- Modify: src/Perpetuum.Tests/Unit/SparkyTraceStoreTests.cs
- Create: src/Perpetuum.Tests/Unit/SparkyComparisonTraceTests.cs

**Interfaces:**
- Add metadata fields to SparkyTraceEvent for actual context message count, actual context character count, summary presence, pending-clarification presence, context-budget trimming, effective intent/focus, resolver category/outcome, comparison subject count, requested attribute category, and result category.
- Add content-controlled fields for bounded ContextTurns, ContextSummary, PendingClarification, and SparkyComparisonDiagnostics. These fields may be persisted only when the Dev/Live snapshot has CaptureContent enabled.
- Keep existing Intent and Focus as the parsed interpreter route. Record the post-correction/effective route separately.
- Use additive EnsureColumn migration; old rows remain readable and new event metadata participates in idempotency hashing.
- Define actual context message count as bounded prior user/assistant turns, excluding the current question. Define actual context character count as the text in those turns plus the bounded summary and pending clarification question/candidate names; exclude system instructions and the supported-language catalog.

- [ ] **Step 1: Add trace-store tests for metadata, content gating, and old-schema migration**

Test that metadata survives with CaptureContent false while all new content fields are null. Test that bounded context and comparison values persist with CaptureContent true. Create a database using the previous Trace table columns, initialize SparkyTraceStore, and verify its existing row remains readable.

- [ ] **Step 2: Run the focused persistence tests and confirm expected failures**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyTraceStoreTests|FullyQualifiedName~SparkyComparisonTraceTests"

Expected: FAIL because the new fields and migration columns are absent.

- [ ] **Step 3: Add bounded event fields, validation, migration, and content redaction**

Validate counts against SparkyApiRequest.MaximumContextTurns and MaximumContextCharacters; bound detail arrays, labels, subjects, and strings; reject controls and non-finite numbers. Extend InitializeSchema with EnsureColumn calls, BindEvent, ReadEvent, ComputeMetadataHash, and the captureContent=false redaction projection. Preserve nullable defaults so old rows materialize without errors.

- [ ] **Step 4: Populate actual context measurements and effective resolver fields**

In SparkyHttpService, compute actual turn count and character count from SparkyConversationContextBudget output, including whether a summary and pending clarification were present. Compare the input context with the bounded context to set the trimming flag. Pass raw parsed route data to the interpretation event; pass effective route and resolver/comparison outcomes from SparkyAnswerResult to retrieval and answer events. Attach bounded turn text and comparison details only to events written under CaptureContent.

- [ ] **Step 5: Run ingestion, storage, retention, and validator regressions**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyTraceStoreTests|FullyQualifiedName~SparkyTraceEventValidationTests|FullyQualifiedName~SparkyTraceIngestionTests|FullyQualifiedName~SparkyProfileManagerTests"

Expected: PASS; old rows still load and retention clears the added content fields.

---

### Task 5: Version sanitized exports and display both routes and comparison diagnostics

**Files:**
- Modify: src/Perpetuum.ItemStatsExporter/SparkyDiagnosticExporter.cs
- Modify: src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/app.js
- Modify: src/Perpetuum.Tests/Unit/SparkyDiagnosticExporterTests.cs
- Modify: src/Perpetuum.Tests/Unit/SparkyAdminListenerTests.cs

**Interfaces:**
- Add diagnostic export SchemaVersion = 1 while preserving existing root and event properties.
- Always export bounded metadata, including actual context counts and parsed/effective route distinction.
- Export context text, query, mentions, subject names, values, and comparison details only when includeContent is true; the trace store already guarantees capture was enabled before those content fields can exist.
- Keep MaximumEvents = 64 and MaximumExportBytes = 512 KiB unless a documented need changes either bound.

- [ ] **Step 1: Add export tests for schema version and both content gates**

Assert metadata-only export includes actual counts and separate parsed/effective route but excludes context strings, metric phrases, subject names, and values. Assert includeContent export contains bounded diagnostic content. Assert existing metadata-only export remains below the size limit.

- [ ] **Step 2: Run the focused exporter tests and confirm expected failures**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyDiagnosticExporterTests

Expected: FAIL because schema version and comparison diagnostics are not yet serialized.

- [ ] **Step 3: Implement versioned export fields with the existing size check**

Add SchemaVersion to SparkyDiagnosticExport. Map new fields using the same includeContent gate used for Question and Answer. Retain the 64-event truncation and 512 KiB UTF-8 size rejection.

- [ ] **Step 4: Render diagnostics in the Admin Panel without unsafe HTML insertion**

Render parsed route, effective route, actual context counts versus configured limits, summary/pending/trim flags, resolver result, and content-gated comparison subjects/metric/values with the existing appendText helper and textContent. Keep the current include-content checkbox controlling the export query.

- [ ] **Step 5: Verify endpoint and browser bundle tests**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~SparkyDiagnosticExporterTests|FullyQualifiedName~SparkyAdminListenerTests"

Expected: PASS; metadata-only download never includes content-only diagnostic fields.

---

### Task 6: Cover comparison sequences in Dev replay and document the operator workflow

**Files:**
- Modify: src/Perpetuum.ItemStatsExporter/SparkyTraceModels.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyReplayService.cs
- Modify: src/Perpetuum.ItemStatsExporter/SparkyTraceStore.cs
- Modify: src/Perpetuum.ItemStatsExporter/wwwroot/sparky-admin/app.js
- Modify: src/Perpetuum.ItemStatsExporter/README.md
- Modify: src/Perpetuum.Tests/Unit/SparkyReplayServiceTests.cs

**Interfaces:**
- Replay output includes effective intent/focus, resolver outcome, and a bounded structured comparison summary when content is available to the replay operator.
- Use the existing ordered SparkyEvaluationTurn sequence format; Active and Draft each start from a new isolated context and never deliver to game or Discord.
- Add these ordered Dev cases: (1) “What are stats for Kain?” → “And Tyrannos?” → “Which one is faster?” with 103.32 kph vs 93.6 kph and Kain ahead by 9.72 kph; (2) “Tell me about Standard medium HCL laser” → “Tell me about Standard Medium Missile Launcher” → “Which has more CPU usage?” with 30 TF vs 40 TF and the missile launcher higher; (3) the same item pair followed by “Compare their stats,” returning only common verified fields; (4) Kain, Tyrannos, and Weasel followed by “Which one is faster?” with clarification because three robots are plausible; (5) Kain and Tyrannos followed by “Which is better?” with a request for a criterion; (6) a Kain question, an unrelated Theseus lore question, then “Which one is faster?” with clarification rather than stale robot reuse; and (7) “Which robot is fastest?” as a global ranking even after a pair comparison.

- [ ] **Step 1: Add deterministic replay sequence tests using a fake answerer**

Verify each turn receives only its own sequence history and that replay results preserve per-turn route/resolution fields. Add a regression asserting contextual Kain/Tyrannos comparison reports the comparison outcome and no global Fastest focus; global ranking remains Fastest.

- [ ] **Step 2: Run replay sequence tests and confirm expected failures**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyReplayServiceTests

Expected: FAIL until replay output carries the new route and comparison summary.

- [ ] **Step 3: Extend replay outputs and keep expectations bounded**

Populate effective route and structured comparison summary from SparkyAnswerResult. Extend SparkyTraceStore replay validation to bound the new fields. Use the existing bounded expected-property format for stable labels such as comparison:contextual, comparison:clarification, and comparison:global; do not add arbitrary scripts or formulas.

- [ ] **Step 4: Document how to create and run the Dev evaluation sequences**

In README, list the seven exact sequences above with their expected route, subject, attribute, verified values, clarification behavior, and forbidden global-ranking or stale-context behavior. State that the Admin Panel replay compares Active and Draft only and does not publish or promote either profile.

- [ ] **Step 5: Run the full project test suite and Release build**

Run: dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64

Run: dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64

Expected: all tests pass and the solution builds. Treat existing NU1900 network warnings as warnings when NuGet vulnerability metadata cannot be fetched; investigate any new compile or test failure.

---

## Implementation Guardrails

- Keep this implementation in the current Main/AI development workflow. Do not deploy to Live or promote a profile.
- Use only the active offline knowledge bundle for item and robot facts. Do not query the live game database.
- Do not serialize caller IDs, platform IDs, account IDs, character IDs, raw prompts, hidden reasoning, credentials, or unrelated logs.
- Ask for clarification when reference resolution, domain, metric, unit, or comparison direction is ambiguous.
- Treat comparisons as factual differences unless the player explicitly asks which is better; never infer a universal ranking from a numeric difference.
- Preserve source-scoped short-lived context and current API authentication, TLS, delivery limits, and response bounds.
