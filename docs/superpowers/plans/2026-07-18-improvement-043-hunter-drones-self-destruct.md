# IMPROVEMENT-043: Hunter Drones with Self-Destruct Module — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a target-agnostic `SelfDestructModule` (player kamikaze item) and autonomous `HunterDrone`/`HunterDroneAI` (PvE Niani-hunter / PvP standings-hunter) that share the same delayed-detonation mechanism.

**Architecture:** A new `SelfDestructCountdownEffect` (Effect subclass, driven entirely by the existing `EffectHandler.Update` tick chain — no `Task.Delay`, no changes to `Unit`/`Player`/`Robot` hot-path code) carries its own detonation config as property modifiers and calls back into a shared static `SelfDestructDetonation` helper on expiry. `SelfDestructModule` (player-equippable) and `HunterDroneAI`'s `SelfDestruct` state (drone-triggered) both call the same helper, so the detonation logic exists exactly once. `HunterDrone` extends `CombatDrone` but overrides its primary-lock-gated visibility so it detects targets independently, and its 4-state AI (`Patrol`/`Approach`/`SelfDestruct`/`Retreat`) follows the existing class-per-state `BaseAI`/`CombatDroneAI` stack-FSM idiom used by every other NPC/drone AI in the codebase.

**Tech Stack:** .NET 8 / C# 12, SQL Server, Autofac DI, the existing `Effect`/`EffectBuilder`/`EffectHandler` system, the existing `StackFSM`/`BaseAI` state-machine idiom.

**Spec:** `docs/superpowers/specs/2026-07-18-improvement-043-hunter-drones-self-destruct-design.md`

## Global Constraints

