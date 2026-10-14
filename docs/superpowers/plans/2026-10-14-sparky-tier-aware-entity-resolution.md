# Sparky Tier-Aware Entity Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Each task should be completed and reviewed before moving to the next.

**Goal:** Resolve tier shorthand, shortened item/robot names, and calibration-template aliases to one verified entity in the offline Sparky bundle.

**Architecture:** Main’s offline exporter reads the exact nullable tier pair from its local database copy, combines it with a reviewed alias-rules file, and writes the data into schema 4. The AI service uses a shared deterministic matcher before the existing item/recipe or robot answer path; the local model only phrases facts after a unique entity is selected.

**Tech Stack:** .NET 8, C# 12, SQL Server through the existing `Db.Query()` API, `System.Text.Json`, xUnit v3, existing Sparky offline knowledge bundle and Admin Panel refresh flow.

**Spec:** `docs/superpowers/specs/2026-10-14-sparky-tier-aware-entity-resolution-design.md`

## Global Constraints

- Build entity facts from Main’s local database copy; the AI service must not connect to a database.
- Preserve raw nullable `(tiertype, tierlevel)` values; `EntityDefaultReader` currently maps SQL `NULL` to zero.
- Use only reviewed tier and calibration-template aliases, localized custom-dictionary display names, and the canonical English display names already in the bundle.
- Resolve an entity deterministically before sending verified facts to LM Studio; the model must not select candidates or invent entity facts.
- Increment the bundle schema from 3 to 4; schema 1–3 remain valid for rollback and keep name-only behavior.
- Keep robot class and role aliases, new mechanics, dynamic season data, account/character/loadout data, and game/Discord routing changes out of scope.
- Routine alias vocabulary changes must be possible by editing the reviewed rules file and refreshing the bundle without redeploying the AI executable.
- Update the schema 4 executable on AI through the existing deployment workflow; activate the new bundle in Dev and leave Live unchanged.
- Do not commit changes unless the user explicitly asks.

## Review Focus

- Raw `(NULL, NULL)`, `(2, NULL)`, `(2, 0)`, and `(2, 1)` must remain distinct; Task 2 and Task 3 pin this with reader and validator tests.
- Tier punctuation (`T2+`, `T3-`) and multiword forms (`T4 prototype`, `Mark 2`) must parse without consuming unrelated base-name words; Task 4 tests both positive and negative forms.
- Same-name candidates at different tiers must not collapse into an arbitrary selection; Task 4 tests the ambiguous result and Task 5 tests the user-facing clarification path.
- Shortened names must match only when one candidate remains after tier filtering; Task 5 tests unique and colliding names.
- Missing localized names must not expose internal definition names; Task 5 tests candidate formatting and language fallback behavior.

## File Structure

- Create `tools/sparky-entity-resolution-rules.json` as the reviewed, operator-editable source for tier descriptors and calibration-template category aliases.
- Create `src/Perpetuum.ItemStatsExporter/SparkyEntityResolutionRules.cs` to deserialize and validate that rules file.
- Create `src/Perpetuum.ItemStatsExporter/SparkyEntityTierMetadataReader.cs` to read raw nullable tier pairs from the configured local database.
- Create `src/Perpetuum.ItemStatsExporter/SparkyTierAwareEntityResolver.cs` to parse descriptors and resolve candidates deterministically.
- Modify `src/Perpetuum.ItemStatsExporter/Program.cs` to load tier metadata/rules, populate export records, include rules provenance, and preserve legacy flat-export loading as schema 3.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs` and `SparkyRobotKnowledge.cs` for schema 4 metadata and validation.
- Modify `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledgeBuilder.cs`, `ItemStatsLookup.cs`, and `SparkyRobotLookup.cs` to populate/use tier-aware metadata without changing fact calculations.
- Add focused tests under `src/Perpetuum.Tests/Unit/`; extend the existing bundle-validator and robot-lookup suites where that keeps fixtures clear.
- Update `docs/superpowers/sparky-knowledge-refresh.md` and `src/Perpetuum.ItemStatsExporter/README.md` with schema 4 build, publish, activation, and rollback notes.

---

### Task 1: Add the reviewed entity-resolution rules catalog

**Files:**
- Create: `tools/sparky-entity-resolution-rules.json`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyEntityResolutionRules.cs`
- Create: `src/Perpetuum.Tests/Unit/SparkyEntityResolutionRulesTests.cs`

