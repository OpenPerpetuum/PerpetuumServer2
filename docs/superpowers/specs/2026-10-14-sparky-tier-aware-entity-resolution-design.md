# Sparky Tier-Aware Entity Resolution Design

**Status:** Draft for review
**Date:** 2026-10-14

## Goal

Let players find existing item, recipe, robot, and robot-module facts using the tier shorthand and shortened names they naturally use, for example “T1 medium plate,” “T2+ Scorcher,” “Mk2 Arbalest,” “Weasel,” or “T4 prototype sensor amplifier.” Recognize calibration templates by the terms “calibration template,” “CT,” and “blueprint.”

Resolution must select an entity from the validated offline knowledge bundle before facts are formatted or sent to the model. The model does not infer tier identity, select among candidates, or invent missing aliases.

## Scope

This design covers deterministic matching for the entity catalogs already exported to Sparky:

- item-stat records;
- recipe product records;
- assembled robot records;
- robot module records used in compatibility questions.

It does not add new item or robot facts, query the AI machine’s database, read live game data, change game/Discord routing, or expose account, character, or loadout data. Robot class and role aliases (light, assault, mech, heavy mech, destroyer, combat, scout, ewar, hauler, miner, harvester, drone boat/carrier, logistic/logi) are deferred pending a separate mapping review.

## Tier source and representation

The local offline database’s `dbo.entitydefaults.tiertype` and `tierlevel` are the source of tier identity. The fields are nullable, and the pair must be preserved exactly:

- both `NULL` means tierless;
- one `NULL` remains distinct from zero;
- a type/level pair that has no reviewed mapping receives no player-facing tier label and cannot match a tier shorthand.

The current server `EntityDefaultReader` maps each SQL `NULL` to zero while creating `TierInfo`. The offline exporter must therefore retain the raw nullable pair from the same local database copy, keyed to the already-loaded entity definition, rather than infer nullability from `TierInfo`. This raw read supplies classification metadata only. Existing server-domain construction and calculations remain authoritative for item and robot facts; the AI service never queries this database.

Tier matching uses the following reviewed mappings. Unlisted pairs are not guessed.

| Entity kind | `tiertype` | `tierlevel` | Canonical player term | Accepted English forms |
|---|---:|---:|---|---|
| Item | 1 | 0 | T0 | T0, tier 0, damaged, flawed, trash |
| Item | 1 | 1 | T1 | T1, tier 1, standard |
| Item | 1 | 2 | T2 | T2, tier 2 |
| Item | 1 | 3 | T3 | T3, tier 3 |
| Item | 1 | 4 | T4 | T4, tier 4 |
| Item | 1 | 5 | T5 | T5, tier 5, season reward, set item |
| Item | 2 | 1 | T1P | T1P, T1 prototype, tier 1 prototype |
| Item | 2 | 2 | T2P | T2P, T2 prototype, tier 2 prototype |
| Item | 2 | 3 | T3P | T3P, T3 prototype, tier 3 prototype |
| Item | 2 | 4 | T4P | T4P, T4 prototype, tier 4 prototype |
| Item | 3 | 0 | S | S, Syntec, noob |
| Item | 3 | 2 | T2+ | T2+, tier 2 plus |
| Item | 3 | 3 | T3- | T3-, tier 3 minus, Niani item |
| Item | 3 | 4 | T4+ | T4+, tier 4 plus |
| Robot | 1 | 1 | Mk1 | Mk1, Mark 1 |
| Robot | 1 | 2 | Mk2 | Mk2, Mark 2 |
| Robot | 1 | 3 | Mk3 | Mk3, Mark 3, season robot, season reward |
| Robot | 2 | `NULL` | P | P, prototype |

Tier terms are parsed as distinct tokens before matching the remaining name. Punctuation and case are normalized, so `T2+`, `T2 plus`, `T3-`, `Mk2`, and `Mark 2` can be recognized. The aliases above are an initial English vocabulary; localized entity names continue to come only from the caller’s supported custom dictionary. No localized tier wording is invented when the dictionary has no authoritative translation for it.

Keep the tier and calibration-template alias vocabulary in a reviewed data file such as `tools/sparky-entity-resolution-rules.json`, and copy the validated rules into the schema 4 bundle. The offline builder validates that each mapped raw pair is well-formed and that one normalized alias does not point to multiple tier pairs within the same entity kind. This lets an operator adjust accepted wording, rebuild, and activate a bundle without redeploying the AI-machine executable. The resolver implementation still requires an executable update for new parsing behavior or new entity kinds. Search the selected-language aliases and shared English aliases; do not synthesize local-language synonyms.

The same spoken term may have different meanings by entity kind. For example, `T1P` denotes item type 2/level 1, while robot `P` denotes type 2/null. Resolve against the item or robot catalog selected by the current structured route. If the question or existing route leaves the entity kind ambiguous, ask for clarification rather than choosing by model preference.

## Calibration template aliases

An exported item or recipe product is a calibration-template candidate only when its canonical database definition name (the dictionary key used to translate it) ends with `_cprg`, case-insensitively. For such a candidate, “calibration template,” “CT,” and “blueprint” act as category aliases. They do not replace the entity’s actual translated name: the request must still identify a base item or leave exactly one candidate after filtering.

Do not classify an entity by a partial internal name, a display-name substring, or an LLM claim. Do not expose the `_cprg` key or untranslated definition name in a player response.

## Matching behavior