- No automated test suite exists in this repo (`docs/codebase/TESTING.md`) — every task ends in `dotnet build` + manual/in-game validation, not unit tests.
- Never hardcode definition/extension/effect/aggregate-field IDs — all content SQL below uses idempotent, name-keyed `IF NOT EXISTS`/`MERGE` patterns per `docs/content/claude_game_content_guide.md`. Any numeric ID shown (e.g. a new `EffectType` value) is a **proposed** value that must be verified free against the live DB before the migration is applied — flagged explicitly where it occurs.
- Preserve DB/network/serialization compatibility; all new columns/rows are additive.
- Zone-update hot path: no blocking waits, no `Task.Delay`-based countdowns (that's exactly the pattern being avoided — see Task 1), no LINQ scans of `zone.Units`/`zone.Beams` in AI `Update()` methods — use `IntervalTimer` + `GetVisibleUnits()` as established.

---

## File Map

| File | Change |
|---|---|
| `src/Perpetuum.ExportedTypes/EffectType.cs` | Add `effect_self_destruct_countdown` |
| `src/Perpetuum.ExportedTypes/AggregateField.cs` | Add 5 carrier fields (explosion radius + 4 damage types) |
| `src/Perpetuum/Zones/Effects/SelfDestructCountdownEffect.cs` | New — Effect subclass, detonates `OnRemoved()` |
| `src/Perpetuum/Zones/Effects/SelfDestructDetonation.cs` | New — shared static arm/detonate helper |
| `src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs` | Autofac keyed registration for the new effect type |
| `src/Perpetuum/Modules/SelfDestructModule.cs` | New — player-equippable head-slot module |
| `src/Perpetuum/Players/Player.cs` | Guard `ApplyInvulnerableEffect()` against active countdown |
| `src/Perpetuum/Zones/RemoteControl/TurretType.cs` | Add `HunterDrone` |
| `src/Perpetuum/Zones/RemoteControl/HunterDrone.cs` | New — extends `CombatDrone` |
| `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterDroneAI.cs` | New — base class (mirrors `CombatDroneAI`) |
| `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterPatrolAI.cs` | New |
| `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterApproachAI.cs` | New |
| `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterSelfDestructAI.cs` | New |
| `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterRetreatAI.cs` | New |
| `src/Perpetuum/Zones/NpcSystem/SmartCreature.cs` | `OnEnterZone` — add `HunterDrone` branch |
| `src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs` | New — PvE/PvP subclasses |
| `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` | New — content SQL |
| `docs/db_structure/database_schema_documentation.md` | Document new rows where applicable |

---

## Task 1: `SelfDestructCountdownEffect` — the un-cancellable, pause-on-zone-exit countdown

This is the foundational piece everything else calls into. It deliberately does **not** use `AreaBomb`'s `Task.Delay` pattern (see spec decision 8) — it's a plain `Effect` whose `Timer` is ticked by the already-existing `EffectHandler.Update(time)` call chain, which is itself hard-gated on `Unit.Update`'s `if (!InZone || States.Dead) return;` (`Unit.cs:248-256`). This gives "pauses while out of zone, doesn't reset" for free, with no changes to `Unit`, `Player`, or `Robot`.

**Files:**
- Modify: `src/Perpetuum.ExportedTypes/EffectType.cs`
- Modify: `src/Perpetuum.ExportedTypes/AggregateField.cs`
- Create: `src/Perpetuum/Zones/Effects/SelfDestructCountdownEffect.cs`
- Create: `src/Perpetuum/Zones/Effects/SelfDestructDetonation.cs`
- Modify: `src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs`

**Interfaces:**
- Produces: `SelfDestructDetonation.Arm(Unit owner, TimeSpan delay, double explosionRadius, double damageChemical, double damageExplosive, double damageKinetic, double damageThermal)` — called by Task 2 (`SelfDestructModule`) and Task 5 (`HunterSelfDestructAI`).
- Produces: `Unit.HasActiveSelfDestructCountdown` extension-style check (implemented as `EffectHandler.ContainsEffect(EffectType.effect_self_destruct_countdown)`) — consumed by Task 3.

- [ ] **Step 1: Add the new `EffectType` member**

In `src/Perpetuum.ExportedTypes/EffectType.cs`, add a new member after `effect_equipment_set_bonus = 139,` (the current highest value):

```csharp
        effect_equipment_set_bonus = 139,
        effect_self_destruct_countdown = 140,
```

> **DB verification required:** this file is generated from the `effects` DB table. Before applying the migration in Step 6, verify `140` is not already used by another `effects` row in the target database (`SELECT * FROM effects WHERE id = 140`). If taken, pick the next free integer and use it consistently in Step 1, Step 6's SQL, and everywhere else in this plan that references `effect_self_destruct_countdown`.

- [ ] **Step 2: Add 5 new `AggregateField` carrier members**

These exist solely to smuggle the module/drone's own per-entity-definition detonation config (explosion radius, 4 damage types) through the generic `Effect.PropertyModifiers` channel from arm-time to detonation-time — the same "property-modifier-as-signal" idiom already used by `AggregateField.drone_remote_command_translation_retreat` (`AggregateField.cs:512`). They are dedicated, brand-new fields specifically so they cannot collide with any existing consumer of `AggregateField.explosion_radius`/`damage_chemical` (which are live robot-stat modifiers used elsewhere).

In `src/Perpetuum.ExportedTypes/AggregateField.cs`, add after `drone_remote_command_translation_retreat_confirmation = 753,` (the current highest value in that block):

```csharp
        drone_remote_command_translation_retreat_confirmation = 753,
        self_destruct_config_explosion_radius = 754,
        self_destruct_config_damage_chemical = 755,
        self_destruct_config_damage_explosive = 756,
        self_destruct_config_damage_kinetic = 757,
        self_destruct_config_damage_thermal = 758,
```

> Same DB-verification caveat as Step 1 — confirm `754`-`758` are free in `aggregatefields` before applying Step 6's migration; adjust if taken.

- [ ] **Step 3: Create `SelfDestructCountdownEffect`**

Create `src/Perpetuum/Zones/Effects/SelfDestructCountdownEffect.cs`:

```csharp
using System.Linq;
using Perpetuum.ExportedTypes;

namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Countdown for the self-destruct module / hunter drone detonation. Ticks only while
    /// the owner is InZone (via the normal EffectHandler.Update chain), so it naturally
    /// pauses across a teleport's remove-from-zone/re-add gap instead of resetting.
    /// Nothing in the codebase removes effects by this token, so this is inherently
    /// un-cancellable once armed.
    /// </summary>
    public class SelfDestructCountdownEffect : Effect
    {
        protected override void OnRemoved()
        {
            base.OnRemoved();

            double explosionRadius = GetConfigValue(AggregateField.self_destruct_config_explosion_radius);
            double damageChemical = GetConfigValue(AggregateField.self_destruct_config_damage_chemical);
            double damageExplosive = GetConfigValue(AggregateField.self_destruct_config_damage_explosive);
            double damageKinetic = GetConfigValue(AggregateField.self_destruct_config_damage_kinetic);
            double damageThermal = GetConfigValue(AggregateField.self_destruct_config_damage_thermal);

            SelfDestructDetonation.Detonate(Owner, explosionRadius, damageChemical, damageExplosive, damageKinetic, damageThermal);
        }

        private double GetConfigValue(AggregateField field)
        {
            return PropertyModifiers.FirstOrDefault(m => m.Field == field)?.Value ?? 0.0;
        }
    }
}
```

- [ ] **Step 4: Create the shared `SelfDestructDetonation` helper**

Create `src/Perpetuum/Zones/Effects/SelfDestructDetonation.cs`. This is the single place both `SelfDestructModule` (Task 2) and `HunterSelfDestructAI` (Task 5) call into — `Arm` builds+applies the countdown effect, `Detonate` (called back by `SelfDestructCountdownEffect.OnRemoved`) does the AoE + kill.

```csharp
using System;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Units;

namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Shared arm/detonate logic for the self-destruct countdown, used by both the
    /// player-piloted SelfDestructModule and HunterDroneAI's SelfDestruct state, so the
    /// detonation behavior exists exactly once.
    /// </summary>
    public static class SelfDestructDetonation
    {
        public static bool IsArmed(Unit owner)
        {
            return owner.EffectHandler.ContainsEffect(EffectType.effect_self_destruct_countdown);
        }

        public static void Arm(
            Unit owner,
            TimeSpan delay,
            double explosionRadius,
            double damageChemical,
            double damageExplosive,
            double damageKinetic,
            double damageThermal)
        {
            if (IsArmed(owner))
            {
                return;
            }

            EffectBuilder effectBuilder = owner.NewEffectBuilder();
            _ = effectBuilder
                .SetType(EffectType.effect_self_destruct_countdown)
                .WithDuration(delay)
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_explosion_radius, AggregateFormula.Modifier, explosionRadius))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_chemical, AggregateFormula.Modifier, damageChemical))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_explosive, AggregateFormula.Modifier, damageExplosive))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_kinetic, AggregateFormula.Modifier, damageKinetic))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_thermal, AggregateFormula.Modifier, damageThermal));

            owner.ApplyPvPEffect();
            owner.ApplyEffect(effectBuilder);
        }

        public static void Detonate(Unit owner, double explosionRadius, double damageChemical, double damageExplosive, double damageKinetic, double damageThermal)
        {
            if (owner?.Zone == null)
            {
                return;
            }

            var damageBuilder = DamageInfo.Builder.WithAttacker(owner)
                .WithDamage(DamageType.Chemical, damageChemical)
                .WithDamage(DamageType.Explosive, damageExplosive)
                .WithDamage(DamageType.Kinetic, damageKinetic)
                .WithDamage(DamageType.Thermal, damageThermal)
                .WithOptimalRange(2)
                .WithExplosionRadius(explosionRadius);

            owner.Zone.DoAoeDamageAsync(damageBuilder);
            owner.Kill(owner);
        }
    }
}
```

> `owner.Kill(owner)` is called after `DoAoeDamageAsync` — per `ZoneExtensions.DoAoeDamage`, `RemoteControlledCreature` targets are always skipped by the AoE loop, so a `HunterDrone` owner never double-dips its own blast; a `Player` owner *is* eligible for their own AoE (same as standing in an `AreaBomb` blast) before `Kill()` runs. `Unit.OnDead` also fires its own separate small `DoExplosion()` wreck-beam — this is expected layered flavor (see spec decision + Task 2 manual validation), not a bug.

- [ ] **Step 5: Register the new effect type in Autofac**

In `src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs`, add a keyed registration next to the other single-purpose effect registrations (after line 23, `builder.RegisterType<InvulnerableEffect>().Keyed<Effect>(EffectType.effect_invulnerable);`):

```csharp
            _ = builder.RegisterType<InvulnerableEffect>().Keyed<Effect>(EffectType.effect_invulnerable);
            _ = builder.RegisterType<SelfDestructCountdownEffect>().Keyed<Effect>(EffectType.effect_self_destruct_countdown);
```

Add the `using Perpetuum.Zones.Effects;` import if not already present (it already is, per the existing file).

- [ ] **Step 6: Content SQL for the new effect type**

Create `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` (this task only adds the effect + aggregate field rows; entity definitions come in Task 7):

```sql
-- IMPROVEMENT-043: Hunter Drones with Self-Destruct Module
-- Part 1: effect + aggregate field rows for the self-destruct countdown.
-- All INSERTs are idempotent (IF NOT EXISTS guarded). Verify the hardcoded IDs below
-- are free in the target DB before applying (see plan Task 1, Steps 1-2).

IF NOT EXISTS (SELECT 1 FROM effectcategories WHERE name = 'effectcategory_self_destruct')
BEGIN
    INSERT INTO effectcategories (name)
    VALUES ('effectcategory_self_destruct');
END

IF NOT EXISTS (SELECT 1 FROM effects WHERE id = 140)
BEGIN
    INSERT INTO effects (id, name, effectcategory, duration, isaura, auraradius, ispositive, display, saveable)
    VALUES (
        140,
        'effect_self_destruct_countdown',
        (SELECT id FROM effectcategories WHERE name = 'effectcategory_self_destruct'),
        0,      -- duration is set per-activation via EffectBuilder.WithDuration(delay), not this default
        0,      -- isaura
        0,      -- auraradius
        0,      -- ispositive (it's a debuff/hazard, not a buff)
        1,      -- display: visible countdown to the owner/nearby players
        0       -- saveable: does not need to persist across server restarts
    );
END

IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 754)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (754, 'self_destruct_config_explosion_radius', 'Modifier', 'meter', 1);
END
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 755)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (755, 'self_destruct_config_damage_chemical', 'Modifier', 'point', 1);
END
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 756)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (756, 'self_destruct_config_damage_explosive', 'Modifier', 'point', 1);
END
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 757)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (757, 'self_destruct_config_damage_kinetic', 'Modifier', 'point', 1);
END
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 758)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (758, 'self_destruct_config_damage_thermal', 'Modifier', 'point', 1);
END
```

> The exact column set for `effects`/`aggregatefields` above is drawn from `docs/content/claude_game_content_guide.md` sections 5-6 ("Key fields" tables). Before running this migration, diff it against `docs/db_structure/database_schema_documentation.md`'s actual `effects`/`aggregatefields` column lists and adjust any mismatched column names — the guide documents the conceptual fields, not a guaranteed-exact column list.

- [ ] **Step 7: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors. (Nothing calls `SelfDestructDetonation.Arm` yet, so there's no runtime behavior to test until Task 2.)

- [ ] **Step 8: Commit**

```
git add src/Perpetuum.ExportedTypes/EffectType.cs src/Perpetuum.ExportedTypes/AggregateField.cs src/Perpetuum/Zones/Effects/SelfDestructCountdownEffect.cs src/Perpetuum/Zones/Effects/SelfDestructDetonation.cs src/Perpetuum.Bootstrapper/Modules/EffectsModule.cs docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql
git commit -m "feat(self-destruct): add un-cancellable, zone-gated countdown effect and detonation helper (IMPROVEMENT-043)"
```

---

## Task 2: `SelfDestructModule` — the player-equippable kamikaze module

**Files:**
- Create: `src/Perpetuum/Modules/SelfDestructModule.cs`
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` (append)

**Interfaces:**
- Consumes: `SelfDestructDetonation.Arm(Unit, TimeSpan, double, double, double, double, double)` from Task 1.
- Consumes: `AreaBomb`'s beam pattern (`zone.CreateBeam(BeamType.timebomb_activation, ...)`) for the activation visual.

- [ ] **Step 1: Create `SelfDestructModule`**

Create `src/Perpetuum/Modules/SelfDestructModule.cs`. Mirrors `ActiveModule`'s constructor pattern (non-ranged, no ammo category) and `AreaBomb.OnSummonSuccess`'s activation-beam style. Reads its own detonation config directly from `ED.Config`, exactly like `AreaBomb` reads `ED.Config.explosion_radius`/`ED.Config.damage_*`.

```csharp
using Perpetuum.Zones.Beams;
using Perpetuum.Zones.Effects;
using System;

namespace Perpetuum.Modules
{
    /// <summary>
    /// Kamikaze module: on activation, arms an un-cancellable countdown that detonates an
    /// AoE around the owner and kills the owner's own robot. No target lock is required —
    /// see IMPROVEMENT-043 design spec decisions 1-2.
    /// </summary>
    public class SelfDestructModule : ActiveModule
    {
        private const int BeamVisibility = 600;

        public SelfDestructModule() : base(false)
        {
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }

        protected override void OnAction()
        {
            if (ParentRobot?.Zone == null)
            {
                return;
            }

            if (SelfDestructDetonation.IsArmed(ParentRobot))
            {
                return;
            }

            ParentRobot.Zone.CreateBeam(BeamType.timebomb_activation, builder => builder
                .WithPosition(ParentRobot.CurrentPosition)
                .WithVisibility(BeamVisibility)
                .WithDuration(100));

            TimeSpan delay = TimeSpan.FromMilliseconds(ED.Config.ActionDelay);

            SelfDestructDetonation.Arm(
                ParentRobot,
                delay,
                ED.Config.explosion_radius ?? 0.0,
                ED.Config.damage_chemical ?? 0.0,
                ED.Config.damage_explosive ?? 0.0,
                ED.Config.damage_kinetic ?? 0.0,
                ED.Config.damage_thermal ?? 0.0);
        }
    }
}
```

> No cancel-path handling is needed here (spec decision 7) — per Task 1's research, the client's only module-cancel path (`ZoneSession.HandleModuleUse` sending `ModuleStateType.Idle`) pops the *module's own* FSM back to idle; it never calls `EffectHandler.RemoveEffectByToken`/`RemoveEffectsByType` on the countdown's token, so the armed countdown survives regardless of what the player does with the module afterward. The module returning to `Idle` after this one `OnAction()` cycle (normal `Oneshot` behavior) is expected and does not affect the countdown.

- [ ] **Step 2: Content SQL for the module + config values**

Append to `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql`:

```sql
-- Part 2: SelfDestructModule entity definition + tunable config.
-- Category/attribute flag values below reuse existing conventions (docs/content/claude_game_content_guide.md
-- sections 3-4, 7) — verify categoryflags/attributeflags bit values against a live DB before applying;
-- placeholders here use the documented naming pattern (cf_<group>, def_<name>), not literal bit values.

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module')
BEGIN
    INSERT INTO entitydefaults
    (definitionname, quantity, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
    ('def_standard_self_destruct_module', 1,
     (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
     '#moduleFlag=i909#tier=$tierlevel_t1',
     'Kamikaze self-destruct module: arms an un-cancellable delayed AoE detonation that kills the owner.',
     1, 100, 500, 0, 100, 'def_standard_self_destruct_module', 1, 'standard', 1);
END

-- definitionconfig rows: reuse the exact same config key names AreaBomb already uses
-- (item_work_range, explosion_radius, damage_chemical/explosive/kinetic/thermal, ActionDelay).
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_self_destruct_module' AND configkey = 'explosion_radius')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue)
    VALUES ('def_standard_self_destruct_module', 'explosion_radius', '15');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_self_destruct_module' AND configkey = 'damage_chemical')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue)
    VALUES ('def_standard_self_destruct_module', 'damage_chemical', '2000');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_self_destruct_module' AND configkey = 'damage_explosive')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue)
    VALUES ('def_standard_self_destruct_module', 'damage_explosive', '2000');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_self_destruct_module' AND configkey = 'damage_kinetic')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue)
    VALUES ('def_standard_self_destruct_module', 'damage_kinetic', '2000');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_self_destruct_module' AND configkey = 'damage_thermal')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue)
    VALUES ('def_standard_self_destruct_module', 'damage_thermal', '2000');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_self_destruct_module' AND configkey = 'ActionDelay')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue)
    VALUES ('def_standard_self_destruct_module', 'ActionDelay', '8000');
END

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_self_destruct_modules')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (
        (SELECT ISNULL(MAX(value), 0) + 1 FROM categoryFlags),
        'cf_self_destruct_modules', 'Self-destruct / kamikaze modules', 0, 0);
END
```

> Damage values (2000/type) and radius (15m) are starting balance numbers, not final tuning — flag for playtesting. `moduleFlag=i909` reuses the numbering convention shown in the guide's example (`#moduleFlag=i908#tier=...`); verify `i909` isn't already assigned to another module before applying. This migration must run **before** the `categoryFlags` insert is referenced by the `entitydefaults` insert above it — SQL Server evaluates the subquery at INSERT time, so either order both statements correctly (category insert first) or wrap the whole script in one transaction; recommend moving the `cf_self_destruct_modules` category block to the top of this section before applying.

- [ ] **Step 3: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Manual validation (requires migration applied + server running)**

1. Apply `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` against your dev DB (fix the category-insert ordering noted above first).
2. Equip `def_standard_self_destruct_module` on a test character's robot, activate it with no target locked.
3. Verify: activation beam appears, ~8s later an explosion-scale AoE fires around the player's position, the player's own robot dies (normal pod-risk death pipeline — `Character.GetHomeBaseOrCurrentBase()` docking flow triggers).
4. Verify docking is blocked immediately after activation (PvP flag applied) — attempt to dock before the countdown expires and confirm `ErrorCodes.CantDockThisState`.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Modules/SelfDestructModule.cs docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql
git commit -m "feat(self-destruct): add player-equippable SelfDestructModule (IMPROVEMENT-043)"
```

---

## Task 3: Suppress post-teleport invulnerability while armed

**Files:**
- Modify: `src/Perpetuum/Players/Player.cs`

**Interfaces:**
- Consumes: `SelfDestructDetonation.IsArmed(Unit)` from Task 1.

- [ ] **Step 1: Guard `ApplyInvulnerableEffect()`**

In `src/Perpetuum/Players/Player.cs`, replace the existing method (currently lines 277-283):

```csharp
        public void ApplyInvulnerableEffect()
        {
            RemoveInvulnerableEffect(); // Remove existing effect, set new
            EffectBuilder builder = NewEffectBuilder().SetType(EffectType.effect_invulnerable);
            _ = builder.WithDurationModifier(0.75); //Reduce span of syndicate protection
            ApplyEffect(builder);
        }
```

with:

```csharp
        public void ApplyInvulnerableEffect()
        {
            if (Perpetuum.Zones.Effects.SelfDestructDetonation.IsArmed(this))
            {
                return;
            }

            RemoveInvulnerableEffect(); // Remove existing effect, set new
            EffectBuilder builder = NewEffectBuilder().SetType(EffectType.effect_invulnerable);
            _ = builder.WithDurationModifier(0.75); //Reduce span of syndicate protection
            ApplyEffect(builder);
        }
```

This is a single guard at the sole canonical entry point that all three invuln call sites (`TeleportWithinZone.cs:43,65`, `Player.LoadPlayerAndAddToZone:605`, `ZoneSession.cs:306`) route through — no changes needed to any of those three files. A player who is docked can't be self-destruct-armed in the first place (docking is blocked by `HasPvpEffect` the moment the module arms — see Task 2), so this guard only ever actually suppresses the teleport-triggered and mid-zone-session-reentry calls, which is exactly the case the design requires.

- [ ] **Step 2: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Manual validation**

1. Activate the self-destruct module on a player standing near a stationary teleport column.
2. Use the teleport column before the countdown expires.
3. Verify: no invulnerability effect is applied on arrival at the destination, and the countdown timer's remaining duration is unaffected by the teleport transition (does not reset to full).
4. Separately, confirm normal (non-armed) teleport use still applies invulnerability as before — this guard must be a no-op in the overwhelmingly common case.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Players/Player.cs
git commit -m "fix(self-destruct): suppress post-teleport invulnerability while a self-destruct countdown is armed (IMPROVEMENT-043)"
```

---

## Task 4: `HunterDrone` — autonomous targeting, extends `CombatDrone`

`HunterDrone` extends `CombatDrone` (not `RemoteControlledCreature` directly) specifically to reuse its `IsHostile(...)` double-dispatch overloads and `OnAggression` relay — see Task-1-research finding that `CombatDrone.IsDetected`/`UpdateUnitVisibility` gate visibility on `CommandRobot.GetPrimaryLock()`, which `HunterDrone` must override away since it hunts independently of the owner's lock.

**Files:**
- Modify: `src/Perpetuum/Zones/RemoteControl/TurretType.cs`
- Create: `src/Perpetuum/Zones/RemoteControl/HunterDrone.cs`

**Interfaces:**
- Produces: `HunterDrone.TargetFaction` (`Faction?`, settable), `HunterDrone.FindTarget()` (`Unit?`) — consumed by Task 5's AI states.
- Produces: `HunterDrone.IsHostilePlayer(Player)` — inherited `protected` from `RemoteControlledCreature`, now reachable without a command-robot lock.

- [ ] **Step 1: Add `TurretType.HunterDrone`**

Replace `src/Perpetuum/Zones/RemoteControl/TurretType.cs`:

```csharp
namespace Perpetuum.Zones.RemoteControl
{
    public enum TurretType
    {
        Sentry,
        Mining,
        Harvesting,
        CombatDrone,
        IndustrialDrone,
        SupportDrone,
        HunterDrone,
    }
}
```

- [ ] **Step 2: Create `HunterDrone`**

Create `src/Perpetuum/Zones/RemoteControl/HunterDrone.cs`. Note `TargetFaction` is `null` for the PvP variant (checks `IsHostilePlayer`) and `Faction.Niani` for the PvE variant (checks `Npc.Faction`). Detection radius is `item_work_range` (a separate `ModuleProperty`-sourced value set at spawn by the controller, exposed here as a plain settable property since `HunterDrone` itself, not a module, needs to read it every scan).

```csharp
using System.Linq;
using Perpetuum.Players;
using Perpetuum.Services.Standing;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.NpcSystem;

namespace Perpetuum.Zones.RemoteControl
{
    /// <summary>
    /// Autonomous kamikaze drone. Unlike CombatDrone, it does not require the command
    /// robot's target lock — it scans independently for PvE (Niani NPC) or PvP
    /// (hostile-standing player) targets. See IMPROVEMENT-043 design spec.
    /// </summary>
    public class HunterDrone : CombatDrone
    {
        private static readonly IntervalTimer ScanInterval = new IntervalTimer(1000, random: true);

        public Faction? TargetFaction { get; set; }

        public double DetectionRange { get; set; }

        public HunterDrone(IStandingHandler standingHandler) : base(standingHandler)
        {
        }

        public Unit FindTarget()
        {
            return GetVisibleUnits()
                .Select(v => v.Target)
                .Where(IsQualifyingTarget)
                .OrderBy(u => u.CurrentPosition.TotalDistance3D(CurrentPosition))
                .FirstOrDefault();
        }

        protected override bool IsDetected(Unit target)
        {
            // Deliberately does NOT call base.IsDetected(target): HunterDrone : CombatDrone,
            // and CombatDrone.IsDetected additionally requires CommandRobot.GetPrimaryLock()
            // to match target (CombatDrone.cs) — exactly the gate this drone must not have.
            // This re-implements Unit's own default stealth-vs-detection range formula
            // (Unit.cs ~107-119: 100 / Math.Max(1, target.StealthStrength) * Math.Max(1, DetectionStrength))
            // combined with this drone's own DetectionRange, instead of routing through CombatDrone.
            double detectionFormulaRange = 100 / System.Math.Max(1, target.StealthStrength) * System.Math.Max(1, DetectionStrength);
            double effectiveRange = System.Math.Min(DetectionRange, detectionFormulaRange);

            return CurrentPosition.IsInRangeOf2D(target.CurrentPosition, effectiveRange);
        }

        protected override void UpdateUnitVisibility(Unit target)
        {
            if (target is Npc or Player)
            {
                UpdateVisibility(target);
            }
        }

        private bool IsQualifyingTarget(Unit unit)
        {
            if (TargetFaction != null)
            {
                return unit is Npc npc && npc.Faction == TargetFaction;
            }

            return unit is Player player && IsHostilePlayer(player);
        }
    }
}
```

> Before implementing, confirm the exact property names `StealthStrength` and `DetectionStrength` (and their exact formula, `100 / Math.Max(1, target.StealthStrength) * Math.Max(1, DetectionStrength)`) by reading `Unit.IsDetected` directly (`src/Perpetuum/Units/Unit.Visibility.cs`, ~lines 107-119) — the formula above is drawn from research-agent paraphrase, not a verbatim quote, so re-verify it word-for-word before relying on it. The `ScanInterval` `IntervalTimer` field declared above is unused in this file — remove it here; the actual scan throttling belongs in `HunterPatrolAI`/`HunterApproachAI` (Task 5), not in `HunterDrone` itself, since `FindTarget()` should be a cheap on-demand query the AI calls at its own throttled cadence, not something `HunterDrone` self-throttles.

- [ ] **Step 3: Wire initial AI selection**

This is deferred to Task 5 Step 5, since it requires `HunterPatrolAI` to exist first (`SmartCreature.OnEnterZone`'s if-chain needs a concrete class to push).

- [ ] **Step 4: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build errors are possible here since `HunterDrone` has no AI pushed yet (it will sit in whatever `SmartCreature.OnEnterZone`'s `else` branch assigns, likely `IdleAI`, until Task 5 wires it) — that's fine, this task only needs to compile.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Zones/RemoteControl/TurretType.cs src/Perpetuum/Zones/RemoteControl/HunterDrone.cs
git commit -m "feat(hunter-drone): add HunterDrone with lock-independent target scanning (IMPROVEMENT-043)"
```

---

## Task 5: `HunterDroneAI` — Patrol / Approach / SelfDestruct / Retreat

Follows the exact class-per-state stack-FSM idiom used by `CombatDroneAI`/`GuardCombatDroneAI`/`RetreatCombatDroneAI` (Task-1-research: `src/Perpetuum/Zones/NpcSystem/AI/CombatDrones/`). `HunterPatrolAI` mirrors `GuardCombatDroneAI`'s `RandomMovement` pattern; `HunterApproachAI`/`HunterSelfDestructAI` mirror `RetreatCombatDroneAI`'s `AStarFinder`/`PathMovement` pattern (the only drone A* pathing code fully verified during research).

**Files:**
- Create: `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterDroneAI.cs`
- Create: `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterPatrolAI.cs`
- Create: `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterApproachAI.cs`
- Create: `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterSelfDestructAI.cs`
- Create: `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterRetreatAI.cs`
- Modify: `src/Perpetuum/Zones/NpcSystem/SmartCreature.cs`

**Interfaces:**
- Consumes: `HunterDrone.FindTarget()`, `HunterDrone.TargetFaction` (Task 4); `SelfDestructDetonation.Arm`/`IsArmed` (Task 1); `RemoteControlledCreature.IsReceivedRetreatCommand`, `CombatDrone.GuardRange`/`IsInGuardRange`, `RemoteControlledCreature.Scoop()` (existing).

- [ ] **Step 1: `HunterDroneAI` base class**

Create `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterDroneAI.cs` — mirrors `CombatDroneAI`'s role as the shared base with `To*AI()` transition helpers:

```csharp
using Perpetuum.Zones.RemoteControl;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public abstract class HunterDroneAI : BaseAI
    {
        protected HunterDroneAI(SmartCreature smartCreature) : base(smartCreature)
        {
        }

        protected HunterDrone Drone => smartCreature as HunterDrone;

        protected void ToHunterPatrolAI()
        {
            smartCreature.AI.Push(new HunterPatrolAI(smartCreature));
        }

        protected void ToHunterApproachAI(Unit target)
        {
            smartCreature.AI.Push(new HunterApproachAI(smartCreature, target));
        }

        protected void ToHunterSelfDestructAI(Unit target)
        {
            smartCreature.AI.Push(new HunterSelfDestructAI(smartCreature, target));
        }

        protected void ToHunterRetreatAI()
        {
            smartCreature.AI.Push(new HunterRetreatAI(smartCreature));
        }
    }
}
```

> Add `using Perpetuum.Units;` for the `Unit target` parameter type if not already implied by another using directive — verify during implementation against the actual `Unit` namespace already confirmed as `Perpetuum.Units` from Task 1/4 research.

- [ ] **Step 2: `HunterPatrolAI`**

Create `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterPatrolAI.cs` — directly modeled on `GuardCombatDroneAI` (Task-1-research, full file quoted), replacing `drone.HasCommandBotPrimaryLock()` with `drone.FindTarget()`:

```csharp
using System;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Movements;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterPatrolAI : HunterDroneAI
    {
        private readonly IntervalTimer scanTimer = new IntervalTimer(1000, random: true);
        private RandomMovement movement;

        public HunterPatrolAI(SmartCreature smartCreature) : base(smartCreature)
        {
        }

        public override void Enter()
        {
            smartCreature.StopAllModules();
            smartCreature.ResetLocks();

            movement = new RandomMovement(smartCreature.HomePosition, Drone.HomeRange);
            movement.Start(smartCreature);

            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (Drone.IsReceivedRetreatCommand)
            {
                ToHunterRetreatAI();
                return;
            }

            scanTimer.Update(time);
            if (scanTimer.Passed)
            {
                scanTimer.Reset();

                Unit target = Drone.FindTarget();
                if (target != null)
                {
                    ToHunterApproachAI(target);
                    return;
                }
            }

            movement?.Update(smartCreature, time);
        }
    }
}
```

- [ ] **Step 3: `HunterApproachAI`**

Create `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterApproachAI.cs`. Pathing structure follows `RetreatCombatDroneAI`'s `AStarFinder`/`PathMovement` pattern exactly (Task-1-research, full file quoted), targeting the hunted unit's position instead of home. Trigger range for entering `SelfDestruct` is 2 tiles per the design spec.

```csharp
using System;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Movements;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterApproachAI : HunterDroneAI
    {
        private const double TriggerRange = 2;

        private readonly Unit target;
        private readonly PathFinder pathFinder;
        private readonly IntervalTimer repathTimer = new IntervalTimer(2000);
        private PathMovement movement;

        public HunterApproachAI(SmartCreature smartCreature, Unit target) : base(smartCreature)
        {
            this.target = target;
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            RepathToTarget();
            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (Drone.IsReceivedRetreatCommand)
            {
                ToHunterRetreatAI();
                return;
            }

            if (target == null || target.IsDead() || !target.InZone)
            {
                ToHunterPatrolAI();
                return;
            }

            if (smartCreature.CurrentPosition.IsInRangeOf2D(target.CurrentPosition, TriggerRange))
            {
                ToHunterSelfDestructAI(target);
                return;
            }

            repathTimer.Update(time);
            if (repathTimer.Passed)
            {
                repathTimer.Reset();
                RepathToTarget();
            }

            movement?.Update(smartCreature, time);
        }

        private void RepathToTarget()
        {
            pathFinder
                .FindPathAsync(smartCreature.CurrentPosition, target.CurrentPosition)
                .ContinueWith(t =>
                {
                    System.Drawing.Point[] path = t.Result;
                    if (path == null)
                    {
                        return;
                    }

                    movement = new PathMovement(path);
                    movement.Start(smartCreature);
                });
        }
    }
}
```

> `target.IsDead()` — verify the exact dead-check API against `Unit`/`RemoteControlledCreature` during implementation (the research surfaced `States.Dead` as the underlying field on `Unit`; use `target.States.Dead` if `IsDead()` is not an existing extension method).

- [ ] **Step 4: `HunterSelfDestructAI`**

Create `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterSelfDestructAI.cs`. This is where spec decision 12 (actively chase to stay within 50m for the whole countdown) is implemented — it keeps re-pathing toward the target exactly like `HunterApproachAI`, the only difference being it has already armed the countdown and does not transition back to `Approach`/`Patrol` even if the target is lost (per spec: detonation still fires wherever the drone ends up).

```csharp
using System;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Effects;
using Perpetuum.Zones.Movements;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterSelfDestructAI : HunterDroneAI
    {
        private const double LeashRange = 50;

        private readonly Unit target;
        private readonly PathFinder pathFinder;
        private readonly IntervalTimer repathTimer = new IntervalTimer(2000);
        private PathMovement movement;

        public HunterSelfDestructAI(SmartCreature smartCreature, Unit target) : base(smartCreature)
        {
            this.target = target;
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            SelfDestructDetonation.Arm(
                smartCreature,
                TimeSpan.FromMilliseconds(Drone.ED.Config.ActionDelay),
                Drone.ED.Config.explosion_radius ?? 0.0,
                Drone.ED.Config.damage_chemical ?? 0.0,
                Drone.ED.Config.damage_explosive ?? 0.0,
                Drone.ED.Config.damage_kinetic ?? 0.0,
                Drone.ED.Config.damage_thermal ?? 0.0);

            RepathToTarget();
            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (target == null || target.IsDead() || !target.InZone)
            {
                movement?.Update(smartCreature, time);
                return;
            }

            if (!smartCreature.CurrentPosition.IsInRangeOf2D(target.CurrentPosition, LeashRange))
            {
                repathTimer.Update(time);
                if (repathTimer.Passed)
                {
                    repathTimer.Reset();
                    RepathToTarget();
                }
            }

            movement?.Update(smartCreature, time);
        }

        private void RepathToTarget()
        {
            if (target == null)
            {
                return;
            }

            pathFinder
                .FindPathAsync(smartCreature.CurrentPosition, target.CurrentPosition)
                .ContinueWith(t =>
                {
                    System.Drawing.Point[] path = t.Result;
                    if (path == null)
                    {
                        return;
                    }

                    movement = new PathMovement(path);
                    movement.Start(smartCreature);
                });
        }
    }
}
```

> No `IsReceivedRetreatCommand` check here — per spec risk note, retreat is only honored from `Approach`, guarded by entering `SelfDestruct` only via `HunterApproachAI`'s trigger-range check. Once armed, retreat commands are intentionally ignored (the countdown is un-cancellable, matching the module's behavior). `Drone.ED` — verify `HunterDrone` (via `CombatDrone`→`RemoteControlledCreature`→...→`Entity`) exposes `ED` the same way `Module`/`Egg` do (confirmed pattern from `AreaBomb.ED.Config`); this should hold since `ED`/`EntityDefault` access is a base `Entity` concern, not module-specific.

- [ ] **Step 5: `HunterRetreatAI`**

Create `src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterRetreatAI.cs` — directly modeled on `RetreatCombatDroneAI` (Task-1-research, full file quoted), swapping `CombatDrone`-specific calls for `HunterDrone`'s equivalents (`GuardRange`/`IsInGuardRange` are inherited unchanged from `CombatDrone`):

```csharp
using System;
using Perpetuum.PathFinders;
using Perpetuum.Zones.Movements;
using Perpetuum.Zones.RemoteControl;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterRetreatAI : HunterDroneAI
    {
        private PathMovement movement;
        private readonly PathFinder pathFinder;

        public HunterRetreatAI(SmartCreature smartCreature) : base(smartCreature)
        {
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            smartCreature.StopAllModules();
            smartCreature.ResetLocks();

            Position randomHome = smartCreature.Zone.FindPassablePointInRadius(smartCreature.HomePosition, (int)(smartCreature as CombatDrone).GuardRange);
            if (randomHome == default)
            {
                randomHome = smartCreature.HomePosition;
            }

            pathFinder
                .FindPathAsync(smartCreature.CurrentPosition, randomHome)
                .ContinueWith(t =>
                {
                    System.Drawing.Point[] path = t.Result;
                    if (path == null)
                    {
                        path = new AStarFinder(Heuristic.Manhattan, (x, y) => true)
                            .FindPath(smartCreature.CurrentPosition, smartCreature.HomePosition);
                    }

                    movement = new PathMovement(path);
                    movement.Start(smartCreature);
                });

            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (!Drone.IsReceivedRetreatCommand)
            {
                ToHunterPatrolAI();
                return;
            }

            if (movement != null)
            {
                movement.Update(smartCreature, time);

                if (movement.Arrived)
                {
                    if (!(smartCreature as CombatDrone).IsInGuardRange)
                    {
                        ToHunterRetreatAI();
                        return;
                    }

                    Drone.Scoop();
                }
            }
        }
    }
}
```

- [ ] **Step 6: Wire initial AI selection in `SmartCreature.OnEnterZone`**

In `src/Perpetuum/Zones/NpcSystem/SmartCreature.cs`, find `OnEnterZone` (the `if`-chain that selects initial AI based on runtime type). Add a `HunterDrone` branch **before** the existing `CombatDrone or SupportDrone` check (since `HunterDrone` *is* a `CombatDrone` via inheritance — the more specific type must be checked first or it will incorrectly match the `CombatDrone or SupportDrone` branch and get `GuardCombatDroneAI` instead):

```csharp
    if (this is HunterDrone)
    {
        AI.Push(new HunterDrones.HunterPatrolAI(this));
    }
    else if (this is CombatDrone or SupportDrone)
    {
        AI.Push(new GuardCombatDroneAI(this));
    }
