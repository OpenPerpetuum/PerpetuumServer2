# Equipment Set Synergy Bonuses (IMPROVEMENT-025) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce equipment set bonuses — modules belonging to the same named set grant threshold-based aggregate stat bonuses, visible in the fitting screen and communicated via a named effect in-zone.

**Architecture:** A startup-cached `EquipmentSetRepository` feeds an `EquipmentSetBonusCalculator` that is called from `Robot.Initialize()`. Results are stored as `_setBonusModifiers` (injected in `GetPropertyModifier()`) and `_activeSetIds` (consumed by `SetBonusEffectApplicator` in `Robot.OnUpdate()`). No client changes required.

**Tech Stack:** C# 12 / .NET 8, SQL Server, Autofac DI, existing `ItemPropertyModifier` / `AggregateField` pipeline, existing `EffectBuilder` / `EffectType` system.

---

## File Map

**Create:**
- `docs/Patches/p36.2/Features/EquipmentSets/migration.sql`
- `docs/Patches/p36.2/Features/EquipmentSets/set_striker_pilot.sql`
- `src/Perpetuum/Robots/EquipmentSets/SetBonusThreshold.cs`
- `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusResult.cs`
- `src/Perpetuum/Robots/EquipmentSets/IEquipmentSetRepository.cs`
- `src/Perpetuum/Robots/EquipmentSets/EquipmentSetRepository.cs`
- `src/Perpetuum/Robots/EquipmentSets/IEquipmentSetBonusCalculator.cs`
- `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs`
- `src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs`

**Modify:**
- `src/Perpetuum.ExportedTypes/EffectType.cs` — add `effect_equipment_set_bonus = 139`
- `src/Perpetuum/Robots/Robot.cs` — property injection, `Initialize()`, `_setBonusEffectApplicator` field, `OnUpdate()` wiring
- `src/Perpetuum/Robots/Robot.Properties.cs` — `GetPropertyModifier()` override
- `src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs` — register repository + calculator
- `src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs` — register `Effect` keyed on `effect_equipment_set_bonus`

---

## Task 1: DB Schema Migration

**Files:**
- Create: `docs/Patches/p36.2/Features/EquipmentSets/migration.sql`

- [ ] **Step 1: Write the migration SQL**

```sql
-- Equipment Set Synergy Bonuses Migration (IMPROVEMENT-025)
-- Run once against the game database before deploying the updated server binary.

CREATE TABLE equipment_sets (
    set_id  INT          NOT NULL IDENTITY(1,1),
    name    NVARCHAR(64) NOT NULL,
    CONSTRAINT PK_equipment_sets PRIMARY KEY (set_id),
    CONSTRAINT UQ_equipment_sets_name UNIQUE (name)
);

CREATE TABLE equipment_set_members (
    set_id      INT NOT NULL,
    definition  INT NOT NULL,
    CONSTRAINT PK_equipment_set_members PRIMARY KEY (set_id, definition),
    CONSTRAINT FK_equipment_set_members_set FOREIGN KEY (set_id)
        REFERENCES equipment_sets (set_id),
    CONSTRAINT FK_equipment_set_members_def FOREIGN KEY (definition)
        REFERENCES entitydefaults (definition)
);

CREATE TABLE equipment_set_bonus_thresholds (
    set_id          INT   NOT NULL,
    required_pieces INT   NOT NULL,
    aggregate_field INT   NOT NULL,
    bonus_value     FLOAT NOT NULL,
    CONSTRAINT PK_equipment_set_bonus_thresholds
        PRIMARY KEY (set_id, required_pieces, aggregate_field),
    CONSTRAINT FK_equipment_set_bonus_thresholds_set FOREIGN KEY (set_id)
        REFERENCES equipment_sets (set_id)
);
```

- [ ] **Step 2: Run the migration**

Execute `docs/Patches/p36.2/Features/EquipmentSets/migration.sql` against the game DB.
Verify: all three tables exist and are empty.

