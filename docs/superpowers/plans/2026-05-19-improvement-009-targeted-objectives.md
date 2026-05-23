# IMPROVEMENT-009: Targeted Objectives (Mining & Harvesting) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend season objectives so a `MineralMined` or `PlantHarvested` objective can optionally require a specific material, and add an Admin Tool picker to configure it.

**Architecture:** `ActivityEvent` record wraps the existing `long amount` on the `RecordActivity` interface, adding an optional `DefinitionId`; a new `PlantHarvested = 21` enum value splits mining from harvesting; `season_objectives` gains a nullable `target_definition_id` column matched in the objective loop. The Admin Tool loads material lists by category flag and presents a combobox column on the objectives DataGrid.

**Tech Stack:** C# 12 / .NET 8, SQL Server, WPF (MVVM / CommunityToolkit.Mvvm), MSBuild x64 Release.

**Spec:** `docs/superpowers/specs/2026-05-19-improvement-009-targeted-objectives-design.md`

**Build command** (use throughout to verify):
```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

> **Note:** This project has no automated test suite. Verification is via build success and the manual steps in each task.

---

## File Map

**Create:**
- `src/Perpetuum/Services/Seasons/ActivityEvent.cs`
- `src/Perpetuum.AdminTool/Seasons/MaterialPickItem.cs`

**Modify (server):**
- `src/Perpetuum/Services/Seasons/ISeasonService.cs`
- `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`
- `src/Perpetuum/Services/Seasons/SeasonModels.cs`
- `src/Perpetuum/Services/Seasons/SeasonRepository.cs`
- `src/Perpetuum/Services/Seasons/SeasonService.cs`
- `src/Perpetuum/Accounting/AccountManager.cs`
- `src/Perpetuum/Accounting/Characters/CharacterWallet.cs`
- `src/Perpetuum/Players/Player.cs`
- `src/Perpetuum/Modules/ArmorRepairModule.cs`
- `src/Perpetuum/Modules/DrillerModule.cs`
- `src/Perpetuum/Modules/EnergyNeutralizerModule.cs`
- `src/Perpetuum/Modules/EnergyTransfererModule.cs`
- `src/Perpetuum/Modules/EnergyVampireModule.cs`
- `src/Perpetuum/Modules/HarvesterModule.cs`
- `src/Perpetuum/Modules/LargeDrillerModule.cs`
- `src/Perpetuum/Modules/ScorcherModule.cs`
- `src/Perpetuum/Units/Unit.cs`
- `src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs`
- `src/Perpetuum/Zones/Intrusion/Outpost.cs`
- `src/Perpetuum/Zones/NpcSystem/Npc.cs`
- `src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs`
- `src/Perpetuum/Services/MissionEngine/MissionProcessorObjects/MissionProcessorAdvanceTarget.cs`
- `src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs`
- `src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs`
- `src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs`

**Modify (Admin Tool):**
- `src/Perpetuum.AdminTool/Seasons/SeasonActivityRateRow.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`
- `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`
- `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs`
- `src/Perpetuum.AdminTool/ViewModels/SeasonsViewModel.cs`
- `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

---

## Task 1: Create `ActivityEvent` record

**Files:**
- Create: `src/Perpetuum/Services/Seasons/ActivityEvent.cs`

- [ ] **Create the file**

```csharp
namespace Perpetuum.Services.Seasons
{
    public record ActivityEvent(long Amount, int? DefinitionId = null);
}
```

- [ ] **Build to verify it compiles in isolation**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: Build succeeds (no existing code references it yet).

- [ ] **Commit**

```
git add src/Perpetuum/Services/Seasons/ActivityEvent.cs
git commit -m "feat(seasons): add ActivityEvent record for RecordActivity context"
```

---

## Task 2: Migrate `RecordActivity` signature + all call sites

