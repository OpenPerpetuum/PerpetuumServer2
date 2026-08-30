# Equipment Set Bonus Effect Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embed per-set `ItemPropertyModifier` values into each `effect_equipment_set_bonus` effect so the client can display the actual bonus amounts a player receives from their equipment set.

**Architecture:** `EquipmentSetBonusResult` gains a `ModifiersPerSet` dictionary (keyed by set ID) alongside a flat `Modifiers` convenience view computed at construction. The calculator returns modifiers grouped by set; the effect applicator embeds the correct modifier list into each per-set effect via `.EnableModifiers(true).WithPropertyModifiers(...)`. `Robot` stores a single `_setBonusResult` field instead of the two separate fields it had before.

**Tech Stack:** C# 12, .NET 8. No test framework — verification is manual build + in-game observation.

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusResult.cs` | Modify | Add `ModifiersPerSet`, compute flat `Modifiers` at construction, derive `ActiveSetIds` from keys |
| `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs` | Modify | Build per-set dictionary instead of flat list |
| `src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs` | Modify | New signature; `.EnableModifiers(true)` + `.WithPropertyModifiers()` at effect creation |
| `src/Perpetuum/Robots/Robot.cs` | Modify | Replace `_setBonusModifiers` + `_activeSetIds` with single `_setBonusResult`; remove `_emptySetIds` |
| `src/Perpetuum/Robots/Robot.Properties.cs` | Modify | `_setBonusModifiers` → `_setBonusResult.Modifiers` |

---

## Task 1: Update `EquipmentSetBonusResult`

**Files:**
- Modify: `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusResult.cs`

- [ ] **Step 1: Replace the file contents**

Replace the entire file with:

```csharp
using Perpetuum.Items;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Robots.EquipmentSets
{
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
}
```

Key changes from the original:
- Constructor now takes `IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>>` instead of `(IReadOnlyList<ItemPropertyModifier>, IReadOnlySet<int>)`
- `Modifiers` is computed by flattening all values — allocated once at construction, zero-alloc on read
- `ActiveSetIds` is derived from dictionary keys — no separate `HashSet<int>` stored
- `Empty` uses an empty dictionary; both `Modifiers` and `ModifiersPerSet` are empty

- [ ] **Step 2: Build to confirm no other callers are broken yet**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build errors in `EquipmentSetBonusCalculator.cs` and `Robot.cs` (they still pass the old constructor signature). Tasks 2 and 4 fix these. No other files should error at this point.

---

## Task 2: Update `EquipmentSetBonusCalculator`

**Files:**
- Modify: `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs`

- [ ] **Step 1: Replace the file contents**

Replace the entire file with:

```csharp
using System.Collections.Generic;
using Perpetuum.Items;

namespace Perpetuum.Robots.EquipmentSets
{
    public class EquipmentSetBonusCalculator : IEquipmentSetBonusCalculator
    {
        private readonly IEquipmentSetRepository _repository;

        public EquipmentSetBonusCalculator(IEquipmentSetRepository repository)
        {
            _repository = repository;
        }

        public EquipmentSetBonusResult Compute(IEnumerable<int> fittedDefinitions)
        {
            if (fittedDefinitions == null)
                return EquipmentSetBonusResult.Empty;

            var countPerSet = new Dictionary<int, int>();
            foreach (int def in fittedDefinitions)
            {
                foreach (int setId in _repository.GetSetIdsForDefinition(def))
                {
                    countPerSet.TryGetValue(setId, out int current);
                    countPerSet[setId] = current + 1;
                }
            }

            if (countPerSet.Count == 0)
                return EquipmentSetBonusResult.Empty;

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
        }
    }
}
```

Key changes from the original:
- `modifiersPerSet` is `Dictionary<int, IReadOnlyList<ItemPropertyModifier>>` (not `List<>`) so it satisfies the `EquipmentSetBonusResult` constructor's invariant `IReadOnlyDictionary` parameter
- `setModifiers` is still `List<ItemPropertyModifier>` locally (for `.Add()`), then upcast to `IReadOnlyList<ItemPropertyModifier>` at assignment
- The flat `modifiers` list and `activeSetIds` set are gone — the result object computes those itself
- `anyThresholdMet` flag replaced by null-check on `setModifiers`

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: still errors in `Robot.cs` (old constructor call and old field assignments). `EquipmentSetBonusCalculator` errors should be gone.

---

## Task 3: Update `SetBonusEffectApplicator`

**Files:**
- Modify: `src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs`

- [ ] **Step 1: Replace the file contents**

Replace the entire file with:

```csharp
using System.Collections.Generic;
using System.Linq;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Zones.Effects;