```sql
SELECT 'equipment_sets' AS tbl, COUNT(*) AS rows FROM equipment_sets
UNION ALL
SELECT 'equipment_set_members', COUNT(*) FROM equipment_set_members
UNION ALL
SELECT 'equipment_set_bonus_thresholds', COUNT(*) FROM equipment_set_bonus_thresholds;
```

Expected: three rows, all with `rows = 0`.

- [ ] **Step 3: Commit**

```
git add docs/Patches/p36.2/Features/EquipmentSets/migration.sql
git commit -m "feat: add equipment set DB schema (IMPROVEMENT-025)"
```

---

## Task 2: EffectType Enum + Effect DB Row

**Files:**
- Modify: `src/Perpetuum.ExportedTypes/EffectType.cs`
- Modify: `src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs`

- [ ] **Step 1: Add enum value**

In `src/Perpetuum.ExportedTypes/EffectType.cs`, after the last line `effect_field_reactor_stabilizer = 138,`, add:

```csharp
        effect_equipment_set_bonus = 139,
```

The enum should end:
```csharp
        effect_field_stealth = 136,
        effect_field_eccm = 137,
        effect_field_reactor_stabilizer = 138,
        effect_equipment_set_bonus = 139,
    }
}
```

- [ ] **Step 2: Register the effect type in Autofac**

In `src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs`, inside `Load()`, after the field effect lines (around line 108), add:

```csharp
            // Equipment set bonus display effect
            _ = builder.RegisterType<Effect>().Keyed<Effect>(EffectType.effect_equipment_set_bonus);
```

- [ ] **Step 3: Insert effects DB row**

Execute against the game DB:

```sql
-- Determine effectcategory value for a passive non-categorised effect (0 = undefined)
INSERT INTO effects (id, name, description, duration, ispositive, isaura, auraradius, display, effectcategory)
VALUES (
    139,
    N'Set Bonus Active',
    N'An equipment set bonus is active on this robot.',
    0,       -- permanent (no timer)
    1,       -- ispositive = true
    0,       -- not an aura
    0,       -- aura radius = n/a
    1,       -- display = true (show to client)
    0        -- effectcategory = undefined (not ECCM-clearable)
);
```

Verify:
```sql
SELECT * FROM effects WHERE id = 139;
```

Expected: one row with `name = 'Set Bonus Active'`.

- [ ] **Step 4: Build to verify no compile errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.ExportedTypes/EffectType.cs
git add src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs
git commit -m "feat: add effect_equipment_set_bonus effect type (IMPROVEMENT-025)"
```

---

## Task 3: Data Types and Repository Interface

**Files:**
- Create: `src/Perpetuum/Robots/EquipmentSets/SetBonusThreshold.cs`
- Create: `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusResult.cs`
- Create: `src/Perpetuum/Robots/EquipmentSets/IEquipmentSetRepository.cs`

- [ ] **Step 1: Create `SetBonusThreshold`**

```csharp
using Perpetuum.ExportedTypes;

namespace Perpetuum.Robots.EquipmentSets
{
    public readonly struct SetBonusThreshold
    {
        public SetBonusThreshold(int requiredPieces, AggregateField field, double value)
        {
            RequiredPieces = requiredPieces;
            Field = field;
            Value = value;
        }

        public int RequiredPieces { get; }
        public AggregateField Field { get; }
        public double Value { get; }
    }
}
```

- [ ] **Step 2: Create `EquipmentSetBonusResult`**

```csharp
using Perpetuum.Items;
using System.Collections.Generic;

namespace Perpetuum.Robots.EquipmentSets
{
    public sealed class EquipmentSetBonusResult
    {
        public static readonly EquipmentSetBonusResult Empty =
            new(Array.Empty<ItemPropertyModifier>(), new HashSet<int>());

        public EquipmentSetBonusResult(
            IReadOnlyList<ItemPropertyModifier> modifiers,
            IReadOnlySet<int> activeSetIds)
        {
            Modifiers = modifiers;
            ActiveSetIds = activeSetIds;
        }