**Context:** The interface change is breaking — the solution will not build until every call site is updated. Complete every sub-step in this task before building.

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/ISeasonService.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`
- Modify: `src/Perpetuum/Accounting/AccountManager.cs`
- Modify: `src/Perpetuum/Accounting/Characters/CharacterWallet.cs`
- Modify: `src/Perpetuum/Players/Player.cs`
- Modify: `src/Perpetuum/Modules/ArmorRepairModule.cs`
- Modify: `src/Perpetuum/Modules/DrillerModule.cs`
- Modify: `src/Perpetuum/Modules/LargeDrillerModule.cs`
- Modify: `src/Perpetuum/Modules/HarvesterModule.cs`
- Modify: `src/Perpetuum/Modules/EnergyNeutralizerModule.cs`
- Modify: `src/Perpetuum/Modules/EnergyTransfererModule.cs`
- Modify: `src/Perpetuum/Modules/EnergyVampireModule.cs`
- Modify: `src/Perpetuum/Modules/ScorcherModule.cs`
- Modify: `src/Perpetuum/Units/Unit.cs`
- Modify: `src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs`
- Modify: `src/Perpetuum/Zones/Intrusion/Outpost.cs`
- Modify: `src/Perpetuum/Zones/NpcSystem/Npc.cs`
- Modify: `src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs`
- Modify: `src/Perpetuum/Services/MissionEngine/MissionProcessorObjects/MissionProcessorAdvanceTarget.cs`
- Modify: `src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs`
- Modify: `src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs`
- Modify: `src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs`

- [ ] **Update `ISeasonService.cs`** — change parameter type from `long amount` to `ActivityEvent evt`

```csharp
// ISeasonService.cs — full file replacement
using Perpetuum.Accounting.Characters;

namespace Perpetuum.Services.Seasons
{
    public interface ISeasonService
    {
        void RecordActivity(int characterId, SeasonActivityType type, ActivityEvent evt);
        void OnCharacterLogin(Character character);
    }
}
```

- [ ] **Update `SeasonService.cs`** — change method signature and replace `amount` with `evt.Amount`

Find (line ~140):
```csharp
public void RecordActivity(int characterId, SeasonActivityType activityType, long amount)
```
Replace with:
```csharp
public void RecordActivity(int characterId, SeasonActivityType activityType, ActivityEvent evt)
```

Then replace the two uses of `amount` in the method body:
```csharp
// line ~159 — before:
basePoints += (double)Math.Round((double)amount / scale * rate.PointsPerUnit, 2);
// after:
basePoints += (double)Math.Round((double)evt.Amount / scale * rate.PointsPerUnit, 2);
```

(That is the only use of `amount` in the method body. The rest of the method is unchanged.)

- [ ] **Update `AccountManager.cs`** — two call sites (~lines 317 and 383)

```csharp
// line ~317 — before:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.EpSpent, spentPoints);
// after:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.EpSpent, new ActivityEvent(spentPoints));

// line ~383 — before:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.EpEarned, boostedPoints);
// after:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.EpEarned, new ActivityEvent(boostedPoints));
```

- [ ] **Update `CharacterWallet.cs`** — two call sites (~lines 79 and 88)

```csharp
// line ~79 — before:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicSpent, (long)Math.Abs(change));
// after:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(change)));

// line ~88 — before:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicEarned, (long)change);
// after:
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)change));
```

- [ ] **Update `Player.cs`** — one call site (~line 1104)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(killerPlayer.Character.Id, SeasonActivityType.PvpKill, 1);
// after:
SeasonServiceLocator.Instance?.RecordActivity(killerPlayer.Character.Id, SeasonActivityType.PvpKill, new ActivityEvent(1));
```

- [ ] **Update `ArmorRepairModule.cs`** — one call site (~line 70)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(repairer.Character.Id, SeasonActivityType.ArmorRestored, repaired);
// after:
SeasonServiceLocator.Instance?.RecordActivity(repairer.Character.Id, SeasonActivityType.ArmorRestored, new ActivityEvent(repaired));
```

- [ ] **Update `DrillerModule.cs`** — one call site (~line 193)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, drilledQuantity);
// after (definition will be added in Task 4):
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(drilledQuantity));
```

- [ ] **Update `LargeDrillerModule.cs`** — one call site (~line 114)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, drilledQuantity);
// after (definition will be added in Task 4):
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(drilledQuantity));
```

- [ ] **Update `HarvesterModule.cs`** — one call site (~line 145)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, extractedMaterial.Quantity);
// after (type and definition will be updated in Task 4):
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(extractedMaterial.Quantity));
```

- [ ] **Update `EnergyNeutralizerModule.cs`** — two call sites (~lines 66 and 68)

```csharp
// line ~66 — before:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, drainAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, new ActivityEvent(drainAmount));

// line ~68 — before:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, drainAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, new ActivityEvent(drainAmount));
```

- [ ] **Update `EnergyTransfererModule.cs`** — two call sites (~lines 60 and 62)

