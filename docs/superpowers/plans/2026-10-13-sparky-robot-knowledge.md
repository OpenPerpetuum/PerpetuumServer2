# Sparky Robot Knowledge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add verified, multilingual robot stats, component breakdowns, conditional chassis bonuses, and module-slot compatibility to Sparky’s offline knowledge bundle and answer flow.

**Architecture:** Build robot facts on the Main development machine through `OfflineContentRuntime`, the real `robottemplaterelation` templates, and initialized server domain objects. Store localized robot and module views plus precomputed compatibility relations in the versioned bundle. Resolve each question locally, send only the selected verified facts to LM Studio, and leave game/Discord routing and live database access unchanged.

**Tech Stack:** .NET 8 / C# 12, `Perpetuum.ItemStatsExporter`, existing offline content runtime and robot domain rules, SQL Server local database copy through registered server readers, custom dictionaries, JSON bundle, existing LM Studio API.

**Spec:** `docs/superpowers/specs/2026-10-13-sparky-robot-knowledge-design.md`

## Global Constraints

- Build the bundle from the development machine’s local game data, local database copy, and custom dictionaries; never query the live database.
- Use server object construction and calculations for assembled robot facts; do not reproduce robot stat accumulation by summing database rows.
- Resolve robots only through a real `robottemplaterelation`; do not use the `starter_master` fallback from `GetRelatedTemplateOrDefault`.
- Omit a robot when its relation is missing or invalid, and record a build diagnostic without exposing it as answerable.
- Use only names and labels present in the requested language’s supported custom dictionary. Omit missing labels and names; never send internal definition names, IDs, extension IDs, or slot masks to the model or caller.
- Keep chassis bonuses conditional and character-independent. Describe the exported calculation at extension level 1 and do not infer any player’s extension level.
- Treat module-slot eligibility separately from occupied slots and whole-fit CPU/power-grid capacity. Do not claim a current fitting is viable.
- Keep answers source-grounded, within the existing 2,000-character cap, and on the existing local inference/service path.
- Preserve activation rollback: the updated service must continue reading the previous schema-1 bundle so a failed schema-2 activation can restore the last known-good bundle.
- Do not change game-server routing, Discord behavior, API DTOs, credentials, or live-server configuration for this feature.

## Review Focus

1. A robot with no valid relation must be omitted without receiving the starter template’s facts or aborting unrelated exports. Verify with one missing or invalid relation in the local copy.
2. A robot with stats distributed across parts must report the initialized robot’s combined values and retain the correct part attribution. Compare one known assembled robot and its components in the generated bundle.
3. Repeated bonuses for the same extension and property across multiple parts must be grouped at level 1, while different extensions remain separate and the baseline stays unchanged. Inspect one chassis with multiple bonuses.
4. An untranslated robot, component, module, extension, field, or unit must not leak its internal name or ID into the localized prompt or answer. Inspect a language with incomplete translations.
5. A module that is slot-incompatible or disallowed for the robot must not be listed as compatible; an eligible module must still be described with unique-category limits and without a promise about occupied slots or total CPU/power-grid capacity. Compare representative results to the server rules.

---

## File Map

**Create:**

- `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledge.cs` — bundle records for robot, component, chassis bonus, module, and localized answer facts.
- `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledgeBuilder.cs` — offline enumeration, template construction, initialization, fact projection, and per-definition diagnostics.
- `src/Perpetuum.ItemStatsExporter/SparkyRobotLookup.cs` — localized robot/module resolution and bounded selection of facts for one question.

**Modify:**

- `src/Perpetuum.ItemStatsExporter/Program.cs` — build robot knowledge from the already-initialized offline runtime, include it in the bundle revision/hash and validation summary, and make existing stat projection helpers reusable without duplicating measurement logic.
- `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs` — add the robot/module bundle section, schema-2 validation, cross-reference checks, and robot/module alias ambiguity validation. Continue accepting schema 1 with an empty robot section for rollback.
- `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs` — classify robot questions and identify stats, component, bonus, and compatibility focus while retaining language and entity extraction.
- `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs` — resolve structured robot questions and send only the selected localized fact set to the existing grounded answerer.
- `src/Perpetuum.ItemStatsExporter/README.md` — describe robot export sources, bundle contents, and current limits.
- `docs/codebase/ARCHITECTURE.md` — document robot template assembly and the conditional/static boundary for exported facts.
- `docs/superpowers/sparky-knowledge-refresh.md` — document bundle-schema compatibility and the same staged activation/rollback workflow.