```

Add `using Perpetuum.Zones.NpcSystem.AI.HunterDrones;` and `using Perpetuum.Zones.RemoteControl;` (for `HunterDrone`) to the top of `SmartCreature.cs` if not already present — verify against the file's existing usings during implementation (`RemoteControlledCreature`/`CombatDrone` are already referenced in this file per Task-1-research's `OnEnterZone` excerpt, so `Perpetuum.Zones.RemoteControl` is likely already imported).

- [ ] **Step 7: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors. Resolve any namespace/using mismatches flagged in Steps 1-6 above against actual file contents at this point.

- [ ] **Step 8: Commit**

```
git add src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/ src/Perpetuum/Zones/NpcSystem/SmartCreature.cs
git commit -m "feat(hunter-drone): add 4-state HunterDroneAI (Patrol/Approach/SelfDestruct/Retreat) (IMPROVEMENT-043)"
```

---

## Task 6: `HunterRemoteControllerModule` — PvE / PvP spawner subclasses

**Files:**
- Create: `src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs`

**Interfaces:**
- Consumes: `RemoteControllerModule.CreateAndConfigureRcu(RemoteControlledUnit)` (base, overridden), `TurretType.HunterDrone` (Task 4), `HunterDrone` constructor `(IStandingHandler)` (Task 4).

- [ ] **Step 1: Create `HunterRemoteControllerModule` base + PvE/PvP subclasses**

Create `src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs`. Modeled directly on `AssaultRemoteControllerModule` (Task-1-research, full file quoted) — same `CreateAndConfigureRcu` override shape, branching on the new `TurretType.HunterDrone`. `TargetFaction`/`DetectionRange` are drone-specific fields set post-creation, mirroring how `AssaultRemoteControllerModule` sets `GuardRange = 5` on the freshly-created `CombatDrone`.

```csharp
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Modules.ModuleProperties;
using Perpetuum.Zones.NpcSystem;
using Perpetuum.Zones.RemoteControl;

