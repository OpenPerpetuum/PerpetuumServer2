# Sparky Robot Knowledge Design

**Status:** Approved
**Date:** 2026-10-13

## Goal

Extend Sparky’s offline knowledge bundle so it can answer player questions about robot stats, parts, chassis bonuses, and module compatibility using the same construction and fitting rules as the game server.

This is an add-on to the approved [Sparky Knowledge Expansion Design](2026-10-12-sparky-knowledge-expansion-design.md). It does not change game or Discord request routing.

## Source of Truth

Robot facts must come from the server’s domain objects and existing calculations, not from summing database rows in a parallel exporter implementation.

The offline bundle builder will use `OfflineContentRuntime`, which registers the entity and robot-template modules against the local game root and database copy. `RobotTemplateRelations` resolves each robot definition to its configured `RobotTemplate`; `RobotTemplateReader` parses the template description using the game’s GenXY converter. The builder will create the robot from that template and initialize it before reading its properties.

This follows the server’s existing `GetEntityDefaults` construction path. After initialization, the builder will read the robot’s computed `ItemProperty` values and the component properties for the breakdown. The initialized robot exposes the combined effects of its components, fitted modules, and equipment-set bonuses through the game’s robot property code. `Robot.GetPropertyModifier` combines component modifiers when calculating a robot property. The exporter will use those resulting domain values for assembled stats rather than adding `aggregatevalues` rows itself.

Definitions without a real `robottemplaterelation` will not receive assembled robot stats. The builder must not use `GetRelatedTemplateOrDefault` for this export: its starter-robot fallback could attach an unrelated template and produce false facts. The build report should identify skipped definitions without exposing them as answerable robots.

## Bundle Data

Add structured robot records to the versioned knowledge bundle for translated `cf_robots` definitions with a valid template relation. A record may include:

- the localized robot name;
- assembled baseline stats computed after server initialization;
- localized component names and each component’s own recognized stats;
- component slot counts and the module-slot compatibility facts derived from the server’s component and module rules;
- localized chassis-bonus facts associated with their component, target property, and extension.

Only labels and names present in a supported custom dictionary may be shown. Missing dictionary keys are omitted, following the existing item-stat rule. Do not expose untranslated database names, definition IDs, extension IDs, or raw slot masks in answers.

### Chassis Bonuses

Chassis bonuses are conditional facts, separate from the assembled baseline. `RobotComponent.ExtensionBonuses` obtains them from the server’s extension reader, and `Player.GetPropertyModifier` applies them according to that character’s extension levels. The offline build has no player’s extension levels, so Sparky must not bake a presumed character bonus into baseline stats.

Store enough verified data to explain which translated extension affects which translated property on which component, including the source bonus value and aggregate-field metadata. When describing an effect, calculate it using the server’s `ItemPropertyModifier` normalization and aggregate formula, and state the extension level the value represents. The model must not calculate or reinterpret it. Do not turn this into a personalized total without that character’s extension levels. If the required extension or field label is untranslated, omit that bonus from the localized view.

When the same extension and property bonus occurs on multiple components of one assembled robot, combine those rows using the same rule as `Player.GetPropertyModifier`. Keep different extensions distinct when their levels are unknown.

### Fitting Compatibility

Compatibility facts must follow the existing rules in `RobotComponent.IsValidSlotTo` and `RobotComponent.CanEquipModule`, including slot flags, specialized slots, `AllowedBots`, and unique-category restrictions. The answer should identify compatible component slots and state relevant module CPU and power-grid usage when available. It describes whether a module is permitted in a slot on that robot; it does not imply that an occupied slot is free.

The current loadout and character extensions are not known to Sparky. A compatibility answer therefore describes whether the module is permitted by the robot/component rules; it must not claim that it fits the caller’s current loadout or that a full fitting has sufficient CPU or power grid. Those checks depend on occupied slots, other installed modules, set bonuses, and character extensions. Ask a follow-up if the player needs to assess a complete fitting.

## Answer Behavior

- Resolve the robot and any requested component or module against the localized bundle before generating a response.
- Include translated robot, component, and module names in the same ambiguity checks used for item and recipe aliases; if a name resolves to multiple records, ask the player to clarify.
- For a stats question, use only the selected robot’s assembled baseline stats and, when requested, its component breakdown.
- For a chassis-bonus question, use the bundle’s conditional extension facts and server-calculated formula results.
- For a module-compatibility question, use the server-derived compatibility facts and clearly separate slot eligibility from loadout capacity.
- Send only the small, question-specific verified fact set to the local model. Keep the existing answer-length limit and the established source-only answer policy.
- Keep names, stat labels, extension names, and units in the caller’s supported dictionary language. If a requested name or label is unavailable, follow the existing localized missing-term behavior.

## Refresh and Compatibility

Robot records are generated on the development machine from its local database copy and game data, then transferred and activated using the existing knowledge refresh flow. The live database is never queried. Include robot data in the game-content revision hash and validate its references, translations, limits, and localized views before activation.

Because this adds required bundle sections, increment the bundle schema version and update the exporter and Sparky service together. Preserve the previous-bundle rollback behavior. The game server’s request API and routing do not need to change for this feature.

## Out of Scope

- Current character fittings, current inventory, account or character records, and personalized extension levels.
- Predictions about whether a whole custom fitting is viable without the caller providing that fitting.
- New game-server commands, Discord behavior, or live-database access.
- Model-generated arithmetic or unsourced robot facts.

## Acceptance Checks

1. Build a bundle for a robot whose useful stats are distributed across multiple parts. Confirm the assembled stats reflect the initialized robot’s domain properties and that component-level facts remain attributable to the correct part.
2. For a robot with installed modules or an equipment-set bonus in its template, confirm the assembled values follow server initialization and set-bonus calculation.
3. For a chassis with bonuses on multiple parts, confirm the baseline remains character-independent and each localized conditional bonus points to the correct extension and property.
4. Ask whether a translated module can fit a translated robot. Confirm slot and allowed-robot results agree with `RobotComponent` rules, and the response does not promise CPU/power-grid capacity for an unknown fitting.
5. Confirm untranslated robot, module, extension, and field names are not emitted, and an invalid/missing robot template does not silently inherit the starter robot’s data.
6. Validate and activate the new bundle on the AI machine, verify the expected bundle version through health, and exercise representative robot-stat, component, chassis-bonus, and fitting questions through the local API.

## Self-Review Notes

- Reviewed the relevant server path in `OfflineContentRuntime`, `RobotTemplatesModule`, `RobotTemplateReader`, `RobotTemplateRelations`, `RobotTemplate`, `Robot.Initialize`, `Robot.GetPropertyModifier`, `RobotComponent`, `Player.GetPropertyModifier`, and `GetEntityDefaults`.
- Reviewed the documented `robottemplates`, `robottemplaterelation`, `chassisbonus`, `extensions`, and `aggregatefields` schema. These sources help load and label data; server object construction and calculations remain authoritative for the exported facts.
- No code, database, bundle, service, or game-server behavior is changed by this design document.
- Risk: the generic `RobotTemplate.Build()` path and `Initialize()` may encounter a definition that assumes runtime-only services. The implementation must report and skip that definition rather than substitute another robot or abort unrelated exports, unless the failure indicates the whole bundle is invalid.
- Risk: a bundle schema change requires the AI-machine CLI/service binaries to be updated together with the generated bundle.