```csharp
// line ~60 — before:
SeasonServiceLocator.Instance?.RecordActivity(giver.Character.Id, SeasonActivityType.EnergyTransferDealt, (long)coreNeutralized);
// after:
SeasonServiceLocator.Instance?.RecordActivity(giver.Character.Id, SeasonActivityType.EnergyTransferDealt, new ActivityEvent((long)coreNeutralized));

// line ~62 — before:
SeasonServiceLocator.Instance?.RecordActivity(receiver.Character.Id, SeasonActivityType.EnergyTransferReceived, (long)coreTransfered);
// after:
SeasonServiceLocator.Instance?.RecordActivity(receiver.Character.Id, SeasonActivityType.EnergyTransferReceived, new ActivityEvent((long)coreTransfered));
```

- [ ] **Update `EnergyVampireModule.cs`** — two call sites (~lines 73 and 75)

```csharp
// line ~73 — before:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, drainAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, new ActivityEvent(drainAmount));

// line ~75 — before:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, drainAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, new ActivityEvent(drainAmount));
```

- [ ] **Update `ScorcherModule.cs`** — two call sites (~lines 97 and 99)

```csharp
// line ~97 — before:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, drainAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, new ActivityEvent(drainAmount));

// line ~99 — before:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, drainAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, new ActivityEvent(drainAmount));
```

- [ ] **Update `Unit.cs`** — two call sites (~lines 411 and 413)

```csharp
// line ~411 — before:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.DamageDone, damageAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.DamageDone, new ActivityEvent(damageAmount));

// line ~413 — before:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.DamageReceived, damageAmount);
// after:
SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.DamageReceived, new ActivityEvent(damageAmount));
```

- [ ] **Update `ArtifactScanner.cs`** — one call site (~line 63)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.ArtifactFound, 1);
// after:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.ArtifactFound, new ActivityEvent(1));
```

- [ ] **Update `Outpost.cs`** — one call site (~line 555)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.IntrusionPoint, 1);
// after:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.IntrusionPoint, new ActivityEvent(1));
```

- [ ] **Update `Npc.cs`** — one call site (~line 260)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(killerPlayer.Character.Id, SeasonActivityType.NpcKill, 1);
// after:
SeasonServiceLocator.Instance?.RecordActivity(killerPlayer.Character.Id, SeasonActivityType.NpcKill, new ActivityEvent(1));
```

- [ ] **Update `GiveExtensionPointsService.cs`** — two call sites (~lines 78 and 87)

```csharp
// line ~78 — before:
SeasonServiceLocator.Instance?.RecordActivity(c.Id, SeasonActivityType.EpEarned, BASEPOINTS);
// after:
SeasonServiceLocator.Instance?.RecordActivity(c.Id, SeasonActivityType.EpEarned, new ActivityEvent(BASEPOINTS));

// line ~87 — before:
SeasonServiceLocator.Instance?.RecordActivity(c.Id, SeasonActivityType.EpEarned, BONUSPOINTS);
// after:
SeasonServiceLocator.Instance?.RecordActivity(c.Id, SeasonActivityType.EpEarned, new ActivityEvent(BONUSPOINTS));
```

- [ ] **Update `MissionProcessorAdvanceTarget.cs`** — one call site (~line 557)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(p.Id, SeasonActivityType.MissionComplete, 1);
// after:
SeasonServiceLocator.Instance?.RecordActivity(p.Id, SeasonActivityType.MissionComplete, new ActivityEvent(1));
```

- [ ] **Update `TransportAssignment.cs`** — seven call sites (~lines 189, 195, 201, 207, 213, 219, 308)

```csharp
// line ~189 — before:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, (long)Math.Abs(collateral));
// after:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral)));

// line ~195 — before:
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, (long)Math.Abs(collateral));
// after:
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral)));

// line ~201 — before:
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, (long)Math.Abs(collateral * COLLATERAL_PENALTY));
// after:
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral * COLLATERAL_PENALTY)));

// line ~207 — before:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, (long)Math.Abs(reward));
// after:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(reward)));

// line ~213 — before:
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, (long)Math.Abs(reward + collateral));
// after:
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(reward + collateral)));

// line ~219 — before:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, (long)Math.Abs(collateral));
// after:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(collateral)));

// line ~308 — before:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, (long)Math.Abs(reward));
// after:
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(reward)));
```