        public IReadOnlyList<ItemPropertyModifier> Modifiers { get; }
        public IReadOnlySet<int> ActiveSetIds { get; }
    }
}
```

- [ ] **Step 3: Create `IEquipmentSetRepository`**

```csharp
using System.Collections.Generic;

namespace Perpetuum.Robots.EquipmentSets
{
    public interface IEquipmentSetRepository
    {
        void Init();

        /// <summary>Returns all set IDs the given module definition belongs to.</summary>
        IEnumerable<int> GetSetIdsForDefinition(int definition);

        /// <summary>Returns all threshold rows for the given set, ordered by required_pieces.</summary>
        IEnumerable<SetBonusThreshold> GetThresholds(int setId);
    }
}
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Robots/EquipmentSets/
git commit -m "feat: add equipment set data types and repository interface (IMPROVEMENT-025)"
```

---

## Task 4: Repository Implementation

**Files:**
- Create: `src/Perpetuum/Robots/EquipmentSets/EquipmentSetRepository.cs`

- [ ] **Step 1: Write the repository**

```csharp
using Perpetuum.Data;
using Perpetuum.ExportedTypes;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Robots.EquipmentSets
{
    public class EquipmentSetRepository : IEquipmentSetRepository
    {
        private ILookup<int, int> _definitionToSetIds;
        private ILookup<int, SetBonusThreshold> _setIdToThresholds;

        public void Init()
        {
            _definitionToSetIds = Db.Query()
                .CommandText("SELECT set_id, definition FROM equipment_set_members")
                .Execute()
                .ToLookup(
                    r => r.GetValue<int>("definition"),
                    r => r.GetValue<int>("set_id"));

            _setIdToThresholds = Db.Query()
                .CommandText("SELECT set_id, required_pieces, aggregate_field, bonus_value FROM equipment_set_bonus_thresholds ORDER BY set_id, required_pieces")
                .Execute()
                .ToLookup(
                    r => r.GetValue<int>("set_id"),
                    r => new SetBonusThreshold(
                        r.GetValue<int>("required_pieces"),
                        (AggregateField)r.GetValue<int>("aggregate_field"),
                        r.GetValue<double>("bonus_value")));
        }

        public IEnumerable<int> GetSetIdsForDefinition(int definition)
        {
            return _definitionToSetIds[definition];
        }

        public IEnumerable<SetBonusThreshold> GetThresholds(int setId)
        {
            return _setIdToThresholds[setId];
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum/Robots/EquipmentSets/EquipmentSetRepository.cs
git commit -m "feat: implement EquipmentSetRepository with in-memory cache (IMPROVEMENT-025)"
```

---

## Task 5: Calculator Interface and Implementation

**Files:**
- Create: `src/Perpetuum/Robots/EquipmentSets/IEquipmentSetBonusCalculator.cs`
- Create: `src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs`

- [ ] **Step 1: Write the interface**

```csharp
using System.Collections.Generic;

namespace Perpetuum.Robots.EquipmentSets
{
    public interface IEquipmentSetBonusCalculator
    {
        EquipmentSetBonusResult Compute(IEnumerable<int> fittedDefinitions);
    }
}
```

- [ ] **Step 2: Write the calculator**

```csharp
using Perpetuum.Items;
using System.Collections.Generic;
using System.Linq;

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
            var piecesPerSet = new Dictionary<int, int>();

            foreach (int definition in fittedDefinitions)
            {
                foreach (int setId in _repository.GetSetIdsForDefinition(definition))
                {
                    piecesPerSet.TryGetValue(setId, out int count);
                    piecesPerSet[setId] = count + 1;
                }
            }

            if (piecesPerSet.Count == 0)
                return EquipmentSetBonusResult.Empty;

            var modifiers = new List<ItemPropertyModifier>();
            var activeSetIds = new HashSet<int>();

            foreach (KeyValuePair<int, int> entry in piecesPerSet)
            {
                int setId = entry.Key;
                int pieceCount = entry.Value;
                bool anyMet = false;

                foreach (SetBonusThreshold threshold in _repository.GetThresholds(setId)
                    .Where(t => t.RequiredPieces <= pieceCount))
                {
                    modifiers.Add(ItemPropertyModifier.Create(threshold.Field, threshold.Value));
                    anyMet = true;
                }

                if (anyMet)
                    activeSetIds.Add(setId);
            }

            return new EquipmentSetBonusResult(modifiers, activeSetIds);
        }
    }
}
```

- [ ] **Step 3: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Robots/EquipmentSets/IEquipmentSetBonusCalculator.cs
git add src/Perpetuum/Robots/EquipmentSets/EquipmentSetBonusCalculator.cs
git commit -m "feat: implement EquipmentSetBonusCalculator (IMPROVEMENT-025)"
```