Do not modify `Robot`, `RobotComponent`, `Player`, or game-server request/routing classes. If implementation evidence makes a gameplay-code change necessary, first run the required graph query for each affected existing class and revise the plan before changing it.

## Bundle Interfaces

Keep database identifiers and definition names only as internal bundle lookup keys. The answer projection must contain public translated names and already-formatted stats only.

The bundle gains `Robots` and `RobotModules` sections. A robot record contains its internal definition key and one localized view per supported language. A localized view contains the translated robot name, assembled stats, translated component views, conditional chassis-bonus facts, and component slot counts. A component view contains its translated entity name, its own recognized stats, and the compatible translated module references with eligible slot numbers. The module catalog contains each translated module name and recognized CPU/power-grid stats. The selected answer facts exclude all internal keys and include at most one robot, one requested module, and the relevant component/stat/bonus/slot facts.

Use the following conceptual records; keep property names aligned across serialization, lookup, validation, and the model projection:

```csharp
internal sealed record SparkyRobotKnowledgeExport(
    IReadOnlyList<SparkyRobotRecord> Robots,
    IReadOnlyList<SparkyRobotModuleRecord> RobotModules);

internal sealed record SparkyRobotRecord(
    string DefinitionName,
    IReadOnlyDictionary<int, SparkyLocalizedRobotView> LocalizedViews);

internal sealed record SparkyLocalizedRobotView(
    string? DisplayName,
    IReadOnlyList<AnswerStat> AssembledStats,
    IReadOnlyList<SparkyRobotComponentView> Components,
    IReadOnlyList<SparkyLocalizedChassisBonus> ChassisBonuses);

internal sealed record SparkyRobotModuleRecord(
    string DefinitionName,
    IReadOnlyDictionary<int, SparkyLocalizedRobotModuleView> LocalizedViews);

internal enum SparkyRobotQuestionFocus
{
    Stats,
    Components,
    ChassisBonus,
    ModuleCompatibility,
    General
}

internal sealed record SparkyRobotAnswerFacts(
    string RobotName,
    SparkyRobotQuestionFocus Focus,
    IReadOnlyList<AnswerStat> AssembledStats,
    IReadOnlyList<SparkyRobotComponentFact> Components,
    IReadOnlyList<SparkyLocalizedChassisBonus> ChassisBonuses,
    SparkyRobotModuleFact? Module);

internal sealed record SparkyRobotLookupResult(
    string Status,
    SparkyRobotAnswerFacts? Facts,
    IReadOnlyList<string> Candidates);
```

Add focused component, bonus, module-catalog, and compatibility records alongside these types. The bonus record retains the summed source bonus per extension level and aggregate metadata internally; its localized view contains the translated extension/property/component names and the server-calculated level-1 result. Use localized views for every supported dictionary, with absent translations represented as omitted facts rather than raw-name fallbacks. `SparkyRobotQuestionFocus` is exactly `Stats`, `Components`, `ChassisBonus`, `ModuleCompatibility`, or `General`.

---

### Task 1: Build Robot Facts from Initialized Server Objects

**Files:**

- Create: `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledge.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledgeBuilder.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`

**Interfaces:**

- Produces: `SparkyRobotKnowledgeBuilder.Build(IEntityDefaultReader defaults, IRobotTemplateRelations relations, IExtensionReader extensions, SparkyDictionaryCatalog dictionaries, IReadOnlyDictionary<int, AggregateFieldMetadata> metadata) -> SparkyRobotKnowledgeExport`.
- Consumes: `OfflineContentRuntime`, the server `Entity.Factory.CreateWithRandomEID` path, `Robot.CreateComponents()`, `Robot.Initialize()`, `Robot.BuildPropertiesDictionary()`, each initialized component’s `BuildPropertiesDictionary()`, `RobotComponent.IsValidSlotTo`, `Module.ED.Options.AllowedBots`, `CategoryFlags.IsUniqueCategoryFlags`, and `IExtensionReader`.
- Consumes stat conversion through the existing `ReadStats`, `BuildAnswerStat`, metadata, multiplier, offset, precision, and unit rules in `Program.cs`; move only the needed projection helpers to an internal shared helper or make them internal, rather than forking the conversion.

- [x] **Step 1: Confirm translation keys for extension names before storing bonus facts.**