- [ ] **Update `ProductionProcessor.cs`** — one multiline call site (~lines 252–255)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(
    productionInProgress.character.Id,
    seasonType.Value,
    productionInProgress.amountOfCycles);
// after:
SeasonServiceLocator.Instance?.RecordActivity(
    productionInProgress.character.Id,
    seasonType.Value,
    new ActivityEvent(productionInProgress.amountOfCycles));
```

- [ ] **Update `AbstractRelic.cs`** — one call site (~line 152)

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.ArtifactFound, 1);
// after:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.ArtifactFound, new ActivityEvent(1));
```

- [ ] **Build to verify all call sites are updated**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: Build succeeds with 0 errors. If you see "no overload for method RecordActivity takes 3 arguments of type long", you missed a call site — search for `RecordActivity.*SeasonActivityType` to find it.

- [ ] **Commit**

```
git add src/Perpetuum/Services/Seasons/ISeasonService.cs
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git add src/Perpetuum/Accounting/AccountManager.cs
git add src/Perpetuum/Accounting/Characters/CharacterWallet.cs
git add src/Perpetuum/Players/Player.cs
git add src/Perpetuum/Modules/ArmorRepairModule.cs
git add src/Perpetuum/Modules/DrillerModule.cs
git add src/Perpetuum/Modules/LargeDrillerModule.cs
git add src/Perpetuum/Modules/HarvesterModule.cs
git add src/Perpetuum/Modules/EnergyNeutralizerModule.cs
git add src/Perpetuum/Modules/EnergyTransfererModule.cs
git add src/Perpetuum/Modules/EnergyVampireModule.cs
git add src/Perpetuum/Modules/ScorcherModule.cs
git add src/Perpetuum/Units/Unit.cs
git add src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs
git add src/Perpetuum/Zones/Intrusion/Outpost.cs
git add src/Perpetuum/Zones/NpcSystem/Npc.cs
git add src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs
git add src/Perpetuum/Services/MissionEngine/MissionProcessorObjects/MissionProcessorAdvanceTarget.cs
git add src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs
git add src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs
git add src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs
git commit -m "refactor(seasons): migrate RecordActivity to ActivityEvent parameter"
```

---

## Task 3: Add `PlantHarvested` activity type and update switch expressions

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonActivityRateRow.cs`
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs`

- [ ] **Add `PlantHarvested` to `SeasonActivityType.cs`**

```csharp
// Add after EnergyTransferReceived = 20:
PlantHarvested       = 21,
```

- [ ] **Add arm to `SeasonService.ActivityTypeName` switch (~line 501)**

```csharp
// Add before the default arm:
SeasonActivityType.PlantHarvested        => "Plant Harvested",
```

- [ ] **Add arm to `SeasonActivityRateRow.ActivityTypeLabel` switch**

In `src/Perpetuum.AdminTool/Seasons/SeasonActivityRateRow.cs`, find the `ActivityTypeLabel` switch and add:
```csharp
SeasonActivityType.PlantHarvested => "Plant Harvested",
```

Also find the effective-rate description switch (the one that produces strings like "pts per unit mined") and add:
```csharp
SeasonActivityType.PlantHarvested => unitScale > 1
    ? $"{pts} pts per {scale} units harvested"
    : $"{pts} pts per unit harvested",
```

- [ ] **Add `PlantHarvested` to `SeasonDetailViewModel.ActivityTypeOptions`**

In `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`, find `ActivityTypeOptions` (line ~41) and add at the end of the array:
```csharp
new ActivityTypeOption(SeasonActivityType.PlantHarvested, "Plant Harvested"),
```

- [ ] **Add `PlantHarvested` to `SeasonWizardViewModel.ActivityTypeOptions`**

In `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs`, find the matching `ActivityTypeOption` array and add:
```csharp
new ActivityTypeOption(SeasonActivityType.PlantHarvested, "Plant Harvested"),
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonActivityType.cs
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonActivityRateRow.cs
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git add src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs
git commit -m "feat(seasons): add PlantHarvested activity type (split from MineralMined)"
```

---

## Task 4: Pass material definitions at mining and harvesting call sites

**Files:**
- Modify: `src/Perpetuum/Modules/DrillerModule.cs`
- Modify: `src/Perpetuum/Modules/LargeDrillerModule.cs`
- Modify: `src/Perpetuum/Modules/HarvesterModule.cs`