**Interfaces:**
- Produce `SparkyEntityResolutionRules.Load(string repositoryRoot)` returning a validated rules object.
- Define `SparkyEntityKind` with only `Item` and `Robot` values, plus `SparkyEntityTierRule` with `EntityKind`, `TierType`, nullable `TierLevel`, `CanonicalLabel`, and `AliasesByLanguage`.
- Each tier rule contains entity kind, integer `TierType`, nullable integer `TierLevel`, canonical label, and aliases keyed by game language ID.
- Calibration-template rules contain the exact `_cprg` suffix and aliases keyed by game language ID.
- Use language ID `0` for the initial English aliases in the spec; lookup later checks the selected language plus language `0`.

- [ ] **Step 1: Write failing rules tests**

Add tests asserting that the shipped file contains the reviewed item/robot mappings, including item `(2, 4) => T4P`, item `(3, 2) => T2+`, robot `(2, null) => P`, and calibration aliases `CT` and `blueprint`. Add invalid-catalog tests for duplicate normalized aliases mapping to different pairs in the same kind, invalid entity kinds, malformed suffixes, and an unsupported rules version.

```csharp
[Fact]
public void LoadsPrototypeRobotRuleWithNullTierLevel()
{
    SparkyEntityResolutionRules rules = SparkyEntityResolutionRules.Load(TestRepositoryRoot);

    Assert.Contains(rules.TierRules, rule =>
        rule.EntityKind == SparkyEntityKind.Robot && rule.TierType == 2 && rule.TierLevel is null &&
        rule.CanonicalLabel == "P");
}
```

- [ ] **Step 2: Run the targeted test and confirm it fails**

Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyEntityResolutionRulesTests`

Expected: FAIL because the rules model/file is not present.

- [ ] **Step 3: Add the JSON catalog and loader**

Store the exact item/robot mappings and aliases from the approved spec in this shape:

```json
{
  "version": 1,
  "tierRules": [
    {
      "entityKind": "item",
      "tierType": 1,
      "tierLevel": 1,
      "canonicalLabel": "T1",
      "aliasesByLanguage": { "0": ["T1", "tier 1", "standard"] }
    },
    {
      "entityKind": "robot",
      "tierType": 2,
      "tierLevel": null,
      "canonicalLabel": "P",
      "aliasesByLanguage": { "0": ["P", "prototype"] }
    }
  ],
  "calibrationTemplate": {
    "suffix": "_cprg",
    "aliasesByLanguage": { "0": ["calibration template", "CT", "blueprint"] }
  }
}
```

Include every mapping and alias in the approved spec, not just the two example entries above. Load the file through `SparkyKnowledgeSourceCatalog.ResolveRepositoryFile` so the configured path cannot escape the repository or traverse a reparse point. Normalize ordinary words using the same Unicode letter/number semantics as Sparky’s name matching. Normalize tier aliases with a tier-specific rule that preserves/canonicalizes `+` and `-` (`T2+` becomes the same key as `T2 plus`, while remaining distinct from `T2`; likewise `T3-` remains distinct from `T3`). Reject duplicate normalized aliases only when they point to different tier pairs within the same entity kind. Keep aliases for distinct entity kinds separate.

- [ ] **Step 4: Run the targeted rules tests**

Run the command from Step 2. Expected: PASS for the shipped catalog and each invalid-catalog rejection.

### Task 2: Preserve and export raw nullable tier metadata

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyEntityTierMetadataReader.cs`
- Create: `src/Perpetuum.Tests/Unit/SparkyEntityTierMetadataReaderTests.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledge.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyRobotKnowledgeBuilder.cs`

**Interfaces:**
- `SparkyEntityTierMetadataReader.Load()` returns `IReadOnlyDictionary<int, SparkyEntityTierMetadata>` keyed by the internal entity definition ID used only during offline bundle construction.
- `SparkyEntityTierMetadata` has required nullable `TierType` and `TierLevel` JSON properties. `null` values are valid; a missing `TierMetadata` object is reserved for schema 1–3 records.
- Add optional `TierMetadata` properties to `WeaponItem`, `ProductionRecipe`, `SparkyRobotRecord`, and `SparkyRobotModuleRecord`; schema 4 requires them. Do not add definition IDs to answer facts.

- [ ] **Step 1: Write failing reader tests using the existing `FakeDb` fixture**

Register result rows for `(NULL, NULL)`, `(2, NULL)`, `(2, 0)`, and `(2, 1)`. Assert the reader preserves each pair and asserts the command selects only `definition`, `tiertype`, and `tierlevel` from enabled `dbo.entitydefaults`. Follow the static database fixture pattern in `DbQueryTests` and `PerpetuumStaticsCollection`.

- [ ] **Step 2: Run the targeted reader test and confirm it fails**

Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyEntityTierMetadataReaderTests`

Expected: FAIL because the reader and metadata record do not exist.

- [ ] **Step 3: Implement the bounded raw metadata reader**

Use the verified query `SELECT definition, tiertype, tierlevel FROM dbo.entitydefaults WHERE enabled = 1`, executed through `Db.Query()`. Read nullable columns with `GetValue<int?>`; reject duplicate definition keys. Do not change `EntityDefaultReader` or use its normalized `TierInfo` for exported metadata.

- [ ] **Step 4: Populate export records from the metadata map**

Load the map once inside `BuildKnowledgeBundle`. Pass it into `BuildExport`, recipe construction, and `SparkyRobotKnowledgeBuilder.Build`; copy the raw pair onto every generated record by its existing internal definition key. Keep item/robot stat calculation and recipe arithmetic on their current server-domain paths.

- [ ] **Step 5: Run the targeted reader and exporter-adjacent tests**

Run the command from Step 2 and the existing `SparkyRobotLookupTests` suite. Expected: PASS; old tests should remain unchanged until schema validation begins requiring tier metadata for schema 4.

### Task 3: Introduce schema 4 and maintain old-bundle compatibility

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyKnowledgeBundle.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`
- Modify: `src/Perpetuum.Tests/Unit/SparkyKnowledgeBundleValidatorTests.cs`

**Interfaces:**
- Set `SparkyKnowledgeBundle.CurrentSchemaVersion` to `4` and add a schema 4 entity-resolution rules property.
- Keep schema 1, 2, and 3 validation branches explicit; schema 4 requires valid rules and tier metadata on every exported record.
- Legacy flat exports loaded by `ReadKnowledgeBundleOrLegacy` must be wrapped as schema 3, not schema 4.

- [ ] **Step 1: Add failing schema tests**

Cover valid schema 4 round-trip, missing rules, missing `TierMetadata` on an individual item/recipe/robot/module, and malformed alias rules. Verify that an unmapped but numeric raw pair is retained and cannot match a shorthand. Preserve the existing schema 1–3 rollback tests. Add a test that legacy flat export loading does not claim schema 4.

