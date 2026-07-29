
# Claude Knowledge Base — Item, Robot, Effect, and Tech Configuration Guide

## Purpose

This document consolidates SQL creation patterns extracted from internal content scripts.  
Its goal is to help Claude:

- Understand how gameplay entities are structured.
- Generate correct SQL automation tools.
- Create consistent migration scripts.
- Safely extend the game database.
- Avoid missing dependent configuration steps.

This guide should be treated as a procedural reference and dependency map.

---

# Index

1. Architecture Overview
2. Entity Lifecycle
3. Naming Conventions
4. Category Creation
5. Effect System
6. Aggregate Fields
7. Item Definitions
8. Tiering System
9. Calibration Programs
10. Prototypes
11. Aggregate Values / Stats
12. Components & Production Recipes
13. Research Levels
14. Tech Tree Integration
15. Research Costs
16. Prototype Linking
17. Production Duration
18. Enabler Extensions
19. Robot Templates
20. Beam Assignments
21. Chassis Bonuses
22. Paint / Visual Configuration
23. Common SQL Patterns
24. Dependency Order
25. Automation Recommendations for Claude
26. Validation Checklist
27. Common Pitfalls
28. Suggested Automation Architecture

---

# 1. Architecture Overview

The system is highly relational and dependency-driven.

Creating a complete gameplay object usually requires updates across multiple tables.

Example dependencies:

- `categoryFlags`
- `entitydefaults`
- `aggregatefields`
- `aggregatevalues`
- `components`
- `itemresearchlevels`
- `techtree`
- `techtreenodeprices`
- `prototypes`
- `enablerextensions`
- `robottemplates`
- `beamassignment`
- `chassisbonus`
- `definitionconfig`

Claude must understand that item creation is not a single INSERT operation.

A "fully integrated" item often requires:

1. Category registration
2. Entity definition
3. Stats
4. Production recipe
5. Research configuration
6. Tech tree placement
7. Prototype linkage
8. Extension requirements
9. Visual setup
10. Robot linkage (if applicable)

---

# 2. Entity Lifecycle

Typical item lifecycle:

```text
Category
    ↓
Entity Definition
    ↓
Aggregate Stats
    ↓
Recipe / Components
    ↓
Research Levels
    ↓
Tech Tree Placement
    ↓
Research Costs
    ↓
Prototype Linkage
    ↓
Production Duration
    ↓
Enabler Extensions
    ↓
Visual / Robot Configuration
```

Effects follow a different lifecycle:

```text
Effect Category
    ↓
Effect
    ↓
Aggregate Fields
    ↓
Effect Enhancers / Bonuses
```

---

# 3. Naming Conventions

## Definitions

Definitions use strict prefixes:

```text
def_
```

Examples:

```text
def_standard_dreadnought_module
def_named1_dreadnought_module
def_named1_dreadnought_module_pr
```

## Prototype Suffix

```text
_pr
```

Example:

```text
def_named2_dreadnought_module_pr
```

## Calibration Program Suffix

```text
_cprg
```

Example:

```text
def_standard_dreadnought_module_cprg
```

## Effect Naming

```text
effect_<name>
```

Examples:

```text
effect_dreadnought
```

## Category Naming

```text
cf_<group>
```

Examples:

```text
cf_dreadnought_modules
cf_robot_enhancements
```

## Aggregate Fields

Pattern:

```text
<object>_<property>_<modifier>
```

Example:

```text
effect_dreadnought_weapon_cycle_time_modifier
```

---

# 4. Category Creation

Categories are stored in:

```sql
categoryFlags
```

Pattern:

```sql
INSERT INTO categoryFlags
(value, name, note, hidden, isunique)
VALUES (...)
```

Important fields:

| Field | Purpose |
|---|---|
| value | Bitmask / category ID |
| name | Internal identifier |
| note | Human-readable description |
| hidden | Visibility |
| isunique | Uniqueness behavior |

Claude should:

- Reuse categories when appropriate.
- Avoid duplicate category values.
- Detect collisions.

---

# 5. Effect System

## Effect Categories

Stored in:

```sql
effectcategories
```

Pattern:

```sql
INSERT INTO effectcategories (...)
```

## Effects

Stored in:

```sql
effects
```

Key fields:

| Field | Meaning |
|---|---|
| effectcategory | Parent category |
| duration | Effect duration |
| isaura | Aura flag |
| auraradius | Aura range |
| ispositive | Buff/debuff |
| display | UI behavior |
| saveable | Persistence |

Claude should generate UPSERT-style logic.

---