- [ ] **Update `DrillerModule.cs`** (~line 193) — add `drilledMineralDefinition`

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(drilledQuantity));
// after:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(drilledQuantity, drilledMineralDefinition));
```

- [ ] **Update `LargeDrillerModule.cs`** (~line 114) — add `drilledMineralDefinition`

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(drilledQuantity));
// after:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(drilledQuantity, drilledMineralDefinition));
```

- [ ] **Update `HarvesterModule.cs`** (~line 145) — change to `PlantHarvested` and add `extractedHarvestDefinition`

```csharp
// before:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new ActivityEvent(extractedMaterial.Quantity));
// after:
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.PlantHarvested, new ActivityEvent(extractedMaterial.Quantity, extractedHarvestDefinition));
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum/Modules/DrillerModule.cs
git add src/Perpetuum/Modules/LargeDrillerModule.cs
git add src/Perpetuum/Modules/HarvesterModule.cs
git commit -m "feat(seasons): pass mineral/plant definition to RecordActivity at mining/harvesting sites"
```

---

## Task 5: Server model, repository, and objective filter

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonModels.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Add `TargetDefinitionId` to `SeasonObjective` in `SeasonModels.cs`**

Find the `SeasonObjective` class and add after `PackageId`:
```csharp
public int? TargetDefinitionId { get; set; }
```

- [ ] **Update `SeasonRepository.GetObjectives`** to SELECT and map the new column

Find the method `GetObjectives` (line ~52). Change the query string from:
```csharp
"SELECT id, season_id, name, description, activity_type, " +
"target_value, bonus_points, display_order, is_daily, package_id " +
"FROM season_objectives WHERE season_id = @seasonId"
```
to:
```csharp
"SELECT id, season_id, name, description, activity_type, " +
"target_value, bonus_points, display_order, is_daily, package_id, target_definition_id " +
"FROM season_objectives WHERE season_id = @seasonId"
```

In the `.Select(r => new SeasonObjective { ... })` block, add after `PackageId`:
```csharp
TargetDefinitionId = r.GetValue<int?>("target_definition_id"),
```

- [ ] **Add the definition filter in `SeasonService.RecordActivity`**

Find the foreach loop over `_activeObjectives` (line ~171). Add the filter as the first line inside the loop body, before the `DateTime dayWindow` line:

```csharp
foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
{
    if (obj.TargetDefinitionId.HasValue && obj.TargetDefinitionId != evt.DefinitionId)
        continue;

    DateTime dayWindow = obj.IsDaily
    // ... rest of loop unchanged
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonModels.cs
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add TargetDefinitionId to SeasonObjective and filter in RecordActivity"
```

---

## Task 6: Database migration

- [ ] **Run the migration against your local database**

Connect to the Perpetuum SQL Server database and execute:
```sql
ALTER TABLE season_objectives ADD target_definition_id INT NULL;
```

- [ ] **Verify the column exists**

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'season_objectives' AND COLUMN_NAME = 'target_definition_id';
```
Expected: one row, `INT`, `YES`.

- [ ] **Verify existing rows are unaffected**

```sql
SELECT TOP 5 id, name, target_definition_id FROM season_objectives;
```
Expected: `target_definition_id` is `NULL` for all existing rows.

---

## Task 7: Admin Tool — `MaterialPickItem` and `SeasonObjectiveRow` target fields

**Files:**
- Create: `src/Perpetuum.AdminTool/Seasons/MaterialPickItem.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs`

- [ ] **Create `MaterialPickItem.cs`**

```csharp
namespace Perpetuum.AdminTool.Seasons
{
    public record MaterialPickItem(int Definition, string DisplayName)
    {
        public string Display => $"{Definition} — {DisplayName}";
    }
}
```

- [ ] **Update `SeasonObjectiveRow.cs`** — add target fields and material list logic

Add these using directives at the top if not already present:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Perpetuum.ExportedTypes;
```

Add the following to the `SeasonObjectiveRow` class body (after the existing `[ObservableProperty]` fields):