Use the existing `ExtensionInfo` names from `IExtensionReader.GetExtensions()` and compare the local English custom dictionary to the extension-name key convention used by game content. A bonus is eligible for a localized view only when the extension name, target-field label, and required unit can be resolved from that language’s dictionary. Do not use `ExtensionInfo.name` as a player-facing fallback. Record this verified mapping in a short code comment beside the key-resolution helper.

- [x] **Step 2: Define the internal robot and module records.**

Create the records in the Bundle Interfaces section. Keep `DefinitionName` only for lookup/cross references. Store answer-ready stat labels, values, and units using the existing `AnswerStat` projection. Store localized component names and eligible slot numbers; do not store raw slot masks in an answer view. Store bonus source fields internally only where needed for validation and formula calculation, then project translated extension, property, component names, and the level-1 formatted result.

- [x] **Step 3: Enumerate and construct only translated robot definitions with real relations.**

Enumerate `defaults.GetAll()` definitions in `CategoryFlags.cf_robots`. For each definition, require an English public translation and a non-null `relations.Get(definition.Definition)?.Template` relation. Use `Entity.Factory.CreateWithRandomEID(definition)` to create the requested source robot, verify that it is a `Robot` for the requested definition and references the exact relation, set it unpackaged, call `CreateComponents()`, and then call `Initialize()` before reading any properties. This matches the server path for shared templates whose embedded root definition differs from the requested robot. If the relation is absent, the build is invalid, or construction/initialization throws, skip that definition, write one diagnostic with its internal definition key, and continue. Never call `GetRelatedTemplateOrDefault` and never use the robot factory’s fallback relation.

Read assembled baseline stats from the initialized robot’s `BuildPropertiesDictionary()` and component stats from each initialized `RobotComponent.BuildPropertiesDictionary()`. This keeps component aggregation and equipment-set behavior in `Robot` initialization and property calculation. Build the module catalog by constructing and initializing translated defaults that are `Module` instances, then project their CPU and power-grid values through the same recognized-stat path.

- [x] **Step 4: Export localized parts, bonuses, and static compatibility facts.**

For each supported dictionary, include a translated robot/component/module name only if its entity translation exists. Use dictionary field labels and unit keys through the existing stat projection. For bonuses, group rows by `(extensionId, AggregateField)` across all robot components, retain the contributing translated component names, sum and retain the source bonus per level, and calculate the level-1 property result with the same server sequence as `Player.GetPropertyModifier`: start from `robot.GetPropertyModifier(field)`, call `ItemPropertyModifier.Create(field, summedBonus)`, `NormalizeExtensionBonus()`, then `Modify(ref baseModifier)`. Convert the result with aggregate multiplier, offset, and digits. Keep extensions separate; do not add any bonus to assembled baseline stats.

For module eligibility, use server slot tests for each actual component slot via `IsValidSlotTo`, and enforce `AllowedBots` against the robot definition. The server’s `CheckUniqueModule` delegates to `IsUniqueCategoryFlags` and then checks the robot’s currently installed modules; `CanEquipModule` also checks occupied slots, quantity, and damage. Export the flag-derived one-per-category rule separately, and do not persist either method’s result on a populated template because it describes that template’s installed modules rather than a caller’s fitting. Store eligible component/slot pairs only. Keep CPU and power-grid usage as module facts, not a complete-fit verdict.

- [x] **Step 5: Keep failures local to one robot and report build totals.**

Catch invalid template/build/initialize failures per definition, not around the whole enumeration. Do not turn a partial robot into an answerable record. Return robot/module counts and skipped-definition count to the build command; detailed diagnostics may use internal IDs in the local build log only.

- [x] **Step 6: Review one robot whose assembled properties come from multiple parts.**

Using the local copy and existing configured paths, generate an intermediate bundle and inspect one robot with distributed part stats, one with template modules/set bonuses, and one with chassis bonuses. Confirm the bundle’s assembled values come from the initialized robot, the parts retain individual values, and the baseline does not include character extensions.

---

### Task 2: Version and Validate the Robot Bundle Section

**Files:**