---

## Task 6: Robot — Property Injection + Initialize + GetPropertyModifier

**Files:**
- Modify: `src/Perpetuum/Robots/Robot.cs`
- Modify: `src/Perpetuum/Robots/Robot.Properties.cs`

- [ ] **Step 1: Add property and backing fields to `Robot.cs`**

In `src/Perpetuum/Robots/Robot.cs`, add the following imports at the top of the file (alongside existing usings):

```csharp
using Perpetuum.Robots.EquipmentSets;
using System.Collections.Generic;
using System.Collections.Immutable;
```

Then in the class body, after the existing `private readonly IntervalTimer overheatCooldownTimer;` field declaration (around line 30), add:

```csharp
        private IReadOnlyList<ItemPropertyModifier> _setBonusModifiers = Array.Empty<ItemPropertyModifier>();
        private IReadOnlySet<int> _activeSetIds = ImmutableHashSet<int>.Empty;
```

After the existing `public InsuranceHelper InsuranceHelper { protected get; set; }` property (around line 46), add:

```csharp
        public IEquipmentSetBonusCalculator EquipmentSetBonusCalculator { private get; set; }
```

- [ ] **Step 2: Override `Initialize()` to compute set bonuses**

In `src/Perpetuum/Robots/Robot.cs`, replace the existing `Initialize()` method (currently at line 123):

```csharp
        public override void Initialize()
        {
            InitComponents();
            base.Initialize();
        }
```

with:

```csharp
        public override void Initialize()
        {
            InitComponents();

            if (EquipmentSetBonusCalculator != null)
            {
                EquipmentSetBonusResult result = EquipmentSetBonusCalculator.Compute(
                    Modules.Select(m => m.Definition));
                _setBonusModifiers = result.Modifiers;
                _activeSetIds = result.ActiveSetIds;
            }

            base.Initialize();
        }
```

Add `using System.Linq;` to the top of `Robot.cs` if not already present.

- [ ] **Step 3: Override `GetPropertyModifier()` in `Robot.Properties.cs`**

In `src/Perpetuum/Robots/Robot.Properties.cs`, replace the existing `GetPropertyModifier(AggregateField field)` method (currently at line 124):

```csharp
        public override ItemPropertyModifier GetPropertyModifier(AggregateField field)
        {
            ItemPropertyModifier modifier = base.GetPropertyModifier(field);

            foreach (RobotComponent component in RobotComponents)
            {
                ItemPropertyModifier m = component.GetPropertyModifier(field);
                m.Modify(ref modifier);
            }

            return modifier;
        }
```

with:

```csharp
        public override ItemPropertyModifier GetPropertyModifier(AggregateField field)
        {
            ItemPropertyModifier modifier = base.GetPropertyModifier(field);

            foreach (RobotComponent component in RobotComponents)
            {
                ItemPropertyModifier m = component.GetPropertyModifier(field);
                m.Modify(ref modifier);
            }

            foreach (ItemPropertyModifier bonus in _setBonusModifiers)
            {
                if (bonus.Field == field)
                    bonus.Modify(ref modifier);
            }

            return modifier;
        }
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with no errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Robots/Robot.cs
git add src/Perpetuum/Robots/Robot.Properties.cs
git commit -m "feat: integrate set bonus modifiers into Robot aggregate pipeline (IMPROVEMENT-025)"
```