- [ ] **Step 2: Run validator tests and confirm the new cases fail**

Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyKnowledgeBundleValidatorTests`

Expected: FAIL on missing schema 4 rules/validation behavior while the existing rollback tests remain green.

- [ ] **Step 3: Update bundle versioning and validation**

Accept versions 1–4. Require nonempty validated rules and a non-null `TierMetadata` object for each schema 4 record. Mark its nullable `TierType` and `TierLevel` JSON properties as required so explicit null remains distinguishable from an omitted property. Validate recognized pairs against their entity-kind rule set; retain other numeric pairs without a tier label so they cannot match a shorthand. Preserve schema-specific robot module laser checks.

- [ ] **Step 4: Include rules provenance and metadata in bundle revisions**

Load the rules file in `BuildKnowledgeBundle`, add its SHA-256 to `Manifest.SourceHashes` under a stable non-path key, and ensure the bundle version seed includes that source hash. Keep absolute repository paths out of the serialized manifest. Tier metadata already participates in the serialized item/recipe/robot content revision once placed on those records.

- [ ] **Step 5: Preserve legacy export and rollback behavior**

Set the schema version of legacy flat exports to `3`. Validate schema 1–3 without tier rules and leave them on current name-only matching. Ensure a schema 4 document with missing/partial fields fails with a clear `InvalidDataException` before activation.

- [ ] **Step 6: Run validator tests**

Run the command from Step 2. Expected: PASS for schema 4 and the existing schema 1–3 rollback/legacy cases.

### Task 4: Build the deterministic tier and alias matcher

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyTierAwareEntityResolver.cs`
- Create: `src/Perpetuum.Tests/Unit/SparkyTierAwareEntityResolverTests.cs`

**Interfaces:**
- `SparkyEntityCandidate` contains an internal definition name, entity kind, `SparkyEntityTierMetadata`, public aliases for the selected and English languages, and a calibration-template flag.
- `SparkyEntityResolutionResult` contains `Status` (`found`, `not_found`, `ambiguous`, or `conflicting_tiers`), `DefinitionName` when found, and internal candidate definition names for the caller to translate safely.
- `SparkyTierAwareEntityResolver.Resolve(string question, string? englishQuery, SparkyEntityKind entityKind, IReadOnlyList<SparkyEntityCandidate> candidates, SparkyEntityResolutionRules rules, int languageId)` parses descriptors, strips category terms, filters by exact nullable pairs and then applies the existing bounded phrase/token matcher.
- Test fixtures provide `CreateRules()` and `Candidate(string definitionName, SparkyEntityKind kind, int? tierType, int? tierLevel, params string[] publicAliases)` helpers.

- [ ] **Step 1: Write failing matcher tests**

Cover every exact descriptor mapping in the approved spec, English phrase variants (`T4 prototype`, `Mark 2`), longest-alias precedence (`T1 prototype` is one item descriptor, not a conflict between `T1` and `prototype`), punctuation/case normalization, distinct parsing of `T2` versus `T2+` and `T3` versus `T3-`, multiple conflicting descriptors, exact null comparisons, exact alias matches, shortened token matches (for example “medium plate” against “Standard medium armor plate”), ties between shortened candidates, name-only ambiguity, tier-only non-selection, and no-match behavior.

```csharp
[Theory]
[InlineData("T2+ Scorcher", "def_scorcher_t2plus", 3, 2)]
[InlineData("T4 prototype sensor amplifier", "def_sensor_amplifier_t4p", 2, 4)]
public void ResolvesItemByTierAndRemainingName(string query, string expectedDefinitionName, int tierType, int tierLevel)
{
    var candidates = new[]
    {
        Candidate(expectedDefinitionName, SparkyEntityKind.Item, tierType, tierLevel, "Scorcher", "Sensor amplifier"),
        Candidate("def_other_tier", SparkyEntityKind.Item, 1, 2, "Scorcher", "Sensor amplifier")
    };
    SparkyEntityResolutionResult result = SparkyTierAwareEntityResolver.Resolve(
        query, null, SparkyEntityKind.Item, candidates, CreateRules(), languageId: 0);

    Assert.Equal("found", result.Status);
    Assert.Equal(expectedDefinitionName, result.DefinitionName);
}
```