- Modify: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`

**Interfaces:**

- Produces: schema-2 bundles with `Robots` and `RobotModules`; schema-1 deserialization remains valid with both new sections empty.
- Consumes: `SparkyRobotKnowledgeExport`, existing alias construction, manifest hashes, and the existing `--build-knowledge` / `--validate-bundle` CLI.

- [x] **Step 1: Add the new sections without breaking schema-1 rollback bundles.**

Add default-empty `Robots` and `RobotModules` properties so a schema-1 JSON document that lacks them deserializes as empty. Set `CurrentSchemaVersion` to 2 for new output. Make validation accept schema 1 and 2; schema 1 must have empty robot sections, while schema 2 must validate both sections. This keeps the previous AI-machine bundle startable after an activation rollback.

- [x] **Step 2: Validate robot/module uniqueness, localization, and references.**

Reject duplicate robot or module definition keys, null localized records, missing language views, non-finite stat or bonus values, invalid/duplicate slot numbers, and compatibility references to absent modules or components. A localized model-facing record must not contain a raw internal name or numeric definition/extension ID. Allow empty localized facts when their translation is absent.

- [x] **Step 3: Extend alias ambiguity records across items, recipes, robots, and modules.**

Build aliases from the selected localized robot/module names and existing item/recipe names. Continue to ignore empty names and same-definition duplicate aliases. When two public records normalize to the same alias, persist the ambiguity so lookup asks for clarification instead of choosing an arbitrary entity. Validate the generated alias list exactly as the current bundle validator validates item/recipe aliases.

- [x] **Step 4: Include robot facts in content revision and validation output.**

Hash the existing item/recipe export together with the robot/module export so any robot relation, template stat, bonus, or fitting change produces a different `GameContentRevision` and bundle version. Extend `--validate-bundle` and `--build-knowledge` summaries with robot, module, and skipped-robot counts. Keep dictionary/source hashes and rollback behavior intact.

- [x] **Step 5: Verify old and new bundle compatibility through the CLI.**

Run `--validate-bundle` against one existing schema-1 bundle and the generated schema-2 bundle. Expected: schema 1 validates with zero robot records; schema 2 validates and reports the generated robot/module counts. Also reject a temporary schema-2 copy with a dangling module reference before it can be staged.

---

### Task 3: Resolve and Answer Structured Robot Questions

**Files:**

- Create: `src/Perpetuum.ItemStatsExporter/SparkyRobotLookup.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyQuestionInterpreter.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyAnswerRouter.cs`

**Interfaces:**

- Produces: a typed robot focus (`Stats`, `Components`, `ChassisBonus`, `ModuleCompatibility`, or `General`) and `SparkyRobotLookup.Resolve(question, englishSearchQuery, bundle, languageId, contextItem)`, returning `not_found`, `ambiguous`, or one robot plus an optional module and bounded localized answer facts.
- Consumes: the existing `SparkyQuestionInterpretation`, `SparkyAnswerResult`, `SparkyResponseTemplates`, `LocalModelAnswerer.AnswerGroundedAsync`, 2,000-character limit, language context, and source-separated short-lived context.

- [x] **Step 1: Extend interpretation for structured robot topics.**

Add `StructuredRobot` to `SparkyIntent` and the prompt. Add optional `RobotFocus` to `SparkyQuestionInterpretation`, constrained to `SparkyRobotQuestionFocus` (`Stats`, `Components`, `ChassisBonus`, `ModuleCompatibility`, or `General`). Ask the interpreter to emit a focus for stats, component breakdown, chassis bonus, compatibility, or general robot details. Keep `mentionedEntities` capped and preserve names as written. Treat a malformed or missing focus as `Unsupported`; do not let interpretation supply robot facts or IDs.

- [x] **Step 2: Resolve localized robot and module names against both player and English query text.**

Use the original question and `englishSearchQuery`, normalize aliases with the existing word-boundary rules, and consult ambiguity records before selecting anything. Use English aliases as a resolution aid when the requested language lacks a translation, then return the existing localized missing-term response rather than displaying an English or internal fallback. For a short follow-up, reuse the existing context string only when it resolves to one previously selected robot; do not add conversation history or player data.

- [x] **Step 3: Select only the facts needed by the question.**

For `Stats`, include assembled stats. For `Components`, include assembled stats and translated component names with their recognized stats. For `ChassisBonus`, include only the matching translated extension/property bonus facts and their stated level-1 result. For `ModuleCompatibility`, require one selected module and include its CPU/power-grid values, the eligible robot components/slots, and the unique-category limit. Never include the bundle’s internal definition keys, IDs, slot masks, unselected robot records, all compatibility pairs, or lore passages in this prompt.

- [x] **Step 4: Route structured robot requests through the existing grounded answerer.**

Call `LocalModelAnswerer.AnswerGroundedAsync` with the original question, selected response language, and the bounded robot fact object. Do not ask the model to calculate stats, bonus math, compatible slots, or CPU/power-grid totals. Return the selected public robot name in the existing response context field so the current volatile follow-up store can resolve “it” questions without changing request DTOs. Return localized ambiguity/missing-term/unsupported templates before inference when selection is incomplete.

- [ ] **Step 5: Check the five Review Focus cases through the local API.** (Unit-level coverage is complete; API smoke is pending transfer and activation on the separate AI machine.)

Ask one assembled-stat, component-breakdown, bonus, and module-compatibility question in English and in each dictionary language that has the needed labels. Include an unknown robot, an ambiguous alias, a missing component translation, an incompatible module, and a short robot follow-up. Inspect diagnostics to confirm the prompt has only the selected public facts. Confirm output stays within 2,000 characters and does not claim the caller’s current fit has free slots or enough total CPU/power grid.

---

### Task 4: Document Refresh and Local Acceptance

**Files:**

- Modify: `src/Perpetuum.ItemStatsExporter/README.md`
- Modify: `docs/codebase/ARCHITECTURE.md`
- Modify: `docs/superpowers/sparky-knowledge-refresh.md`

- [x] **Step 1: Document the generated fact boundary and exclusions.**

Explain that robot stats come from built/initialized templates, chassis bonuses are conditional level-1 calculations, fitting facts cover static eligibility, and current character extensions/loadouts are unavailable. State that missing translations and missing relations omit facts; no live database, personal data, or model-generated calculations are used.

- [x] **Step 2: Document build, transfer, schema validation, and rollback.**

Update the Main-machine bundle instructions and AI-machine refresh steps. The AI machine receives the schema-2 bundle and the matching published `Perpetuum.ItemStatsExporter` files in both the command-line directory and service directory. The new service accepts schema 1 for rollback and schema 2 for active robot data. The existing activation script remains the only activation path and must verify the expected bundle version before success.

- [ ] **Step 3: Complete local-only activation and smoke review.** (Bundle and self-contained executable are built and locally validated; transfer, service activation, and model-backed API prompts require the separate AI machine.)

Build using the established Main-machine paths, validate the schema-2 bundle, copy and hash-check it to AI, publish/copy the matching exporter binaries, and activate through `Refresh-SparkyKnowledge.ps1`. Confirm health reports the new bundle version, then use the Main machine’s debug server and existing reverse tunnel to exercise robot stats, component, bonus, and compatibility prompts. Do not restart or modify VDI/live server as part of acceptance.

- [x] **Step 4: Review final scope and regression evidence.** Independent review findings were fixed and regression-tested: localized current-fit boundary, dictionary-only English stat labels, overlapping robot mentions, and non-English follow-up resolution. The schema-1 rollback bundle validates, the full Release unit suite passes 142/142, and bundle revision sensitivity is covered. Main-machine API acceptance follows AI-machine activation.

Confirm no game-server route/API files changed, schema-1 rollback still validates, existing item/recipe answers remain available, structured robot facts stay multilingual, and the new bundle revision changes when robot content changes. Report any robot definitions skipped for missing/invalid templates.

---

## Implementation Notes and Risks

- `OfflineContentRuntime` already registers `EntitiesModule` and `RobotTemplatesModule`; the builder should resolve the relation service from that runtime rather than creating another reader or SQL implementation.
- `RobotTemplate.Build()` builds its parts and template modules; `Robot.Initialize()` initializes parts and calculates equipment-set bonuses. Read the assembled robot and each component after this initialization.
- `RobotComponent.CanEquipModule` mixes static rules with current slot/module instance state. The exported fact must describe static slot and `AllowedBots` eligibility and the one-per-unique-category rule without reusing the default template’s occupied slots as a claim about a player’s fit.
- Extension names may not have a per-extension translation entry in every dictionary. Confirm the real key mapping during Task 1; omit the affected bonus if no exact public translation exists.
- A bundle schema bump can break rollback if schema 1 is rejected. Task 2 preserves a schema-1 reader path before any activation is attempted.
- The local database, custom dictionaries, and actual translated robot/module coverage determine final bundle size and counts. Keep compatibility pairs normalized as references rather than duplicating full module facts per slot.
- No new gameplay code is planned. If the offline initialization fails because a required runtime-only service is missing, record the failing robot and dependency; do not change shared robot/runtime classes without a revised impact review.