---

## Task 7: SetBonusEffectApplicator

**Files:**
- Create: `src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs`

- [ ] **Step 1: Write the applicator**

```csharp
using Perpetuum.ExportedTypes;
using Perpetuum.Zones.Effects;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Robots.EquipmentSets
{
    public sealed class SetBonusEffectApplicator
    {
        private readonly Dictionary<int, EffectToken> _activeTokens = new();

        public void Update(Robot robot, IReadOnlySet<int> activeSetIds)
        {
            List<int> toRemove = _activeTokens.Keys
                .Where(id => !activeSetIds.Contains(id))
                .ToList();

            foreach (int setId in toRemove)
            {
                robot.EffectHandler.RemoveEffectByToken(_activeTokens[setId]);
                _activeTokens.Remove(setId);
            }

            foreach (int setId in activeSetIds)
            {
                if (_activeTokens.ContainsKey(setId))
                    continue;

                EffectToken token = EffectToken.NewToken();
                EffectBuilder builder = robot.NewEffectBuilder()
                    .SetType(EffectType.effect_equipment_set_bonus)
                    .EnableModifiers(false)
                    .WithToken(token);
                robot.ApplyEffect(builder);
                _activeTokens[setId] = token;
            }
        }

        public void RemoveAll(Robot robot)
        {
            foreach (EffectToken token in _activeTokens.Values)
            {
                robot.EffectHandler.RemoveEffectByToken(token);
            }
            _activeTokens.Clear();
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum/Robots/EquipmentSets/SetBonusEffectApplicator.cs
git commit -m "feat: add SetBonusEffectApplicator for zone-side set bonus display (IMPROVEMENT-025)"
```

---

## Task 8: Wire SetBonusEffectApplicator into Robot.OnUpdate

**Files:**
- Modify: `src/Perpetuum/Robots/Robot.cs`

- [ ] **Step 1: Add applicator field**

In `src/Perpetuum/Robots/Robot.cs`, after the `_activeSetIds` field added in Task 6, add:

```csharp
        private readonly SetBonusEffectApplicator _setBonusEffectApplicator = new SetBonusEffectApplicator();
```

- [ ] **Step 2: Call applicator in `OnUpdate`**

In `src/Perpetuum/Robots/Robot.cs`, find `OnUpdate(TimeSpan time)` (currently at line 342). After `foreach (RobotComponent robotComponent in RobotComponents) { robotComponent.Update(time); }`, add:

```csharp
            _setBonusEffectApplicator.Update(this, _activeSetIds);
```

The updated method should look like:

```csharp
        protected override void OnUpdate(TimeSpan time)
        {
            base.OnUpdate(time);

            lockHandler.Update(time);

            foreach (RobotComponent robotComponent in RobotComponents)
            {
                robotComponent.Update(time);
            }

            _setBonusEffectApplicator.Update(this, _activeSetIds);

            if (overheatCooldownTimer.Passed)
            {
                OverheatHandler.Decrease(HeatDissipation);
                ResetTimer();
            }

            overheatCooldownTimer.Update(time);
        }
```

- [ ] **Step 3: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Robots/Robot.cs
git commit -m "feat: wire SetBonusEffectApplicator into Robot.OnUpdate (IMPROVEMENT-025)"
```

---

## Task 9: DI Registration

**Files:**
- Modify: `src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs`

- [ ] **Step 1: Add using directive**

In `src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs`, add to the using directives block:

```csharp
using Perpetuum.Robots.EquipmentSets;
```

- [ ] **Step 2: Register the repository and calculator**

In `EntitiesModule.Load()`, after the line:
```csharp
builder.RegisterType<ModulePropertyModifiersReader>().OnActivated(e => e.Instance.Init()).SingleInstance();
```

add:

```csharp
            builder.RegisterType<EquipmentSetRepository>().As<IEquipmentSetRepository>()
                .OnActivated(e => e.Instance.Init()).SingleInstance();
            builder.RegisterType<EquipmentSetBonusCalculator>().As<IEquipmentSetBonusCalculator>()
                .SingleInstance();