- [ ] **Step 2: Run the targeted matcher tests and confirm they fail**

Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyTierAwareEntityResolverTests`

Expected: FAIL because the resolver is not implemented.

- [ ] **Step 3: Implement descriptor parsing and exact candidate filtering**

Match tier aliases as standalone normalized terms, prefer the longest alias at a given token span (so `T1 prototype` is not split into two descriptors), remove descriptors and calibration-category words from the base-name query, and search the original question plus English interpreter query. Search aliases from the selected language plus language `0`. Use a tier-specific normalizer that canonicalizes plus/minus suffixes without collapsing `T2+` into `T2` or `T3-` into `T3`. If no tier term is present, do not filter on null metadata. Require at least one remaining public-name token; a tier descriptor alone must never select an entity. Prefer existing exact phrase matches. For shortened names, remove tier/category and known intent stop words, then match the remaining whole tokens against each public alias. Keep all top candidates tied at the best token coverage; the caller returns a unique result only when one candidate remains and otherwise asks for clarification. Do not use edit distance, model-generated synonyms, or internal definition-name tokens.

- [ ] **Step 4: Implement exact `_cprg` qualification**

Set the candidate’s calibration-template flag only when its canonical definition/dictionary key ends with `_cprg`, case-insensitively. A query containing `CT`, `calibration template`, or `blueprint` filters to those flagged candidates; a partial suffix or display-name substring does not qualify. Require the remaining base name to identify a candidate.

- [ ] **Step 5: Run the targeted matcher tests**

Run the command from Step 2. Expected: PASS, including conflicting tier, null-pair, and exact suffix cases.

### Task 5: Integrate matching into item, recipe, robot, and module lookups

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/ItemStatsLookup.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyRobotLookup.cs`
- Create: `src/Perpetuum.Tests/Unit/SparkyItemTierLookupTests.cs`
- Modify: `src/Perpetuum.Tests/Unit/SparkyRobotLookupTests.cs`

**Interfaces:**
- `ItemStatsLookup.Resolve` receives the schema 4 bundle and uses the shared resolver to select the merged item/recipe record before returning stats, direct recipe, or full materials.
- `SparkyRobotLookup.Resolve`, `ContainsRobotMention`, and `FindMentionedRobots` use the same resolver for robot and module candidates, preserving current question focus, compatibility facts, and context behavior.
- Candidate display names are obtained through existing localized-view helpers; internal definition keys stay inside the lookup layer and never become player-facing candidate text.

- [ ] **Step 1: Add item lookup tests**

Verify `T1 medium plate`, `T2+ Scorcher`, `T4 prototype sensor amplifier`, and CT/blueprint lookup select the intended item/recipe facts. Verify a shared base name across tiers returns ambiguous, and a generic shortened name returns ambiguous when more than one record matches.

- [ ] **Step 2: Run the item lookup tests and confirm they fail**

Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyItemTierLookupTests`

Expected: FAIL because the current lookup does not parse tier/category descriptors.

- [ ] **Step 3: Integrate tier resolution into item and recipe catalog merging**

Carry the raw tier pair and `_cprg` classification while merging `WeaponItem` and `ProductionRecipe` records by their existing definition key. Apply tier filtering before longest-alias selection. Preserve direct/full recipe facts, existing follow-up context, and the current `not_found`/`ambiguous` result contract.

- [ ] **Step 4: Add and run robot/module cases**

Extend `SparkyRobotLookupTests` for `Mk2 Arbalest`, `Mk3 Weasel`, unique unqualified `Weasel`, prototype `P`, and a tiered module in a compatibility question. Add a name collision across robot tiers and assert `ambiguous`. Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SparkyRobotLookupTests`.

- [ ] **Step 5: Route results through existing localized clarification behavior**

For unique matches, pass only the selected record’s existing verified facts to the model. For ambiguous results, use existing localized candidate formatting; suppress any candidate without a safe public display name. For missing results, use the existing localized missing-term response. Do not send candidate lists or internal tier IDs to LM Studio. Preserve name-only matching for schema 1–3 bundles.

- [ ] **Step 6: Run all Sparky lookup and bundle-validator tests**

Run: `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~Sparky`

Expected: PASS across lookup, routing, and bundle validation suites.

### Task 6: Update operations guidance and validate the Dev rollout

**Files:**
- Modify: `docs/superpowers/sparky-knowledge-refresh.md`
- Modify: `src/Perpetuum.ItemStatsExporter/README.md`

- [ ] **Step 1: Document the rules file and schema 4**

Explain where to edit aliases, how English/shared aliases and localized aliases are selected, how the rules hash appears in bundle provenance, and that alias-only changes use a bundle refresh without an executable redeploy. Update schema compatibility language for schemas 1–4 and note the one-time AI executable update for schema 4.