namespace Perpetuum.Modules
{
    public abstract class HunterRemoteControllerModule : RemoteControllerModule
    {
        private readonly ModuleProperty detectionRange;

        protected HunterRemoteControllerModule(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
            detectionRange = new ModuleProperty(this, AggregateField.item_work_range);
            AddProperty(detectionRange);
        }

        protected abstract Faction? GetTargetFaction();

        public override RemoteControlledCreature CreateAndConfigureRcu(RemoteControlledUnit ammo)
        {
            RemoteControlledCreature remoteControlledCreature = null;

            if (ammo.ED.Options.TurretType == TurretType.HunterDrone)
            {
                var hunterDrone = (HunterDrone)Factory.CreateWithRandomEID(ammo.ED.Options.TurretId);
                hunterDrone.Behavior = Behavior.Create(BehaviorType.RemoteControlledDrone);
                hunterDrone.GuardRange = 5;
                hunterDrone.TargetFaction = GetTargetFaction();
                hunterDrone.DetectionRange = detectionRange.Value;
                remoteControlledCreature = hunterDrone;
            }
            else
            {
                _ = PerpetuumException.Create(ErrorCodes.InvalidAmmoDefinition);
            }

            return remoteControlledCreature;
        }
    }

    public class HunterRemoteControllerModulePvE : HunterRemoteControllerModule
    {
        public HunterRemoteControllerModulePvE(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
        }

