# IMPROVEMENT-027 — Equipment Set Bonus Values in Effect Display

**Date:** 2026-05-24  
**Status:** Approved  
**Area:** Combat / Items / UI

---

## Problem

`SetBonusEffectApplicator` creates one `effect_equipment_set_bonus` effect per active equipment set using `.EnableModifiers(false)`. The client receives the effect token and can display an icon, but no bonus amounts are embedded — the player cannot see what they actually gained.

---

## Goal

Embed the actual `ItemPropertyModifier` values into each per-set effect so the client can display them, using the existing `.EnableModifiers(true).WithPropertyModifiers(...)` builder pattern.

---

## Design

### 1. `EquipmentSetBonusResult`

Add a `ModifiersPerSet` dictionary computed at construction. Keep `Modifiers` as a flat convenience view (computed once, zero-alloc on read). Derive `ActiveSetIds` from dictionary keys.

```csharp
public sealed class EquipmentSetBonusResult
{
    public static readonly EquipmentSetBonusResult Empty =
        new(new Dictionary<int, IReadOnlyList<ItemPropertyModifier>>());

    public EquipmentSetBonusResult(IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> modifiersPerSet)
    {
        ModifiersPerSet = modifiersPerSet;
        Modifiers = modifiersPerSet.Values.SelectMany(x => x).ToArray();
    }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> ModifiersPerSet { get; }
    public IReadOnlyList<ItemPropertyModifier> Modifiers { get; }
    public IEnumerable<int> ActiveSetIds => ModifiersPerSet.Keys;
}
```

The constructor accepts `IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>>`. `Compute()` declares its local dictionary as `Dictionary<int, IReadOnlyList<ItemPropertyModifier>>` and upcasts each `List<T>` to `IReadOnlyList<T>` at assignment — `IReadOnlyDictionary`'s value type parameter is invariant in C#, so the list must be upcast before the dictionary is passed to the constructor.

### 2. `EquipmentSetBonusCalculator.Compute()`

The per-set grouping loop already exists. Change it to build a per-set dictionary instead of a flat list:

```csharp
var modifiersPerSet = new Dictionary<int, IReadOnlyList<ItemPropertyModifier>>();

foreach (KeyValuePair<int, int> entry in countPerSet)
{
    int setId = entry.Key;
    int count = entry.Value;
    List<ItemPropertyModifier> setModifiers = null;

    foreach (SetBonusThreshold threshold in _repository.GetThresholds(setId))
    {
        if (threshold.RequiredPieces <= count)
        {
            setModifiers ??= new List<ItemPropertyModifier>();
            setModifiers.Add(ItemPropertyModifier.Create(threshold.Field, threshold.Value));
        }
    }

    if (setModifiers != null)
        modifiersPerSet[setId] = setModifiers; // List<T> upcast to IReadOnlyList<T>
}

if (modifiersPerSet.Count == 0)
    return EquipmentSetBonusResult.Empty;

return new EquipmentSetBonusResult(modifiersPerSet);
```

### 3. `SetBonusEffectApplicator.Update()`

Accept `IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>>` instead of `IReadOnlySet<int>`. Enable modifiers and embed per-set values at effect creation:

```csharp
public void Update(Robot robot, IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> modifiersPerSet)
{
    if (!robot.InZone)
        return;

    foreach (int setId in _activeTokens.Keys.Except(modifiersPerSet.Keys).ToList())
    {
        robot.EffectHandler.RemoveEffectByToken(_activeTokens[setId]);
        _activeTokens.Remove(setId);
    }

    foreach (int setId in modifiersPerSet.Keys.Except(_activeTokens.Keys).ToList())
    {
        EffectToken token = EffectToken.NewToken();
        EffectBuilder builder = robot.NewEffectBuilder()
            .SetType(EffectType.effect_equipment_set_bonus)
            .EnableModifiers(true)
            .WithPropertyModifiers(modifiersPerSet[setId])
            .WithToken(token);
        robot.ApplyEffect(builder);
        _activeTokens[setId] = token;
    }
}
```

**Explicit decision:** Effects are only created for *newly* active sets (set-difference check). If a set's bonus values change mid-session (e.g. a second piece crosses a new threshold), the effect is not recreated in the same tick. This is acceptable because bonus recalculation fires on fitting events, which always produces a fresh `EquipmentSetBonusResult` — new thresholds will be reflected on the next fitting change that makes the set newly active. No correctness issue.

### 4. `Robot.cs`

Collapse the two separate fields into one:

```csharp
// Before
private IReadOnlyList<ItemPropertyModifier> _setBonusModifiers = Array.Empty<ItemPropertyModifier>();
private IReadOnlySet<int> _activeSetIds = _emptySetIds;

// After
private EquipmentSetBonusResult _setBonusResult = EquipmentSetBonusResult.Empty;
```

`_emptySetIds` static field becomes unused and is removed.

In `Initialize()`:
```csharp
EquipmentSetBonusResult result = EquipmentSetBonusCalculator.Compute(Modules.Select(m => m.Definition));
_setBonusResult = result;
```

In `OnUpdate()`:
```csharp
_setBonusEffectApplicator.Update(this, _setBonusResult.ModifiersPerSet);
```

In `Robot.Properties.cs` — `GetPropertyModifier()` inner loop:
```csharp
foreach (ItemPropertyModifier bonus in _setBonusResult.Modifiers)
```

---

## Files Changed

| File | Change |
|---|---|
| `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusResult.cs` | Add `ModifiersPerSet`, compute flat `Modifiers` at construction, derive `ActiveSetIds` |
| `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs` | Build per-set dictionary instead of flat list |
| `src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs` | New signature, `.EnableModifiers(true)`, `.WithPropertyModifiers()` |
| `src/Perpetuum/Robots/Robot.cs` | Collapse two fields to `_setBonusResult`, remove `_emptySetIds` |
| `src/Perpetuum/Robots/Robot.Properties.cs` | `_setBonusModifiers` → `_setBonusResult.Modifiers` |

---

## Manual Validation

1. Start the server with a robot that has set pieces fitted meeting at least one threshold.
2. Log in and open the effect panel or module tooltip for `effect_equipment_set_bonus`.
3. **Expected:** Bonus stat values are visible in the UI.
4. **If not visible:** The client is not consuming `PropertyModifiers` for this effect type. Log as a separate client-side investigation — out of scope for this improvement.

---

## Regression Areas

- `GetPropertyModifier()` hot path — flat `Modifiers` list is still iterated; no behavioural change, only field rename
- Fitting events — recalculation and effect removal/application logic unchanged
- `EquipmentSetBonusResult.Empty` — still valid sentinel; both `ModifiersPerSet` and `Modifiers` are empty collections