- [ ] **Step 2: Run the complete unit tier and build**

Run:

```powershell
dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64
dotnet build src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj -c Release -p:Platform=x64
```

Expected: all unit tests pass and the exporter builds. Report any pre-existing package-feed warnings separately from errors.

- [ ] **Step 3: Build and validate a real local bundle on Main**

Use the existing approved Main paths and command shape:

```powershell
dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --build-knowledge `
  'G:\PerpetuumServer\data' `
  'E:\MyStuff\Projects\OPDB\Patches\Live_36\Server\data\customDictionary' `
  'E:\MyStuff\Projects\PerpetuumServer2' `
  'E:\Perpetuum-lore' `
  'E:\MyStuff\Projects\PerpetuumServer2\sparky-knowledge.bundle.json'

dotnet run --project src/Perpetuum.ItemStatsExporter -c Release -- --validate-bundle `
  'E:\MyStuff\Projects\PerpetuumServer2\sparky-knowledge.bundle.json'
```

Expected: schema 4 validates; export counts and bundle version are reported; no absolute path is embedded for the rules file.

- [ ] **Step 4: Publish/update AI’s command-line and service installations once**

Publish self-contained `win-x64` output using the established command shape:

```powershell
$publishPath = Join-Path 'E:\MyStuff\Projects\PerpetuumServer2' ("publish\SparkyItemStatsExporter-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
dotnet publish src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj `
  -c Release -r win-x64 --self-contained true -p:Platform=x64 `
  -o $publishPath
```

Transfer it to both `D:\Sparky\SparkyItemStatsExporter` and `C:\Program Files\PerpetuumSparky` using the established backup/stop/copy/start procedure. Verify the service reports healthy with the Dev profile and the new executable validates the schema 4 bundle.

- [ ] **Step 5: Stage and activate only the Dev bundle**

Transfer the validated bundle and matching SHA-256 using the existing approved file-transfer method. Stage it with `Refresh-SparkyKnowledge.ps1`, activate it in the Admin Panel’s Dev profile, and confirm `/health` reports the expected bundle version. Leave Live disabled/unchanged.

- [ ] **Step 6: Run manual acceptance through the local Main server**

Ask stats and recipe questions for `T1 medium plate`, `T2+ Scorcher`, and `T4 prototype sensor amplifier`; ask a full recipe for a `_cprg` template; ask robot questions for `Mk2 Arbalest`, `Mk3 Weasel`, `Weasel`, and a prototype robot; test an ambiguous short name and a nonexistent tier. Confirm localized clarification, facts unchanged, and no IDs/internal names in answers or LM Studio evidence. Confirm Main remains in Debug and Live remains untouched.

## Self-Review

- Spec coverage: raw nullability and each tier mapping are covered by Tasks 1–3; exact `_cprg` handling and aliases by Tasks 1 and 4–5; deterministic ambiguity and localized candidates by Tasks 4–5; schema 4/legacy compatibility by Task 3; Dev-only staging and security boundaries by Task 6.
- Review-focus tests are assigned: raw nulls to Task 2/3; punctuation and tier forms to Task 4; tier collisions to Task 4/5; shortened-name uniqueness to Task 5; missing localized names/internal-name suppression to Task 5.
- Legacy path: flat JSON exports must remain schema 3, because schema 4 requires metadata that legacy files cannot carry.
- Database query: the raw read uses the documented `dbo.entitydefaults` columns and filters to `enabled = 1`, matching `EntityDefaultReader.LoadAll`; no arbitrary SQL or live database access is introduced.
- Interface and graph review: before modifying central files (`Program.cs`, `SparkyKnowledgeBundle.cs`, `ItemStatsLookup.cs`, and `SparkyRobotLookup.cs`), rerun `tools/query-graph.ps1` with the exact unique symbols and inspect direct dependents. The graph tool currently returns ambiguous matches for some file/class names, so disambiguate rather than treating that output as a null result.
- No commit step is included because repository guidance says not to commit unless explicitly requested.