```csharp
[ObservableProperty] private int? _targetDefinitionId;
[ObservableProperty] private string? _targetDisplayName;

private IReadOnlyList<MaterialPickItem> _oreAndLiquidMaterials = Array.Empty<MaterialPickItem>();
private IReadOnlyList<MaterialPickItem> _organicMaterials = Array.Empty<MaterialPickItem>();

[ObservableProperty] private IReadOnlyList<MaterialPickItem> _availableMaterials = Array.Empty<MaterialPickItem>();

public void InitializeMaterialLists(
    IReadOnlyList<MaterialPickItem> oreAndLiquid,
    IReadOnlyList<MaterialPickItem> organics)
{
    _oreAndLiquidMaterials = oreAndLiquid;
    _organicMaterials = organics;
    RefreshAvailableMaterials();
}

partial void OnActivityTypeChanged(SeasonActivityType value) => RefreshAvailableMaterials();

partial void OnTargetDefinitionIdChanged(int? value)
{
    TargetDisplayName = AvailableMaterials
        .FirstOrDefault(m => m.Definition == value)?.DisplayName;
}

private void RefreshAvailableMaterials()
{
    AvailableMaterials = ActivityType switch
    {
        SeasonActivityType.MineralMined   => _oreAndLiquidMaterials,
        SeasonActivityType.PlantHarvested => _organicMaterials,
        _                                 => Array.Empty<MaterialPickItem>()
    };
    if (TargetDefinitionId.HasValue &&
        !AvailableMaterials.Any(m => m.Definition == TargetDefinitionId))
        TargetDefinitionId = null;
}
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum.AdminTool/Seasons/MaterialPickItem.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs
git commit -m "feat(admin): add MaterialPickItem and target fields to SeasonObjectiveRow"
```

---

## Task 8: Admin Tool — build material lists in `SeasonDetailViewModel`

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonsViewModel.cs`

- [ ] **Add fields and constructor parameter to `SeasonDetailViewModel`**

Add a using directive at the top:
```csharp
using Perpetuum.AdminTool.Translations;
using Perpetuum.ExportedTypes;
```

Add two private fields after the existing `_connection` field:
```csharp
private IReadOnlyList<MaterialPickItem> _oreAndLiquidMaterials = Array.Empty<MaterialPickItem>();
private IReadOnlyList<MaterialPickItem> _organicMaterials = Array.Empty<MaterialPickItem>();
```

Add `TranslationsViewModel? translations = null` as the last constructor parameter:
```csharp
public SeasonDetailViewModel(
    SeasonRow season,
    SeasonRepository repo,
    PackageRepository pkgRepo,
    ChangeQueue queue,
    PackagesViewModel packagesVm,
    SeasonStatisticsViewModel statsVm,
    LookupCache cache,
    ConnectionSettings connection,
    ObservableCollection<PackageRow> packages,
    TranslationsViewModel? translations = null)
```

Store translations for use in `LoadAsync`:
```csharp
private readonly TranslationsViewModel? _translations;
```
In the constructor body add:
```csharp
_translations = translations;
```

- [ ] **Add `BuildMaterialLists` and `IsCategoryMatch` helper methods to `SeasonDetailViewModel`**

Add these private methods anywhere in the class:

```csharp
private void BuildMaterialLists(TranslationsViewModel? translations)
{
    const int EnglishLangId = 0;
    Dictionary<string, string>? englishNames = null;
    if (translations?.Store?.Rows != null)
    {
        englishNames = translations.Store.Rows
            .GroupBy(r => r.Key)
            .ToDictionary(g => g.Key, g => g.First()[EnglishLangId]);
    }

    var oreAndLiquid = new List<MaterialPickItem>();
    var organic = new List<MaterialPickItem>();

    foreach (var e in _cache.Entities)
    {
        if (!e.Enabled || e.Hidden) continue;

        var displayName = (englishNames != null &&
                           englishNames.TryGetValue(e.Name, out var eng) &&
                           !string.IsNullOrEmpty(eng))
            ? eng : e.Name;

        if (IsCategoryMatch(e.CategoryFlags, (long)CategoryFlags.cf_ore) ||
            IsCategoryMatch(e.CategoryFlags, (long)CategoryFlags.cf_liquid))
            oreAndLiquid.Add(new MaterialPickItem(e.Definition, displayName));
        else if (IsCategoryMatch(e.CategoryFlags, (long)CategoryFlags.cf_organic))
            organic.Add(new MaterialPickItem(e.Definition, displayName));
    }

    _oreAndLiquidMaterials = oreAndLiquid
        .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();
    _organicMaterials = organic
        .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

private static bool IsCategoryMatch(long entityFlags, long category)
{
    var mask = PackageItemPickItem.CategoryFlagsMask(category);
    return (entityFlags & mask) == category;
}
```

Note: `PackageItemPickItem.CategoryFlagsMask` is `internal static` in the same assembly (`Perpetuum.AdminTool`), so it is accessible here.

- [ ] **Call `BuildMaterialLists` at the start of `LoadAsync`**

`BuildMaterialLists` must run inside `LoadAsync` (not the constructor) so that `_cache.Entities` is populated before the lists are built. Find `LoadAsync` and add as its first statement:

```csharp
BuildMaterialLists(_translations);
```

- [ ] **Initialize material lists on each loaded objective in `LoadAsync`**

In the same `LoadAsync`, find where objectives are loaded (after `foreach (var o in await _repo.LoadObjectivesAsync(...))`). Add a call to `InitializeMaterialLists` on each row, just before `Objectives.Add(o)`:

```csharp
foreach (var o in await _repo.LoadObjectivesAsync(Season.Id))
{
    if (o.PackageId.HasValue)
        o.SelectedPackage = Packages.FirstOrDefault(p => p.Id == o.PackageId);
    o.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials);
    Objectives.Add(o);
}
```

- [ ] **Initialize material lists on newly added objectives in `AddObjective`**

Find the `AddObjective` method. After `var row = new SeasonObjectiveRow { ... }` and before `Objectives.Add(row)`, add:
```csharp
row.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials);
```

- [ ] **Pass `_translations` from `SeasonsViewModel.NavigateToSeason`**

In `src/Perpetuum.AdminTool/ViewModels/SeasonsViewModel.cs`, find `NavigateToSeason` (~line 117). Change:
```csharp
var detail = new SeasonDetailViewModel(
    row, _seasonRepo, _pkgRepo, _queue,
    PackagesVm, statsVm,
    _lookups, _connection, PackagesVm.Packages);