```

- [ ] **Step 3: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 4: Start the server and verify no startup crash**

```
cd src/Perpetuum.Server
dotnet run -- --GameRoot "E:\PerpetuumServer2\data"
```

Expected: server starts without exceptions. Check logs for any DI resolution errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs
git commit -m "feat: register EquipmentSetRepository and Calculator in DI (IMPROVEMENT-025)"
```

---

## Task 10: Pilot Set Content SQL

**Files:**
- Create: `docs/Patches/p36.2/Features/EquipmentSets/set_striker_pilot.sql`

- [ ] **Step 1: Query for valid module definitions to use**

Run this against the game DB to find medium armor plate definitions to include in the pilot set:

```sql
SELECT definition, definitionname
FROM entitydefaults
WHERE definitionname LIKE N'def_%armor_plate%medium%'
   OR definitionname LIKE N'def_medium%armor_plate%'
AND enabled = 1
ORDER BY definitionname;
```

Pick 4 definitions from the results. Note their `definitionname` values.

- [ ] **Step 2: Write the pilot set SQL using the names found above**

Replace `'def_module_name_1'` through `'def_module_name_4'` with the actual `definitionname` values found in Step 1.

```sql
-- Pilot set: set_striker (IMPROVEMENT-025 validation content)
-- AggregateField values used:
--   armor_max_modifier  = 17  (formula: Modifier, 1.05 = +5%)
--   resist_kinetic_modifier = 310  (formula: Modifier, 1.08 = +8%)

-- 1. Create the set
IF NOT EXISTS (SELECT 1 FROM equipment_sets WHERE name = N'set_striker')
    INSERT INTO equipment_sets (name) VALUES (N'set_striker');

DECLARE @setId INT = (SELECT set_id FROM equipment_sets WHERE name = N'set_striker');

-- 2. Assign member modules (replace names with actual definitionname values from the DB)
INSERT INTO equipment_set_members (set_id, definition)
SELECT @setId, ed.definition
FROM entitydefaults ed
WHERE ed.definitionname IN (
    N'def_module_name_1',
    N'def_module_name_2',
    N'def_module_name_3',
    N'def_module_name_4'
)
AND NOT EXISTS (
    SELECT 1 FROM equipment_set_members m
    WHERE m.set_id = @setId AND m.definition = ed.definition
);

-- 3. Define thresholds
-- 2-piece: +5% max armor (armor_max_modifier = 17, value = 1.05, Modifier formula)
IF NOT EXISTS (
    SELECT 1 FROM equipment_set_bonus_thresholds
    WHERE set_id = @setId AND required_pieces = 2 AND aggregate_field = 17)
    INSERT INTO equipment_set_bonus_thresholds (set_id, required_pieces, aggregate_field, bonus_value)
    VALUES (@setId, 2, 17, 1.05);

-- 4-piece: +8% kinetic resist (resist_kinetic_modifier = 310, value = 1.08, Modifier formula)
IF NOT EXISTS (
    SELECT 1 FROM equipment_set_bonus_thresholds
    WHERE set_id = @setId AND required_pieces = 4 AND aggregate_field = 310)
    INSERT INTO equipment_set_bonus_thresholds (set_id, required_pieces, aggregate_field, bonus_value)
    VALUES (@setId, 4, 310, 1.08);
```

- [ ] **Step 3: Execute the pilot set SQL**

Run `docs/Patches/p36.2/Features/EquipmentSets/set_striker_pilot.sql` against the game DB.

Verify:
```sql
SELECT s.name, m.definition, ed.definitionname
FROM equipment_sets s
JOIN equipment_set_members m ON s.set_id = m.set_id
JOIN entitydefaults ed ON m.definition = ed.definition
WHERE s.name = N'set_striker';

SELECT s.name, t.required_pieces, t.aggregate_field, t.bonus_value
FROM equipment_sets s
JOIN equipment_set_bonus_thresholds t ON s.set_id = t.set_id
WHERE s.name = N'set_striker'
ORDER BY t.required_pieces;
```