1. Normalize the original question and the interpreter’s English search query using the existing name-normalization rules.
2. Extract at most one recognized tier descriptor for the relevant entity kind. If multiple incompatible tier descriptors are present, ask the player to clarify.
3. Remove the descriptor and calibration-template category words from the text used for base-name matching.
4. Match the remaining base-name tokens against supported localized display names and the canonical English display name already present in the bundle. Keep matching bounded to these public names; do not use database IDs, arbitrary definition names, filesystem content, or model-generated synonyms.
5. If a tier descriptor was present, filter candidates by the exact raw nullable `(tiertype, tierlevel)` mapping. A missing descriptor does not imply a tierless filter; `(NULL, NULL)` simply has no mapped tier label and remains a normal candidate for an unqualified name query.
6. Select a candidate only when the combined tier and name constraints identify exactly one entity. If no candidate matches, use the existing localized missing-term response. If multiple candidates remain, return the existing localized ambiguity response with translated candidate names. Do not send candidate sets to the model for it to choose.
7. Once selected, continue through the existing item-stat/recipe or robot-answer path, preserving verified values, translations, and current follow-up context behavior.

Shortened names are supported only when deterministic token matching after tier filtering yields one unique candidate. “Medium plate” may match a unique translated “medium armor plate”; a generic “plate” that matches several entities must request clarification. A tier prefix alone is never enough to choose an entity.

The tier descriptor disambiguates the entity; it does not change the requested answer intent. “T1 medium plate stats” continues to request stats, while “full recipe for T1 medium plate” continues to request recursive materials. A follow-up such as “and the full recipe?” may use the existing short-lived context only if it identifies the previously selected entity unambiguously.

## Bundle and refresh behavior

The exporter will include raw nullable tier metadata on the relevant item, recipe-product, robot, and robot-module records, plus the validated tier/category alias rules from the reviewed rules file. The exported values are public game-content classification only; do not include numeric definition IDs in answers or model evidence. Validation must reject malformed tier metadata, duplicate catalog identities, invalid entity-kind/tier mappings, and alias collisions that make one normalized alias identify different tier pairs within the same entity kind. The rules-file hash must be included in bundle provenance so the activated bundle identifies the exact alias vocabulary used.

Increment the knowledge-bundle schema from 3 to 4. The schema 4 exporter and AI-machine CLI/service validator must be published and transferred together. Schema 1–3 bundles remain acceptable for rollback under their existing validation rules, but cannot provide tier-aware matching; the runtime should fall back to current name-only behavior for them. A schema 4 bundle that contains partial or invalid tier metadata must fail validation rather than silently degrade.

Build and validate the bundle on Main from its local database copy, server source, custom dictionaries, and reviewed rules file. Publish the schema 4 exporter/service executable and update both AI-machine installations using the existing operations workflow; restart the Sparky service once to load the new executable. Then stage and activate the new bundle in the Dev profile through the Admin Panel. Bundle activation itself does not require another game-server or AI-service restart. Live remains unchanged.

## Security and privacy

- The AI machine receives only the validated offline bundle and request-specific public facts.
- No account ID, character ID, inventory, fitting, private player data, or live database connection is introduced.
- Internal definition names and raw tier pairs are used only for deterministic selection/validation and are not emitted to players or included in LM Studio evidence.
- Keep the current authenticated HTTPS and reverse-SSH boundaries unchanged.

## Acceptance checks

1. Confirm each listed raw item tier pair resolves to its canonical term and that each robot mapping uses the separate robot rules.
2. Confirm `(NULL, NULL)` receives no tier label, remains available through an unqualified name, and type 2/`NULL` remains distinguishable from type 2/0 and type 2/1.
3. Resolve representative item examples: `T1 medium plate`, `T2+ Scorcher`, `T4 prototype sensor amplifier`, and a `T3-` item by a shortened localized or English name.
4. Resolve representative robot examples: `Mk2 Arbalest`, `Mk3 Weasel`, an unqualified unique name such as `Weasel`, and a prototype robot using `P`/`prototype`.
5. Ask for a CT/calibration template/blueprint by a base item name and confirm only `_cprg` candidates qualify. Confirm unrelated suffixes and partial `_cprg` matches do not qualify.
6. Create deliberate collisions (same base name across tiers; an unclear item-versus-robot query) and confirm the user receives translated candidates or a clarification, never an arbitrary selection.
7. Confirm no tier match, ambiguous match, unsupported pair, or absent translation causes model-generated item facts or untranslated internal names to appear.
8. Confirm existing item stats, direct and recursive recipes, robot assembled stats, robot bonuses, and module compatibility are unchanged once an entity has been uniquely resolved.
9. Validate a schema 4 bundle and confirm invalid tier metadata is rejected. Confirm an older bundle retains name-only behavior and remains usable for rollback.
10. Activate the bundle in Dev and verify all checks through the local Main server while Live remains unchanged.

## Out of scope

- Robot class and role resolution.
- New game mechanics or dynamic season state.
- Changes to player chat, Discord triggers, delivery, or server-side request routing.
- Vector database adoption or model fine-tuning.
- A player-facing explanation of hidden database tier numbers.

## Self-review

- Reviewed the entity-default schema: `dbo.entitydefaults` defines nullable `tiertype` and `tierlevel`.
- Reviewed `EntityDefaultReader`: it converts both nullable values to zero when building `TierInfo`, so preserving raw nulls is necessary for the user-defined rules, especially robot prototypes.
- Reviewed the current bundle schema and validator, item and recipe exporter records, `SparkyItemLookup`, robot/module records and lookup, and the knowledge refresh runbook. Schema 3 currently carries localized names and facts but no raw tier metadata; the existing Dev bundle refresh flow is the deployment path.
- The reviewed rules-file proposal keeps operator-maintainable tier terms in bundle data, so routine alias tuning can use the established bundle refresh/activation path rather than an executable redeploy.
- The tier shorthand rules and aliases in this draft come from the user’s mapping. Unlisted pairs are deliberately unmapped.
- No code, bundle, service configuration, or game-server behavior was changed while preparing this design.
