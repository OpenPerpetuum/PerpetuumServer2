# IMPROVEMENT-025: Equipment Set Synergy Bonuses — Design Spec

**Date:** 2026-05-23
**Status:** Approved
**Branch:** p36.2

---

## Overview

Introduce an equipment set mechanic: modules belonging to the same named set grant additional stat bonuses that scale with the number of set pieces fitted. Bonuses are threshold-based (2-piece unlocks one bonus, 4-piece unlocks another). Bonuses are visible in the fitting screen (docked) and in zone. A named effect notifies the player when a threshold is active. No client changes required.

---

## Constraints

- Game client cannot be changed.
- All stat delivery must go through the existing `AggregateField` / `ItemPropertyModifier` pipeline.
- Effect display must use the existing `EffectType` + `effects` DB table mechanism (already extended by OPP up to ID 138).
- Fitting is docked-only; zone entry re-initializes the robot from DB.

---

## Data Layer

### New Tables

**`equipment_sets`**
```sql
set_id   INT          NOT NULL IDENTITY PRIMARY KEY,
name     NVARCHAR(64) NOT NULL UNIQUE   -- e.g. 'set_striker'
```

**`equipment_set_members`**
```sql
set_id      INT NOT NULL REFERENCES equipment_sets(set_id),
definition  INT NOT NULL REFERENCES entitydefaults(definition),
PRIMARY KEY (set_id, definition)
```
Each row maps one module definition to a set. A module may belong to at most one set (enforced by content convention, not schema).

**`equipment_set_bonus_thresholds`**
```sql
set_id          INT   NOT NULL REFERENCES equipment_sets(set_id),
required_pieces INT   NOT NULL,
aggregate_field INT   NOT NULL,   -- AggregateField enum value
bonus_value     FLOAT NOT NULL,   -- e.g. 1.05 for a 5% Modifier bonus
PRIMARY KEY (set_id, required_pieces, aggregate_field)
```
Each row is one bonus unlocked at a specific piece count. Multiple rows with the same `(set_id, required_pieces)` are allowed (multiple fields per threshold).

### EffectType Extension

Add to `Perpetuum.ExportedTypes/EffectType.cs`:
```csharp
effect_equipment_set_bonus = 139,
```

Add to the `effects` DB table:
```
id=139, name='Set Bonus Active', ispositive=1, display=1,
description='An equipment set bonus is active on this robot.',
duration=0, isaura=0, effectcategory=<passive category>
```

No `effectdefaultmodifiers` rows — the effect carries no property modifiers. Stats are applied exclusively via the aggregate pipeline.

---

## Server-Side Architecture

### `IEquipmentSetRepository` (new)

Loaded once at startup from the three new tables; cached in memory (pattern: `DefaultPropertyModifierReader`).

```csharp
public interface IEquipmentSetRepository
{
    // Returns set_id for each definition that belongs to a set
    ILookup<int, int> GetSetMemberships(IEnumerable<int> definitions);

    // Returns all threshold rows for a set, ordered by required_pieces
    IEnumerable<SetBonusThreshold> GetThresholds(int setId);
}

public readonly struct SetBonusThreshold
{
    public int RequiredPieces { get; }
    public AggregateField Field { get; }
    public double Value { get; }
}
```

Autofac registration: singleton.

### `EquipmentSetBonusCalculator` (new)

Pure stateless service. Given a list of fitted module definitions, returns active bonus modifiers.

```csharp
public interface IEquipmentSetBonusCalculator
{
    IReadOnlyList<ItemPropertyModifier> Compute(IEnumerable<int> fittedDefinitions);
}
```

**Algorithm:**
1. Look up set memberships for all fitted definitions (one repository call).
2. Count fitted instances per `set_id` (duplicates count — each instance contributes).
3. For each set, retrieve thresholds; include all threshold rows where `required_pieces <= actual_count`.
4. Accumulate into `List<ItemPropertyModifier>`.
5. Return as `IReadOnlyList<ItemPropertyModifier>`.

No DB access at call time — all data is from the in-memory repository.

Autofac registration: singleton.

### `Robot` changes

**New field:**
```csharp
private IReadOnlyList<ItemPropertyModifier> _setBonusModifiers
    = Array.Empty<ItemPropertyModifier>();
```

**`Initialize()` override:**
```csharp
public override void Initialize()
{
    InitComponents();
    _setBonusModifiers = _setBonusCalculator.Compute(
        Modules.Select(m => m.Definition));
    base.Initialize();
}
```
`_setBonusCalculator` injected via constructor.

**`GetPropertyModifier()` override:**
```csharp
public override ItemPropertyModifier GetPropertyModifier(AggregateField field)
{
    ItemPropertyModifier modifier = base.GetPropertyModifier(field);

    foreach (RobotComponent component in RobotComponents)
    {
        component.GetPropertyModifier(field).Modify(ref modifier);
    }

    foreach (ItemPropertyModifier bonus in _setBonusModifiers)
    {
        if (bonus.Field == field)
            bonus.Modify(ref modifier);
    }

    return modifier;
}
```

No DB access, no allocation — iterates a small in-memory list.