# 6. Aggregate Fields

Aggregate fields define configurable stats and modifiers.

Stored in:

```sql
aggregatefields
```

Examples:

- cycle time modifiers
- damage modifiers
- resistance modifiers
- usage modifiers

Pattern:

```sql
INSERT INTO aggregatefields
(name, formula, measurementunit, ...)
VALUES (...)
```

Important fields:

| Field | Meaning |
|---|---|
| formula | Calculation mode |
| measurementunit | UI unit |
| measurementmultiplier | Scaling |
| measurementoffset | Offset |
| moreisbetter | UI comparison logic |

Claude should:

- Reuse existing aggregate fields whenever possible.
- Detect duplicate semantic fields.
- Preserve measurement consistency.

---

# 7. Item Definitions

Core table:

```sql
entitydefaults
```

This is the primary gameplay object registry.

Example pattern:

```sql
INSERT INTO entitydefaults
(
    definitionname,
    quantity,
    attributeflags,
    categoryflags,
    options,
    note,
    enabled,
    volume,
    mass,
    hidden,
    health,
    descriptiontoken,
    purchasable,
    tiertype,
    tierlevel
)
VALUES (...)
```

## Critical Fields

| Field | Meaning |
|---|---|
| definitionname | Unique identifier |
| attributeflags | Functional capabilities |
| categoryflags | Classification |
| options | Metadata tokens |
| tiertype | Tier family |
| tierlevel | Tier number |

## attributeflags

`attributeflags` is a bitmask over `AttributeFlags` (`src/Perpetuum.ExportedTypes/AttributeFlags.cs`,
`long`-backed), not a set of independent single-purpose columns — the same "verify against a sibling,
don't guess/increment" rule from Options Metadata below applies here too. Notable bits for content
creation:

| Bit | Name | Notes |
|---|---|---|
| 3 | `onePerRobot` | Not read anywhere in `src/Perpetuum` as of this writing — a declarative/data-side flag carried for consistency with sibling items, not (currently) enforced by server logic. |
| 4 | `activeModule` | Required for any module with an `OnAction()` cycle. |
| 18 | `ammo_required` | **Authoritative "this module needs ammo" signal for `Perpetuum.AdminTool`** (`RobotTemplateEditorEntity.IsAmmoable`, `DECISIONS.md` #1) — the tool trusts this bit, not `options.ammoCapacity`/`ammoType`. Server-side ammo behavior is separately driven by `ActiveModule.Ammo.cs`'s `IsAmmoable => ammoCategoryFlags > 0 && AmmoCapacity > 0`, so a module can be functionally ammoable at runtime while still needing this bit set for the AdminTool to recognize it. Set on every ammo-consuming module. |
| 21 | `forceOneCycle` | Forces every activation into `ModuleStateType.Oneshot` (`ActiveModule.States.cs`'s `IdleState.SwitchTo`) regardless of client AutoRepeat request — does not prevent repeated player-initiated activations or conflict with a nonzero `cycle_time` aggregatevalue (`cycle_time` still governs the cooldown between activations). Confirmed (via live DB) present on `def_standard_assault_remote_controller` and all sibling `RemoteControllerModule`s alongside a real `cycle_time` value — do not assume the two are mutually exclusive. |

Example verified sibling value: `def_standard_assault_remote_controller` = `2359320` =
`onePerRobot | activeModule | ammo_required | forceOneCycle`.

## Options Metadata

`options` is a GenXY-encoded string (`src/Perpetuum/GenXY/`): `#key=TOKENvalue#key2=TOKENvalue2...`.
The token prefix determines both the C# type and the number base used to parse `value`
(`GenxyReader`/`GenxyConverter`):

| Token | Meaning | Number base |
|---|---|---|
| `i` | `int` | hex (`NumberStyles.HexNumber`, no `0x` prefix) |
| `L` | `long` | hex |
| `f` | `float`/`double` | decimal |
| `$` | `string` | n/a |

Example:

```text
#moduleFlag=i908#tier=$tierlevel_t1
```

**`moduleFlag` is a bitmask, not an ID.** It is read as `Module.ModuleFlag` (`ED.Options.ModuleFlag`)
and checked against a robot slot's `slotFlags` bitmask in
`RobotComponent.IsValidSlotTo` (`src/Perpetuum/Robots/RobotComponent.cs`):

```csharp
return (moduleFlagMask & slotFlagMask) == moduleFlagMask &&
    (!specializedSlot || specializedModule);
```

Every bit set in the module's `moduleFlag` must also be set in the target slot's mask (subset test), and
the `specialized` bit is an extra gate on top of that. Bit positions come from the `SlotFlags` enum
(`src/Perpetuum/Modules/SlotFlags.cs`): `turret=0, missile=1, melee=2, head=3, chassis=4, leg=5, small=6,
medium=7, large=8, industrial=9, ew_and_engineering=10, specialized=11`.

**Never invent a `moduleFlag` value by incrementing a prior one** (e.g. treating the `i908` example above
as a counter and producing `i909`, `i90a`, ...) — that flips arbitrary low bits on and almost always
produces a bit combination (e.g. `turret|head` together) no real slot satisfies, making the item
unequippable everywhere. Instead, find an existing sibling item of the same equipment kind (same
`RegisterModule<T>`/head-vs-turret-vs-chassis-vs-leg family) and reuse its exact `moduleFlag` value —
query `SELECT definitionname, options FROM entitydefaults WHERE definitionname IN (...)` for known
sibling names, or ask the user for that query's output if no DB access is available. As of this
writing, every plain (non-size-classed) head-slot module — all four
`def_standard_*_remote_controller` modules and `def_standard_neuralyzer` — uses `moduleFlag=i8` (`0x8` =
`SlotFlags.head` only, no size or specialized bit).

**`ammoType` is the ammo item's `categoryFlags` value**, hex-encoded with no leading zeroes and
`L`-prefixed (long token), not a free-form tag. Every ammoable module's `options` sets it to match the
ammo category the module is built to consume, e.g. `def_standard_assault_remote_controller` uses
`ammoType=L4120a` for `cf_assault_drones_units = 0x000000000004120A`
(`src/Perpetuum.ExportedTypes/CategoryFlags.cs`). Cross-check this against the same
`ammoCategoryFlags`/`CategoryFlags` value passed to the module's `ByCategoryFlags<T>(...)` registration
in `src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs` — they must be the same categoryFlags value.
`ammoType` isn't read by server-side code, but `Perpetuum.AdminTool`'s `RobotTemplateSlotViewModel` parses
it to filter valid ammo candidates via a categoryFlags hierarchy match, so omitting or mis-encoding it
silently breaks that tooling rather than throwing at runtime.

Beyond `moduleFlag`/`ammoType`, Claude should preserve existing metadata syntax in general — treat any
option value that "looks like an ID" as a candidate bitmask/enum/categoryFlags value first, and verify
against a sibling row before assuming otherwise.

---

# 8. Tiering System

Observed tiers:

| Tier | Example |
|---|---|
| T1 | standard |
| T2 | named1 |
| T3 | named2 |
| T4 | named3 |

Patterns:

```text
standard
named1
named2
named3
```

Tier hierarchy affects:

- research
- recipes
- tech tree
- prototype progression

Higher tiers often consume previous tiers as ingredients.

---

# 9. Calibration Programs

Calibration programs are special craft/research entities.

Naming:

```text
_cprg
```

Characteristics:

- Usually non-purchasable
- Very low volume/mass
- Used in research progression

Related table:

```sql
itemresearchlevels
```

---

# 10. Prototypes

Prototype items:

- Use `_pr` suffix.
- Represent craftable/researchable blueprints.
- Usually linked to production progression.

Prototype linkage stored in:

```sql
prototypes
```

Pattern:

```sql
MERGE prototypes AS Target
```

Claude should automatically create prototype relations for craftable items.

---

# 11. Aggregate Values / Stats

Actual item stats stored in:

```sql
aggregatevalues
```

Pattern:

```sql
INSERT INTO aggregatevalues
(definition, field, value)
VALUES (...)
```

Resolution flow:

```text
entitydefaults.definition
    ↓
aggregatefields.id
    ↓
aggregatevalues
```

Examples:

- core_usage
- cpu_usage
- cycle_time
- powergrid_usage

Claude should:

- Resolve aggregate field IDs dynamically.
- Never hardcode IDs.
- Use transactional batching.

---

# 12. Components & Production Recipes

Recipes stored in:

```sql
components
```

Preferred pattern:

```sql
MERGE components AS Target
```

Temporary table pattern used:

```sql
DECLARE @tempTable TABLE (...)
```

Recipe progression pattern:

```text
T2 requires T1
T3 requires T2
T4 requires T3
```

## Important Insight

Production is incremental.

Higher tiers inherit previous tiers.

Claude should automatically infer progression chains.

---

# 13. Research Levels

Stored in:

```sql
itemresearchlevels
```

Pattern:

```sql
MERGE itemresearchlevels AS Target
```

Fields:

| Field | Meaning |
|---|---|
| definition | Item |
| researchlevel | Required level |
| calibrationprogram | Calibration item |
| enabled | Availability |

Example progression:

| Tier | Research Level |
|---|---|
| T1 | 5 |
| T2 | 6 |
| T3 | 7 |
| T4 | 8 |

---

# 14. Tech Tree Integration

Stored in:

```sql
techtree
```

Fields:

| Field | Meaning |
|---|---|
| parentdefinition | Parent node |
| childdefinition | Child node |
| groupID | Tech tree group |
| x/y | UI position |
| enablerextensionid | Optional unlock requirement |

Pattern:

```sql
MERGE techtree AS Target
```

## Layout Behavior

Coordinates define visual layout.

Example:

```text
(6,20)
(7,20)
(8,20)
(9,20)
```

Claude should:

- Avoid coordinate overlap.
- Support auto-layout generation.

---

# 15. Research Costs

Stored in:

```sql
techtreenodeprices
```

Linked through:

```sql
techtreepointtypes
```

Example point types:

```text
common
hitech
```

Claude should:

- Scale costs progressively.
- Preserve economic consistency.

---

# 16. Prototype Linking

Prototype relationships stored in:

```sql
prototypes
```

Relationship:

```text
module → prototype
```

Pattern:

```sql
MERGE prototypes AS Target
```

Claude should automatically generate:

- missing prototype links
- reverse dependency validation

---

# 17. Production Duration

Stored in:

```sql
productionduration
```

Linked by category.

Pattern:

```sql
INSERT INTO productionduration
(category, durationmodifier)
VALUES (...)
```

Claude should:

- Reuse existing category modifiers.
- Avoid duplicates.

---

# 18. Enabler Extensions

Defines skill requirements.

Stored in:

```sql
enablerextensions
```

Pattern:

```sql
DELETE FROM enablerextensions WHERE definition = @definition
```

followed by:

```sql
INSERT INTO enablerextensions (...)
```

This indicates full replacement behavior.

## Important

Extensions are resolved dynamically:

```sql
SELECT extensionid
FROM extensions
WHERE extensionname = ...
```

Never hardcode extension IDs.

---

# 19. Robot Templates

Robot assembly stored in:

```sql
robottemplates
robottemplaterelation
```

Robot template description format:

```text
#robot=iHEX
#head=iHEX
#chassis=iHEX
#leg=iHEX
#container=iHEX
```

Generated using:

```sql
FORMAT(@robot, 'X')
```

## Important

Robots are assembled from component definitions.

Claude should understand robot composition architecture.

---

# 20. Beam Assignments

Used for visual weapon/ammo effects.

Stored in:

```sql
beamassignment
```

Workflow:

1. Resolve beam ID.
2. Delete existing assignment.
3. Insert replacement.

Pattern:

```sql
DELETE FROM beamassignment WHERE definition = ...
INSERT INTO beamassignment (...)
```

---

# 21. Chassis Bonuses

Stored in:

```sql
chassisbonus
```

Observed workflow:

1. Copy bonuses from source robot.
2. Replace extension references.
3. Reuse configuration.

Pattern:

```sql
INSERT INTO chassisbonus
(SELECT ... FROM chassisbonus WHERE definition = @sourceDefinition)
```

This is effectively template inheritance.

Claude should support:

- bonus cloning
- extension remapping
- inheritance templates

---

# 22. Paint / Visual Configuration

Stored in:

```sql
definitionconfig
```

Example:

```sql
INSERT INTO definitionconfig
(definition, tint)
VALUES (...)
```

Tint example:

```text
#D65617
```

Claude should:

- Preserve consistent color formats.
- Avoid duplicate config rows.

---

# 23. Common SQL Patterns

## IF NOT EXISTS

Used for idempotency.

```sql
IF NOT EXISTS (...)
BEGIN
    INSERT ...
END
```

## MERGE

Used for UPSERT behavior.

Observed in:

- components
- prototypes
- techtree
- itemresearchlevels

## DELETE + INSERT

Used when full replacement is intended.

Observed in:

- enablerextensions
- beamassignment

## Dynamic ID Resolution

Common pattern:

```sql
SELECT TOP 1 definition
FROM entitydefaults
WHERE definitionname = ...
```

Claude should ALWAYS prefer dynamic lookup over hardcoded IDs.

---

# 24. Dependency Order

## Minimal Item Flow

```text
categoryFlags
    ↓
entitydefaults
    ↓
aggregatevalues
```

## Full Craftable Item Flow

```text
categoryFlags
    ↓
entitydefaults
    ↓
aggregatevalues
    ↓
components
    ↓
itemresearchlevels
    ↓
techtree
    ↓
techtreenodeprices
    ↓
prototypes
```

## Robot Flow

```text
entitydefaults
    ↓
enablerextensions
    ↓
robottemplates
    ↓
robottemplaterelation
    ↓
definitionconfig
```

---

# 25. Automation Recommendations for Claude

Claude should generate tools capable of:

## A. Full Entity Scaffolding

Input:

```yaml
name:
tier:
category:
craftable:
robot:
effect:
```

Output:

- all SQL required
- dependency-safe ordering
- rollback-safe script

## B. Tier Chain Generator

Generate:

- T1
- T2
- T3
- T4
- prototypes
- calibration programs

Automatically.

## C. Recipe Generator

Should infer:

- previous tier dependency
- scaling resource costs
- shard/component progression

## D. Tech Tree Auto-Placer

Automatically assign:

- x/y coordinates
- parent links
- research scaling

## E. Clone-Based Templates

Support:

- robot cloning
- chassis bonus inheritance
- stat inheritance
- extension remapping

---

# 26. Validation Checklist

Before considering content complete, Claude should validate:

## Definitions

- Unique names
- Correct suffixes
- Valid categories

## Aggregate Fields

- Existing field references
- Measurement consistency

## Recipes

- All components exist
- No circular dependency

## Tech Tree

- No overlapping coordinates
- Parent exists

## Extensions

- Extension exists
- Levels valid

## Robots

- All parts exist
- Template linked correctly

## Modules / Ammoable Equipment

- `moduleFlag` matches a verified sibling item of the same slot family (not a guessed/incremented value)
- `ammoType` (if ammoable) matches the ammo's actual `categoryFlags` value, hex with no leading zeroes,
  `L`-prefixed, and agrees with the `ammoCategoryFlags` passed in `EntitiesModule.cs`
- `attributeflags` includes `ammo_required` (bit 18) on every ammo-consuming module (required for
  `Perpetuum.AdminTool`'s ammo detection, independent of the server-side `ammoCapacity`/`ammoType` check)
- `attributeflags` otherwise matches a verified sibling item of the same module family, not just the
  bits the server is known to read (e.g. `onePerRobot`, `forceOneCycle`)

---

# 27. Common Pitfalls

## Missing Prototype Links

Creates broken research/crafting progression.

## Hardcoded IDs

Unsafe across environments.

Always resolve dynamically.

## Missing Aggregate Fields

Results in invisible or broken stats.

## Incomplete Robot Templates

Creates unusable robots.

## Incorrect Tier Chains

Breaks production progression.

## Missing Research Entries

Item becomes inaccessible.

## Guessed or Incremented Bitmask Values

`moduleFlag` (SlotFlags bitmask), `ammoType` (categoryFlags value), and `attributeflags` (AttributeFlags
bitmask) are not IDs — see the "attributeflags" and "Options Metadata" sections above. Incrementing an
example/prior value, or omitting a bit that "doesn't obviously do anything" (e.g. `ammo_required`,
which only `Perpetuum.AdminTool` reads), produces an unequippable module, a broken ammo dropdown, or a
silent tooling mismatch instead of a server-side error. Always match a verified sibling item's
`attributeflags`/`options` values exactly, or ask the user to run a lookup query against the target DB.

---

# 28. Suggested Automation Architecture

Recommended Claude automation pipeline:

```text
Specification
    ↓
Validation
    ↓
Dependency Resolution
    ↓
SQL Generation
    ↓
Cross-Reference Validation
    ↓
UPSERT Normalization
    ↓
Migration Packaging
```

## Suggested Internal Modules

### Definition Builder

Creates:

- entitydefaults
- categoryflags

### Stat Builder

Creates:

- aggregatefields
- aggregatevalues

### Research Builder

Creates:

- itemresearchlevels
- techtree
- techtreenodeprices

### Production Builder

Creates:

- recipes
- productionduration
- prototypes

### Robot Builder

Creates:

- robottemplates
- beamassignment
- chassisbonus
- paint configuration

---

# Final Notes

The SQL samples demonstrate a mature content pipeline centered around:

- idempotent migrations
- dynamic ID resolution
- incremental progression
- relational composition
- reusable templates

Claude should prioritize:

1. Idempotency
2. Dependency safety
3. Reusability
4. Dynamic resolution
5. Template inheritance
6. Full-chain generation

Partial generation should be avoided whenever possible.

If required, Claude should ask user to provide existing values from the database.