        protected override Faction? GetTargetFaction() => Faction.Niani;
    }

    public class HunterRemoteControllerModulePvP : HunterRemoteControllerModule
    {
        public HunterRemoteControllerModulePvP(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
        }

        protected override Faction? GetTargetFaction() => null;
    }
}
```

> `AcceptVisitor`/`TryAcceptVisitor` boilerplate present on every other module in this namespace (`RemoteControllerModule`, `AssaultRemoteControllerModule`) is omitted here deliberately: verify during implementation whether `RemoteControllerModule`'s own `AcceptVisitor` override is sufficient (it likely is, since these subclasses don't need their own visitor dispatch entry — `AssaultRemoteControllerModule` doesn't define one either per the quoted file). `SelfDestructModule` attachment to the drone's head slot (mentioned in the original backlog entry) is intentionally **not** implemented here — see Task 5's `HunterSelfDestructAI.Enter()`, which calls `SelfDestructDetonation.Arm` directly using the drone's own `ED.Config`, avoiding the need to equip an actual `SelfDestructModule` instance onto an NPC-like creature (no equip-slot API for RCUs was found during research; this is a deliberate simplification over the original backlog language, consistent with YAGNI — the drone doesn't need a player-facing equipped module, only the shared detonation behavior).

- [ ] **Step 2: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs
git commit -m "feat(hunter-drone): add HunterRemoteControllerModule PvE/PvP spawner subclasses (IMPROVEMENT-043)"
```