Expected: 4 member rows, 2 threshold rows (pieces=2 field=17 value=1.05, pieces=4 field=310 value=1.08).

- [ ] **Step 4: Commit**

```
git add docs/Patches/p36.2/Features/EquipmentSets/set_striker_pilot.sql
git commit -m "feat: add set_striker pilot set content SQL (IMPROVEMENT-025)"
```

---

## Task 11: Manual Validation

**Prerequisites:** Server running with the pilot set SQL applied and the updated binary deployed.

- [ ] **Step 1: Restart the server**

Restart to trigger `EquipmentSetRepository.Init()` and reload the `effects` cache.
Confirm in logs: no startup exceptions.

- [ ] **Step 2: Fitting screen — no bonus with 1 piece**

1. Dock a character with a robot.
2. Equip exactly 1 of the 4 pilot set modules.
3. Open the fitting screen and note `armor_max` and `resist_kinetic` values.
4. Expected: no change from baseline values — no threshold is met with 1 piece.

- [ ] **Step 3: Fitting screen — 2-piece bonus visible**

1. Equip a second pilot set module into the same robot (any of the 4 members).
2. Note `armor_max` in the fitting screen.
3. Expected: `armor_max` is approximately 5% higher than the 1-piece value (modifier 1.05 applied).
4. `resist_kinetic` should be unchanged (4-piece threshold not yet met).

- [ ] **Step 4: Fitting screen — 4-piece bonus visible**

1. Equip a third and fourth pilot set module.
2. Note both `armor_max` and `resist_kinetic` in the fitting screen.
3. Expected: `armor_max` +5%, `resist_kinetic` +8% vs. baseline (both thresholds active).

- [ ] **Step 5: Fitting screen — bonus removed on unequip**

1. Unequip one module to drop to 3 pieces.
2. Expected: `resist_kinetic` returns to baseline (4-piece threshold lost), `armor_max` remains +5%.
3. Unequip another to drop to 1 piece.
4. Expected: both values return to baseline.

- [ ] **Step 6: Duplicate pieces count**

1. Fit 2 copies of the same set member module (same `definitionname`, stacked).
2. Expected: 2-piece threshold is met with 2 identical modules — `armor_max` +5%.

- [ ] **Step 7: Zone deployment — effect visible**

1. Deploy the robot to a zone with 2+ set pieces fitted.
2. Open the robot status UI and inspect the effect list.
3. Expected: "Set Bonus Active" effect is present.
4. Remove one set piece in-station, redeploy.
5. Expected: if threshold still met, effect present; if not, effect absent.

- [ ] **Step 8: Zone entry/re-entry idempotency**

1. Deploy robot with 4 set pieces — one "Set Bonus Active" effect should appear.
2. Undeploy and redeploy.
3. Expected: still one "Set Bonus Active" effect — no duplicates.

- [ ] **Step 9: Commit validation note**

```
git commit --allow-empty -m "chore: manual validation complete for IMPROVEMENT-025"
```

---

## Notes

- `AggregateField.armor_max_modifier = 17`, formula = Modifier (multiply). `bonus_value = 1.05` means ×1.05.
- `AggregateField.resist_kinetic_modifier = 310`, formula = Modifier. `bonus_value = 1.08` means ×1.08.
- `effectcategory = 0` for `effect_equipment_set_bonus` means the effect is not ECCM-clearable and has no stacking limit. Adjust if needed after playtesting.
- The `SetBonusEffectApplicator.Update()` call in `Robot.OnUpdate()` runs every zone tick — it is O(active sets) and only creates/removes effects on change, so the hot-path cost is negligible.
- To add more sets after this feature ships: insert into `equipment_sets`, `equipment_set_members`, and `equipment_set_bonus_thresholds` — no code changes needed.