```
to:
```csharp
var detail = new SeasonDetailViewModel(
    row, _seasonRepo, _pkgRepo, _queue,
    PackagesVm, statsVm,
    _lookups, _connection, PackagesVm.Packages,
    _translations);
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git add src/Perpetuum.AdminTool/ViewModels/SeasonsViewModel.cs
git commit -m "feat(admin): build ore/liquid/organic material lists in SeasonDetailViewModel"
```

---

## Task 9: Admin Tool — repository and change script

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`

- [ ] **Update `SeasonRepository.LoadObjectivesAsync`** to read `target_definition_id`

Find `LoadObjectivesAsync`. Change the query string to include the new column (it's the 11th column, index 10):

```csharp
cmd.CommandText =
    "SELECT id, season_id, name, description, activity_type, " +
    "target_value, bonus_points, display_order, is_daily, package_id, target_definition_id " +
    "FROM season_objectives WHERE season_id = @seasonId ORDER BY display_order";
```

In the `while (await reader.ReadAsync())` block, add after `PackageId`:
```csharp
TargetDefinitionId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
```

- [ ] **Update `SeasonChanges.BuildInsertObjective`**

Find `BuildInsertObjective`. Change the SQL string:

```csharp
public static IPendingChange BuildInsertObjective(SeasonObjectiveRow row)
{
    return new RawSqlChange(
        $"season_objectives: insert '{row.Name}' in season {row.SeasonId}",
        $"INSERT INTO season_objectives (season_id, name, description, activity_type, " +
        $"target_value, bonus_points, display_order, is_daily, package_id, target_definition_id) VALUES (" +
        $"{row.SeasonId}, {SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
        $"{(int)row.ActivityType}, {row.TargetValue}, {row.BonusPoints}, {row.DisplayOrder}, " +
        $"{(row.IsDaily ? 1 : 0)}, {SqlLiteral.OfNullableInt(row.PackageId)}, " +
        $"{SqlLiteral.OfNullableInt(row.TargetDefinitionId)})");
}
```

- [ ] **Update `SeasonChanges.BuildUpdateObjective`**

Find `BuildUpdateObjective`. Change the SQL string:

```csharp
public static IPendingChange BuildUpdateObjective(SeasonObjectiveRow row)
{
    return new RawSqlChange(
        $"season_objectives: update id {row.Id}",
        $"UPDATE season_objectives SET name = {SqlLiteral.Of(row.Name)}, " +
        $"description = {SqlLiteral.Of(row.Description)}, " +
        $"activity_type = {(int)row.ActivityType}, target_value = {row.TargetValue}, " +
        $"bonus_points = {row.BonusPoints}, display_order = {row.DisplayOrder}, " +
        $"is_daily = {(row.IsDaily ? 1 : 0)}, package_id = {SqlLiteral.OfNullableInt(row.PackageId)}, " +
        $"target_definition_id = {SqlLiteral.OfNullableInt(row.TargetDefinitionId)} " +
        $"WHERE id = {row.Id}");
}
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs
git commit -m "feat(admin): read/write target_definition_id in season objectives repository and change scripts"
```

---

## Task 10: Admin Tool — objectives DataGrid Target Material column

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

- [ ] **Add the `Target Material` column after the `Activity Type` column**

In `SeasonDetailView.xaml`, find the objectives DataGrid (search for `FilteredObjectives`). After the closing `</DataGridTemplateColumn>` of the "Activity Type" column (after line ~206) and before the `<DataGridTextColumn Header="Target"` column, insert:

```xml
<DataGridTemplateColumn Header="Target Material" Width="220">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Margin="4,0" VerticalAlignment="Center"
                       Text="{Binding TargetDisplayName, FallbackValue='(any)'}"
                       ToolTip="{Binding TargetDefinitionId, FallbackValue=''}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding AvailableMaterials}"
                      DisplayMemberPath="DisplayName"
                      SelectedValuePath="Definition"
                      SelectedValue="{Binding TargetDefinitionId, UpdateSourceTrigger=PropertyChanged}"
                      IsEnabled="{Binding AvailableMaterials.Count, Converter={StaticResource CountToBoolConverter}}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

> **Note on `CountToBoolConverter`:** Check whether this converter already exists in the project's resource dictionary (search for `CountToBool` in XAML files). If it does not exist, either add a simple `IValueConverter` that returns `true` when `(int)value > 0`, or replace the `IsEnabled` binding with `IsEnabled="{Binding AvailableMaterials.Count, Converter={...}}"` using whichever converter is available. Alternatively, add a computed bool property `HasTargetMaterials` on `SeasonObjectiveRow` that returns `AvailableMaterials.Count > 0` and bind to that.

- [ ] **Verify `HasTargetMaterials` approach if no converter exists**

If no suitable converter exists, add to `SeasonObjectiveRow.cs`:
```csharp
public bool HasTargetMaterials => AvailableMaterials.Count > 0;
```

Add `OnPropertyChanged(nameof(HasTargetMaterials))` at the end of `RefreshAvailableMaterials()`.

Then in the XAML use:
```xml
IsEnabled="{Binding HasTargetMaterials}"
```

- [ ] **Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git add src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs
git commit -m "feat(admin): add Target Material combobox column to objectives DataGrid"
```

---

## Manual Validation

After all tasks are complete:

1. **Server build** — `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64` produces 0 errors.
2. **DB migration** — confirm `target_definition_id INT NULL` column exists on `season_objectives`; existing rows have NULL.
3. **Admin Tool — activity type list** — open Season Detail → Objectives tab → add a new objective → confirm "Plant Harvested" appears in the Activity Type dropdown.
4. **Admin Tool — ore picker** — set Activity Type to "Mineral Mined" → confirm Target Material combobox is enabled and shows only ores/liquids (e.g. `def_titanium_ore`, `def_crude_oil`).
5. **Admin Tool — organic picker** — set Activity Type to "Plant Harvested" → confirm combobox shows only organics.
6. **Admin Tool — other types** — set Activity Type to "NPC Kill" → confirm combobox is disabled/empty.
7. **Admin Tool — save script** — select a material, click "Queue Save" → confirm generated SQL includes `target_definition_id = <number>`. With no material selected, confirm `target_definition_id = NULL`.
8. **Admin Tool — load** — commit the script, reload → confirm the saved objective loads with the correct `TargetDisplayName` shown.
9. **In-game — targeted mining** — configure a `MineralMined` objective targeting ore X. Mine ore X → objective progresses. Mine ore Y → objective does not progress. Mine ore X without a target objective → unfiltered objective still progresses.
10. **In-game — targeted harvesting** — configure a `PlantHarvested` objective targeting plant X. Harvest plant X → objective progresses. Harvest plant Y → no progress.
11. **In-game — no regression** — confirm all existing `MineralMined` objectives with no target still receive progress from any mining. Confirm NPC kills, EP events, etc. still record normally.