---

## Task 7: Content SQL — Hunter Drone entity definitions

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` (append)

- [ ] **Step 1: Append entity definitions for the two drones and two controller modules**

```sql
-- Part 3: HunterDrone (PvE/PvP) + HunterRemoteController (PvE/PvP) entity definitions.
-- Verify categoryflags/attributeflags/options bit values against a live DB before applying.

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_drones')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES ((SELECT ISNULL(MAX(value), 0) + 1 FROM categoryFlags), 'cf_hunter_drones', 'Autonomous kamikaze drones', 0, 0);
END

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve')
BEGIN
    INSERT INTO entitydefaults
    (definitionname, quantity, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
    ('def_standard_hunter_drone_pve', 1,
     (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
     '#turretType=HunterDrone',
     'Autonomous drone that hunts and self-destructs on Niani NPCs.',
     1, 50, 200, 0, 1500, 'def_standard_hunter_drone_pve', 0, 'standard', 1);
END
IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp')
BEGIN
    INSERT INTO entitydefaults
    (definitionname, quantity, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
    ('def_standard_hunter_drone_pvp', 1,
     (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
     '#turretType=HunterDrone',
     'Autonomous drone that hunts and self-destructs on hostile-standing players.',
     1, 50, 200, 0, 1500, 'def_standard_hunter_drone_pvp', 0, 'standard', 1);
END

-- Both drone definitions share the same self-destruct config keys as the player module (Task 2).
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'explosion_radius')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'explosion_radius', '12');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'damage_chemical')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'damage_chemical', '1500');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'damage_explosive')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'damage_explosive', '1500');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'damage_kinetic')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'damage_kinetic', '1500');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'damage_thermal')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'damage_thermal', '1500');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'ActionDelay')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'ActionDelay', '3000');
END
IF NOT EXISTS (SELECT 1 FROM definitionconfig WHERE definitionname = 'def_standard_hunter_drone_pve' AND configkey = 'item_work_range')
BEGIN
    INSERT INTO definitionconfig (definitionname, configkey, configvalue) VALUES ('def_standard_hunter_drone_pve', 'item_work_range', '40');