### `SetBonusEffectApplicator` (new, owned by `Robot`)

Tracks which sets currently have a display effect applied. Called from the robot's zone update path alongside `PassiveEffectModule.Update()`.

```csharp
public class SetBonusEffectApplicator
{
    private readonly Dictionary<int, EffectToken> _activeTokens = new();

    public void Update(Robot robot, IEnumerable<int> activeSetIds)
    {
        var incoming = activeSetIds.ToHashSet();

        // Remove effects for sets no longer active
        foreach (int setId in _activeTokens.Keys.Except(incoming).ToList())
        {
            robot.EffectHandler.RemoveEffectByToken(_activeTokens[setId]);
            _activeTokens.Remove(setId);
        }

        // Apply effects for newly active sets
        foreach (int setId in incoming.Except(_activeTokens.Keys))
        {
            var token = EffectToken.NewToken();
            var builder = robot.NewEffectBuilder()
                .SetType(EffectType.effect_equipment_set_bonus)
                .EnableModifiers(false)
                .WithToken(token);
            robot.ApplyEffect(builder);
            _activeTokens[setId] = token;
        }
    }
}
```

`activeSetIds` is derived from `_setBonusModifiers` (the set IDs that contributed at least one modifier).

**Zone-only constraint:** `SetBonusEffectApplicator.Update()` is only called when `robot.InZone` is true. Docked robots get stats via the aggregate pipeline but no effect display — consistent with how `PassiveEffectModule` works.

---

## Fitting Change Flow

1. `EquipModule` / `RemoveModule` handler fires.
2. `robot.Initialize(character)` called (already exists).
3. `Initialize()` recomputes `_setBonusModifiers` from scratch.
4. `robot.ToDictionary()` includes updated aggregate stats in the response.
5. Client receives improved/reduced stats immediately in the fitting panel.

## Zone Entry Flow

1. Robot loaded from DB → `Initialize()` → `_setBonusModifiers` computed.
2. On first zone update tick, `SetBonusEffectApplicator.Update()` called.
3. For each set with an active threshold, a display effect is applied.
4. Client sees "Set Bonus Active" in the robot's effect list.

---

## Edge Cases

| Scenario | Behaviour |
|---|---|
| Duplicate set pieces fitted | Each instance counts toward piece count |
| Module unequipped mid-session | `Initialize()` recomputes; `SetBonusEffectApplicator` removes effect |
| Multiple active sets | One effect token per set; flat modifier list aggregates all |
| 3 pieces in a 2/4 set | 2-piece thresholds active; 4-piece thresholds excluded |
| Unknown set_id / missing rows | Repository returns empty; calculator produces no modifiers |
| Robot repack / swap | Zone exit clears effects; next deploy re-applies via `Initialize()` |

---

## Content Pipeline (Pilot Set)

Define one set to validate the full pipeline:

- **Set name:** `set_striker`
- **Members:** 3–4 existing module definitions (e.g. medium armor plates)
- **Thresholds:**
  - 2 pieces → `armor_max_modifier = 1.05` (+5% max armor)
  - 4 pieces → `kinetic_resist_modifier = 1.08` (+8% kinetic resist)

**SQL authoring rules:**
- Always resolve `set_id` dynamically: `SELECT set_id FROM equipment_sets WHERE name = N'set_striker'`
- Never hardcode `set_id` values
- Follow `docs/content/claude_game_content_guide.md` section 2 (entity lifecycle) and section 3 (naming conventions)
- Use idempotent patterns (`MERGE` or `IF NOT EXISTS`)

---

## Admin Tool

Out of scope for this improvement. Sets are authored via content SQL. A future improvement can add a set editor UI once the runtime system is validated.

---

## Performance

- `GetPropertyModifier()` iterates `_setBonusModifiers` — typically 0–6 entries, no allocation, no DB access.
- Repository is read-only singleton loaded at startup.
- Calculator is called only at `Initialize()` time (fitting events and zone entry), not per-tick.
- No impact on zone update hot path.

---

## Manual Validation Steps

1. Fit 1 set piece → confirm no set bonus in stats; no effect active in zone.
2. Fit 2 set pieces → confirm 2-piece threshold bonus visible in fitting stats; effect appears in zone.
3. Fit 4 set pieces → confirm both thresholds active; stats reflect cumulative bonuses.
4. Fit 2 identical set pieces → confirm each instance counts (2 instances = 2-piece threshold met).
5. Unequip one piece to drop below threshold → confirm bonus removed from stats; effect removed in zone.
6. Deploy robot → confirm effect visible; undeploy and redeploy → confirm effect re-applies correctly.
7. Two different active sets simultaneously → confirm both effects present and both bonus sets applied.

---

## Potential Regressions

- `Robot.GetPropertyModifier()` change: any code that calls this and caches results must handle the new modifier source — verify `UpdateRelatedProperties()` is called after fitting changes (it already is via `Initialize()`).
- `Robot.Initialize()` now calls the calculator — verify it is safe to call during robot construction and that `Modules` is populated before `Initialize()` is invoked.
- `SetBonusEffectApplicator` effect removal path: verify `RemoveEffectByToken` is safe to call when the token has already expired or the robot is dead.