namespace Perpetuum.Robots.EquipmentSets
{
    public class SetBonusEffectApplicator
    {
        private readonly Dictionary<int, EffectToken> _activeTokens = new();

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
    }
}
```

Key changes from the original:
- Parameter changes from `IReadOnlySet<int> activeSetIds` to `IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> modifiersPerSet`
- Remove/add set-difference logic now operates on `modifiersPerSet.Keys` (an `IEnumerable<int>`) — `Except()` works fine on it
- `.EnableModifiers(false)` → `.EnableModifiers(true)`
- `.WithPropertyModifiers(modifiersPerSet[setId])` added — embeds the bonus values for this set into the effect
- `using Perpetuum.Items` added for `IReadOnlyList<ItemPropertyModifier>`

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: still errors in `Robot.cs` only.

---

## Task 4: Update `Robot.cs` and `Robot.Properties.cs`

**Files:**
- Modify: `src/Perpetuum/Robots/Robot.cs` (lines 36–41, 138–142, 371)
- Modify: `src/Perpetuum/Robots/Robot.Properties.cs` (line 134)

- [ ] **Step 1: Remove `_emptySetIds` and the two separate bonus fields; add `_setBonusResult`**

In `Robot.cs`, locate this block (around lines 36–41):

```csharp
        private static readonly IReadOnlySet<int> _emptySetIds = new HashSet<int>();

        // Safe: Initialize() is called docked (no zone update) or at zone entry (before zone participation).
        private IReadOnlyList<ItemPropertyModifier> _setBonusModifiers = Array.Empty<ItemPropertyModifier>();
        private IReadOnlySet<int> _activeSetIds = _emptySetIds;
        private readonly SetBonusEffectApplicator _setBonusEffectApplicator = new SetBonusEffectApplicator();
```

Replace with:

```csharp
        // Safe: Initialize() is called docked (no zone update) or at zone entry (before zone participation).
        private EquipmentSetBonusResult _setBonusResult = EquipmentSetBonusResult.Empty;
        private readonly SetBonusEffectApplicator _setBonusEffectApplicator = new SetBonusEffectApplicator();
```

- [ ] **Step 2: Update `Initialize()` to store the full result**

In `Robot.cs`, locate this block (around lines 138–143):

```csharp
            if (EquipmentSetBonusCalculator != null)
            {
                EquipmentSetBonusResult result = EquipmentSetBonusCalculator.Compute(Modules.Select(m => m.Definition));
                _setBonusModifiers = result.Modifiers;
                _activeSetIds = result.ActiveSetIds;
            }
```

Replace with:

```csharp
            if (EquipmentSetBonusCalculator != null)
            {
                _setBonusResult = EquipmentSetBonusCalculator.Compute(Modules.Select(m => m.Definition));
            }
```

- [ ] **Step 3: Update `OnUpdate()` to pass `ModifiersPerSet`**

In `Robot.cs`, locate this line (around line 371):

```csharp
            _setBonusEffectApplicator.Update(this, _activeSetIds);
```

Replace with:

```csharp
            _setBonusEffectApplicator.Update(this, _setBonusResult.ModifiersPerSet);
```

- [ ] **Step 4: Update `GetPropertyModifier()` in `Robot.Properties.cs`**

In `Robot.Properties.cs`, locate this line (around line 134):

```csharp
            foreach (ItemPropertyModifier bonus in _setBonusModifiers)
```

Replace with:

```csharp
            foreach (ItemPropertyModifier bonus in _setBonusResult.Modifiers)
```

- [ ] **Step 5: Build clean**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: **0 errors, 0 warnings** (or only pre-existing warnings unrelated to these files). If `_emptySetIds` was referenced elsewhere, the compiler will tell you — fix by removing the reference.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusResult.cs
git add src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs
git add src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs
git add src/Perpetuum/Robots/Robot.cs
git add src/Perpetuum/Robots/Robot.Properties.cs
git commit -m "feat: embed set bonus modifier values in effect display (IMPROVEMENT-027)"
```

---

## Task 5: Manual Validation

- [ ] **Step 1: Start the server**

```
cd src/Perpetuum.Server
dotnet run -- --GameRoot "E:\PerpetuumServer2\data"
```

- [ ] **Step 2: Log in with a character that has set pieces fitted**

Ensure the robot has at least the minimum number of pieces from a set (e.g. `set_striker`) to meet at least one bonus threshold.

- [ ] **Step 3: Observe `effect_equipment_set_bonus` in the UI**

Open the active effects panel or hover over a fitted module tooltip. Look for the `effect_equipment_set_bonus` effect entry.

- [ ] **Step 4: Verify bonus values are visible**

**Pass:** The effect display shows the actual stat names and values granted by the set bonus (e.g. "+5% damage").

**Fail:** The effect icon appears but no values are shown. This means the client is not consuming `PropertyModifiers` for this effect type. If this happens, log a new backlog item for client-side investigation — do not attempt to fix it server-side.

- [ ] **Step 5: Verify stat computation is unchanged**

Open the robot info panel and confirm the actual stat values (e.g. damage, armor) match expectations for the equipped set. The `GetPropertyModifier()` path uses `_setBonusResult.Modifiers` (flat, same data as before) — no regression expected, but confirm.

- [ ] **Step 6: Equip and unequip a set piece**

Remove one set piece below a threshold and confirm:
- The set effect is removed from the active effects list
- Stats revert correctly

Re-equip and confirm the effect and stat values return.

---

## Task 6: Update Backlog

**Files:**
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Mark IMPROVEMENT-027 as DONE**

In `docs/backlog/improvements.md`, find the `## IMPROVEMENT-027` entry and change:

```
Status: TODO
```

to:

```
Status: DONE
```

- [ ] **Step 2: Commit**

```
git add docs/backlog/improvements.md
git commit -m "docs: mark IMPROVEMENT-027 as DONE"
```