END

-- (repeat the same 6 definitionconfig rows for def_standard_hunter_drone_pvp — identical values,
-- omitted here for brevity; copy the block above and substitute the definitionname.)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
BEGIN
    INSERT INTO entitydefaults
    (definitionname, quantity, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
    ('def_standard_hunter_remote_controller_pve', 1,
     (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
     '#moduleFlag=i910#tier=$tierlevel_t1',
     'Deploys an autonomous PvE hunter drone.', 1, 100, 500, 0, 100,
     'def_standard_hunter_remote_controller_pve', 1, 'standard', 1);
END
IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
BEGIN
    INSERT INTO entitydefaults
    (definitionname, quantity, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
    ('def_standard_hunter_remote_controller_pvp', 1,
     (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
     '#moduleFlag=i911#tier=$tierlevel_t1',
     'Deploys an autonomous PvP hunter drone.', 1, 100, 500, 0, 100,
     'def_standard_hunter_remote_controller_pvp', 1, 'standard', 1);
END
```

> This is intentionally scoped to entity definitions + config only — **not** production recipes, research levels, tech tree placement, or prototype linkage (guide sections 8-17). Per the design spec's own "Content Required" section, tech tree placement is conditional ("if the items are researchable/craftable") and needs a live-DB decision about where these fit in the existing tech tree, which requires data not available in the docs — flag this explicitly to the user before extending the migration, per CLAUDE.md's instruction to ask rather than fabricate when dynamic resolution needs live data. `moduleFlag=i910`/`i911` are placeholder proposed values — verify free against `entitydefaults.options` usage before applying, same as `i909` in Task 2.

- [ ] **Step 2: Update schema documentation**

In `docs/db_structure/database_schema_documentation.md`, add short entries under the relevant `## effects`, `## aggregatefields`, and `## entitydefaults` sections noting the new rows added by this migration (follow the existing documentation style for those tables — see how prior migrations like `IMPROVEMENT-042-trade-list-order-type.sql` were documented, if that pattern exists in the docs).

- [ ] **Step 3: Commit**

```
git add docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql docs/db_structure/database_schema_documentation.md
git commit -m "docs(self-destruct): add content SQL for Hunter Drone and controller entity definitions (IMPROVEMENT-043)"
```

---

## Manual Validation (Full Flow)

After all tasks are committed and the migration applied to a dev DB:

1. **Player kamikaze end-to-end**: equip + activate `SelfDestructModule`, verify beam → countdown → AoE → own-robot death → pod-risk resurrect/loot flow, with no way to deactivate the module mid-countdown.
2. **Teleport pause**: activate, teleport via a stationary column mid-countdown, verify no reset and no post-teleport invulnerability; verify mobile teleport devices remain blocked outright (existing `HasPvpEffect` rule, untouched).
3. **PvE hunter drone**: spawn via `HunterRemoteControllerModulePvE` on an alpha zone with Niani NPCs nearby — verify Patrol → Approach → SelfDestruct → detonation on a Niani NPC, ignoring nearby players entirely.
4. **PvP hunter drone**: spawn via `HunterRemoteControllerModulePvP` in a PvP zone with a standing ≤ 0 player nearby — verify the same flow targets the player, and that the drone actively chases within 50m through the countdown even if the player tries to run.
5. **Retreat**: send a retreat command while a drone is in `Approach` — verify transition to `Retreat`, not `SelfDestruct`; verify a retreat command sent *after* `SelfDestruct` has armed is ignored (matches module's no-cancel behavior).
6. **Friendly fire**: verify a drone's detonation never damages another hunter drone (RCU AoE immunity, unchanged, verified by inspection of `ZoneExtensions.DoAoeDamage`, no test needed beyond confirming no regression).
7. **Command relay**: verify a hunter drone cannot be steered via target-lock relay — only retreat commands via `RemoteCommandTranslatorModule` have any effect.

---

## Potential Regressions

- `Player.ApplyInvulnerableEffect()` guard (Task 3) runs on every teleport/undock/session-start for every player, armed or not — must be verified as a true no-op in the non-armed case (the overwhelming majority), since it's a change to a widely-used method. Run `query-graph.ps1 Player -Direction in` before merging to confirm no other caller relies on `ApplyInvulnerableEffect()` unconditionally succeeding.
- `SmartCreature.OnEnterZone`'s if-chain (Task 5, Step 6) — the new `HunterDrone` branch must come *before* the existing `CombatDrone or SupportDrone` branch, or every hunter drone will silently get `GuardCombatDroneAI` instead of `HunterPatrolAI` (a `HunterDrone` also satisfies `is CombatDrone` by inheritance) and behave like a normal escort combat drone with no self-destruct behavior at all — verify branch order carefully during Task 5 implementation and testing.
- New `TurretType.HunterDrone` enum value — anywhere else in the codebase that exhaustively switches on `TurretType` (client protocol serialization, other spawn-adjacent logic) needs to handle the new case; enumerate via `query-graph.ps1 TurretType -Direction in` before considering Task 4 complete, not assumed safe from this plan alone.
- New `AggregateField`/`EffectType` enum values are purely additive appends at the end of their respective blocks — should not renumber or collide with existing values as long as the DB-verification steps in Task 1 are actually performed before migration.
- `owner.Kill(owner)` in `SelfDestructDetonation.Detonate` triggers `Unit.OnDead`'s own separate `DoExplosion()` wreck beam in addition to the custom AoE — confirmed expected/flavorful during Task 1, but re-verify visually during Task 2's manual validation that this doesn't look like a bug (e.g., a second unexpected damage tick) to players.
