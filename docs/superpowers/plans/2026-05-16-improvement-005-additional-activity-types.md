# IMPROVEMENT-005: Additional Season Activity Types — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 12 new season activity types in two phases — 5 non-combat types (Phase 1) and 7 combat types (Phase 2) — all integrated with the existing `RecordActivity` pipeline.

**Architecture:** Each new type adds an enum value to `SeasonActivityType`, a display name in the `ActivityTypeName` switch, and a `SeasonServiceLocator.Instance?.RecordActivity(...)` call at the relevant game event hook point. No DB schema changes are required.

**Tech Stack:** C# 12, .NET 8, SQL Server, existing `SeasonServiceLocator` / `RecordActivity` pattern.

---

## File Map

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonActivityType.cs` | Add 12 new enum values (Tasks 1 + 5) |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Add 12 display names to `ActivityTypeName` switch (Tasks 1 + 5) |
| `src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs` | Hook production job completion (Task 2) |
| `src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs` | Hook scanner artifact find (Task 3) |
| `src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs` | Hook relic artifact find (Task 3) |
| `src/Perpetuum/Accounting/AccountManager.cs` | Hook activity-based EP grant (Task 4) |
| `src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs` | Hook passive EP grant (Task 4) |
| `src/Perpetuum/Units/Unit.cs` | Hook damage dealt/received (Task 6) |
| `src/Perpetuum/Modules/ArmorRepairModule.cs` | Hook armor restored (Task 7) |
| `src/Perpetuum/Modules/EnergyNeutralizerModule.cs` | Hook energy drain dealt/received (Task 8) |
| `src/Perpetuum/Modules/EnergyTransfererModule.cs` | Hook energy transfer dealt/received (Task 9) |

---

## Phase 1 — Non-Combat Types

---

### Task 1: Add Phase 1 Enum Values and Display Names

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs:472-483`

- [ ] **Step 1: Add enum values**

Open `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`. Replace the entire file content with:

```csharp
namespace Perpetuum.Services.Seasons
{
    public enum SeasonActivityType
    {
        NpcKill            = 1,
        PvpKill            = 2,
        MissionComplete    = 3,
        MineralMined       = 4,
        EpSpent            = 5,
        NicEarned          = 6,
        NicSpent           = 7,
        IntrusionPoint     = 8,

        // Phase 1 — non-combat
        Prototyping        = 9,
        ReverseEngineering = 10,
        Production         = 11,
        ArtifactFound      = 12,
        EpEarned           = 13,
    }
}
```

- [ ] **Step 2: Add display names**

Open `src/Perpetuum/Services/Seasons/SeasonService.cs`. Find the `ActivityTypeName` method at line ~472. Replace it with:

```csharp
private static string ActivityTypeName(SeasonActivityType type) => type switch
{
    SeasonActivityType.NpcKill            => "NPC Kill",
    SeasonActivityType.PvpKill            => "PvP Kill",
    SeasonActivityType.MissionComplete    => "Mission Completed",
    SeasonActivityType.MineralMined       => "Mineral Mined",
    SeasonActivityType.EpSpent            => "EP Spent",
    SeasonActivityType.NicEarned          => "NIC Earned",
    SeasonActivityType.NicSpent           => "NIC Spent",
    SeasonActivityType.IntrusionPoint     => "Intrusion SAP",
    SeasonActivityType.Prototyping        => "Prototyping",
    SeasonActivityType.ReverseEngineering => "Reverse Engineering",
    SeasonActivityType.Production         => "Production",
    SeasonActivityType.ArtifactFound      => "Artifact Found",
    SeasonActivityType.EpEarned           => "EP Earned",
    _                                     => type.ToString(),
};
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonActivityType.cs src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add Phase 1 activity type enum values and display names"
```

---

### Task 2: Wire Production Hook

**Files:**
- Modify: `src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs`

The hook fires at job completion inside `EndProduction`. The production type is on `productionInProgress.type` (`ProductionInProgressType` enum). Amount is `productionInProgress.amountOfCycles` (number of production cycles/jobs).

- [ ] **Step 1: Add using**

Open `src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs`. At the top, check if `using Perpetuum.Services.Seasons;` is present. If not, add it with the other `using` statements.

- [ ] **Step 2: Add the hook**

Find the block at line ~238 (inside `EndProduction`, inside the transaction scope):

```csharp
var ep =CalculateEp(facility, productionInProgress);

productionInProgress.character.AddExtensionPointsBoostAndLog( EpForActivityType.Production, ep);
```

Add the season hook immediately after the `AddExtensionPointsBoostAndLog` call:

```csharp
var ep =CalculateEp(facility, productionInProgress);

productionInProgress.character.AddExtensionPointsBoostAndLog( EpForActivityType.Production, ep);

var seasonType = productionInProgress.type switch
{
    ProductionInProgressType.prototype    => (SeasonActivityType?)SeasonActivityType.Prototyping,
    ProductionInProgressType.research     => SeasonActivityType.ReverseEngineering,
    ProductionInProgressType.massProduction => SeasonActivityType.Production,
    _                                     => null,
};
if (seasonType.HasValue)
{
    SeasonServiceLocator.Instance?.RecordActivity(
        productionInProgress.character.Id,
        seasonType.Value,
        productionInProgress.amountOfCycles);
}
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 4: Manual validation**

Using an admin character on a test server:
1. Use `#SeasonAddRate <season_id> 9 1 1` to set Prototyping rate to 1 pt per cycle.
2. Use `#SeasonAddRate <season_id> 10 1 1` for ReverseEngineering.
3. Use `#SeasonAddRate <season_id> 11 1 1` for Production.
4. Complete a prototyping job — verify character season points increase.
5. Complete a research (reverse engineering) job — verify points increase.
6. Complete a mass production job — verify points increase proportional to `amountOfCycles`.
7. Complete a job of any other type (e.g., refine, reprocess) — verify points do NOT change.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Services/ProductionEngine/ProductionProcessor.cs
git commit -m "feat(seasons): wire Prototyping, ReverseEngineering, Production activity hooks"
```

---

### Task 3: Wire ArtifactFound Hook

**Files:**
- Modify: `src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs`
- Modify: `src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs`

Two distinct artifact systems both grant EP on find — both must record the season activity.

- [ ] **Step 1: Add using to ArtifactScanner.cs**

Open `src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs`. Add `using Perpetuum.Services.Seasons;` if not already present.

- [ ] **Step 2: Hook in ArtifactScanner.cs**

Find line ~61:

```csharp
if (ep > 0) player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Artifact, ep);
```

Add the season hook on the next line:

```csharp
if (ep > 0) player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Artifact, ep);
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.ArtifactFound, 1);
```

Note: the `RecordActivity` call is unconditional (not guarded by `ep > 0`). An artifact is found even on training zones where EP is 0. If training zone filtering is desired, `RecordActivity` already filters training characters internally.

- [ ] **Step 3: Add using to AbstractRelic.cs**

Open `src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs`. Add `using Perpetuum.Services.Seasons;` if not already present.

- [ ] **Step 4: Hook in AbstractRelic.cs**

Find line ~150 (inside `Task.Run(() => { ... })`):

```csharp
if (ep > 0) player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Artifact, ep);
```

Add the season hook on the next line, still inside the `Task.Run` lambda and inside the `using (var scope = Db.CreateTransaction())` block:

```csharp
if (ep > 0) player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Artifact, ep);
SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.ArtifactFound, 1);
```

- [ ] **Step 5: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 6: Manual validation**

1. Set rate: `#SeasonAddRate <season_id> 12 1 1`
2. Find an artifact via scanner on a non-training zone — verify season points increase by 1.
3. Find a relic (the other artifact system) — verify season points increase by 1.
4. Confirm no crash on training zones (points will not accumulate for training chars, but the call must not throw).

- [ ] **Step 7: Commit**

```
git add src/Perpetuum/Zones/Artifacts/Scanners/ArtifactScanner.cs src/Perpetuum/Services/Relics/Relics/AbstractRelic.cs
git commit -m "feat(seasons): wire ArtifactFound activity hook (scanner and relic paths)"
```

---

### Task 4: Wire EpEarned Hook

**Files:**
- Modify: `src/Perpetuum/Accounting/AccountManager.cs`
- Modify: `src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs`

Two EP sources: activity-based boosts (via `AddExtensionPointsBoostAndLog`) and passive daily grants (via `GiveExtensionPointsService`).

- [ ] **Step 1: Add using to AccountManager.cs**

Open `src/Perpetuum/Accounting/AccountManager.cs`. Add `using Perpetuum.Services.Seasons;` if not present.

- [ ] **Step 2: Hook activity-based EP in AccountManager.cs**

Find `AddExtensionPointsBoostAndLog` at line ~356. The method currently ends with:

```csharp
AddExtensionPoints(account, boostedPoints);
return boostedPoints;
```

Add the season hook before the `return`:

```csharp
AddExtensionPoints(account, boostedPoints);
SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.EpEarned, boostedPoints);
return boostedPoints;
```

- [ ] **Step 3: Add using to GiveExtensionPointsService.cs**

Open `src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs`. Add `using Perpetuum.Services.Seasons;` if not present.

- [ ] **Step 4: Hook passive EP in GiveExtensionPointsService.cs**

Find `InformAffectedCharacters` at line ~55. The method currently sends messages to `affectedLeechers` and `affectedPayingCustomers`. Add season recording for each group:

```csharp
if (grp.Key == BASEPOINTS)
{
    var affectedLeechers = grp.Select(r => Character.Get(r.GetValue<int>(0))).Distinct().ToArray();
    Logger.Info($"Daily Extension Point Add: {affectedLeechers.Length} characters will be informed with point {BASEPOINTS} - leechers.");
    ExtensionHelper.CreateExtensionPointsIncreasedMessage(BASEPOINTS).ToCharacters(affectedLeechers).Send();
    foreach (var c in affectedLeechers)
        SeasonServiceLocator.Instance?.RecordActivity(c.Id, SeasonActivityType.EpEarned, BASEPOINTS);
}
else
{
    var affectedPayingCustomers = grp.Select(r => Character.Get(r.GetValue<int>(0))).Distinct().ToArray();
    Logger.Info($"Daily Extension Point Add: {affectedPayingCustomers.Length} characters will be informed with point {BONUSPOINTS} - good guys.");
    ExtensionHelper.CreateExtensionPointsIncreasedMessage(BONUSPOINTS).ToCharacters(affectedPayingCustomers).Send();
    foreach (var c in affectedPayingCustomers)
        SeasonServiceLocator.Instance?.RecordActivity(c.Id, SeasonActivityType.EpEarned, BONUSPOINTS);
}
```

- [ ] **Step 5: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 6: Manual validation**

1. Set rate: `#SeasonAddRate <season_id> 13 1 1000` (1 pt per 1000 EP — the daily grant is 1440/2880 so use scale 1 for easy testing)
2. Kill an NPC (triggers activity-based EP from `AddExtensionPointsBoostAndLog`) — verify season `EpEarned` activity is recorded (check DB or season score update).
3. Complete a mission — verify additional EP earned is recorded.
4. Passive EP: wait for or simulate the `GiveExtensionPointsService` daily tick in a test environment — verify the passive grant amount is also recorded per character.

- [ ] **Step 7: Commit**

```
git add src/Perpetuum/Accounting/AccountManager.cs src/Perpetuum/Services/ExtensionService/GiveExtensionPointsService.cs
git commit -m "feat(seasons): wire EpEarned activity hook (activity boosts and passive daily grant)"
```

---

## Phase 2 — Combat Types

---

### Task 5: Add Phase 2 Enum Values and Display Names

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Add Phase 2 enum values**

Open `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`. Replace the file content with:

```csharp
namespace Perpetuum.Services.Seasons
{
    public enum SeasonActivityType
    {
        NpcKill              = 1,
        PvpKill              = 2,
        MissionComplete      = 3,
        MineralMined         = 4,
        EpSpent              = 5,
        NicEarned            = 6,
        NicSpent             = 7,
        IntrusionPoint       = 8,

        // Phase 1 — non-combat
        Prototyping          = 9,
        ReverseEngineering   = 10,
        Production           = 11,
        ArtifactFound        = 12,
        EpEarned             = 13,

        // Phase 2 — combat
        DamageDone           = 14,
        DamageReceived       = 15,
        ArmorRestored        = 16,
        EnergyDrainDealt     = 17,
        EnergyDrainReceived  = 18,
        EnergyTransferDealt  = 19,
        EnergyTransferReceived = 20,
    }
}
```

- [ ] **Step 2: Add Phase 2 display names**

Open `src/Perpetuum/Services/Seasons/SeasonService.cs`. Find `ActivityTypeName` and replace with:

```csharp
private static string ActivityTypeName(SeasonActivityType type) => type switch
{
    SeasonActivityType.NpcKill               => "NPC Kill",
    SeasonActivityType.PvpKill               => "PvP Kill",
    SeasonActivityType.MissionComplete       => "Mission Completed",
    SeasonActivityType.MineralMined          => "Mineral Mined",
    SeasonActivityType.EpSpent               => "EP Spent",
    SeasonActivityType.NicEarned             => "NIC Earned",
    SeasonActivityType.NicSpent              => "NIC Spent",
    SeasonActivityType.IntrusionPoint        => "Intrusion SAP",
    SeasonActivityType.Prototyping           => "Prototyping",
    SeasonActivityType.ReverseEngineering    => "Reverse Engineering",
    SeasonActivityType.Production            => "Production",
    SeasonActivityType.ArtifactFound         => "Artifact Found",
    SeasonActivityType.EpEarned              => "EP Earned",
    SeasonActivityType.DamageDone            => "Damage Done",
    SeasonActivityType.DamageReceived        => "Damage Received",
    SeasonActivityType.ArmorRestored         => "Armor Restored",
    SeasonActivityType.EnergyDrainDealt      => "Energy Drained (Dealt)",
    SeasonActivityType.EnergyDrainReceived   => "Energy Drained (Received)",
    SeasonActivityType.EnergyTransferDealt   => "Energy Transferred (Dealt)",
    SeasonActivityType.EnergyTransferReceived => "Energy Transferred (Received)",
    _                                         => type.ToString(),
};
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonActivityType.cs src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add Phase 2 activity type enum values and display names"
```

---

### Task 6: Wire DamageDone / DamageReceived Hook

**Files:**
- Modify: `src/Perpetuum/Units/Unit.cs`

`Unit.cs` already imports `Perpetuum.Players`, so the `Player` type is available. Hook goes into `OnDamageTaken`, which fires for all units. We guard with `is Player` checks: attacker only records `DamageDone` if they are a player; victim only records `DamageReceived` if they are a player (NPCs have no character ID and do not accumulate season points).

- [ ] **Step 1: Add using**

Open `src/Perpetuum/Units/Unit.cs`. Add `using Perpetuum.Services.Seasons;` if not already present.

- [ ] **Step 2: Modify OnDamageTaken**

Find `OnDamageTaken` at line ~389. Replace the method with:

```csharp
protected virtual void OnDamageTaken(Unit source, DamageTakenEventArgs e)
{
    DamageTaken?.Invoke(this, source, e);

    CombatLogPacket packet = new CombatLogPacket(CombatLogType.Damage, this, source);
    packet.AppendByte((byte)(e.IsCritical ? 1 : 0));
    packet.AppendDouble(e.TotalDamage);
    packet.AppendDouble(e.TotalKers);
    packet.Send(this, source);

    if (!(e.TotalDamage >= 0.0))
    {
        return;
    }

    Armor -= e.TotalDamage;

    var damageAmount = (long)e.TotalDamage;
    if (damageAmount > 0)
    {
        if (source is Player attacker)
            SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.DamageDone, damageAmount);
        if (this is Player victim)
            SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.DamageReceived, damageAmount);
    }

    OnCombatEvent(source, e);

    if (Armor <= 0.0)
    {
        Kill(source);
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 4: Manual validation**

1. Set rates: `#SeasonAddRate <season_id> 14 1 100` and `#SeasonAddRate <season_id> 15 1 100` (1 pt per 100 HP).
2. With a player character, fire weapons at an NPC — verify `DamageDone` is recorded for the player, and no crash occurs for the NPC.
3. Let an NPC fire at a player — verify `DamageReceived` is recorded for the player.
4. Two players fight each other — verify both `DamageDone` for the attacker and `DamageReceived` for the victim are recorded.
5. Confirm overall game stability under normal combat — no performance regression visible.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Units/Unit.cs
git commit -m "feat(seasons): wire DamageDone and DamageReceived activity hooks"
```

---

### Task 7: Wire ArmorRestored Hook

**Files:**
- Modify: `src/Perpetuum/Modules/ArmorRepairModule.cs`

Both `ArmorRepairModule` (self-repair) and `RemoteArmorRepairModule` (remote repair) call `OnRepair` on the base class `ArmorRepairerBaseModule`. The hook goes into `OnRepair` so both module types are covered. `ParentRobot` is the unit activating the module; we record the activity only if that unit is a player.

- [ ] **Step 1: Add using**

Open `src/Perpetuum/Modules/ArmorRepairModule.cs`. Add `using Perpetuum.Services.Seasons;` and `using Perpetuum.Players;` if not already present.

- [ ] **Step 2: Modify OnRepair**

Find `protected void OnRepair(Unit target, double amount)` at line ~48. Replace the method with:

```csharp
protected void OnRepair(Unit target, double amount)
{
    if (amount <= 0.0)
    {
        return;
    }

    double armor = target.Armor;

    target.Armor += amount;

    double total = Math.Abs(armor - target.Armor);
    CombatLogPacket packet = new CombatLogPacket(CombatLogType.ArmorRepair, target, ParentRobot, this);

    packet.AppendDouble(amount);
    packet.AppendDouble(total);
    packet.Send(target, ParentRobot);

    var repaired = (long)total;
    if (repaired > 0 && ParentRobot is Player repairer)
        SeasonServiceLocator.Instance?.RecordActivity(repairer.Character.Id, SeasonActivityType.ArmorRestored, repaired);
}
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 4: Manual validation**

1. Set rate: `#SeasonAddRate <season_id> 16 1 10` (1 pt per 10 HP restored).
2. Activate a local armor repair module while at partial HP — verify `ArmorRestored` is recorded for the player activating the module.
3. Activate a remote armor repair module targeting an ally — verify `ArmorRestored` is recorded for the player activating the module (not the target).
4. Let an NPC repair itself (if applicable) — verify no `ArmorRestored` is recorded for NPC units.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Modules/ArmorRepairModule.cs
git commit -m "feat(seasons): wire ArmorRestored activity hook (local and remote repair modules)"
```

---

### Task 8: Wire EnergyDrainDealt / EnergyDrainReceived Hook

**Files:**
- Modify: `src/Perpetuum/Modules/EnergyNeutralizerModule.cs`

`EnergyNeutralizerModule` covers energy neutralizers. Energy drainer modules (if any) should be checked for a separate class — if a drainer module inherits from `EnergyDispersionModule` and has its own `OnAction`, it needs the same hook. The current codebase only has `EnergyNeutralizerModule` as a concrete class — this task covers that class.

- [ ] **Step 1: Add usings**

Open `src/Perpetuum/Modules/EnergyNeutralizerModule.cs`. Add `using Perpetuum.Services.Seasons;` and `using Perpetuum.Players;` if not present.

- [ ] **Step 2: Modify OnAction**

Find `protected override void OnAction()` at line ~31. Replace the method with:

```csharp
protected override void OnAction()
{
    var unitLock = GetLock().ThrowIfNotType<UnitLock>(ErrorCodes.InvalidLockType);

    if (!LOSCheckAndCreateBeam(unitLock.Target))
    {
        OnError(ErrorCodes.LOSFailed);

        return;
    }

    var coreNeutralized = _energyNeutralizedAmount.Value;
    var coreNeutralizedDone = 0.0;

    ModifyValueByReactorRadiation(unitLock.Target,ref coreNeutralized);
    coreNeutralized = ModifyValueByOptimalRange(unitLock.Target,coreNeutralized);
    
    if ( coreNeutralized > 0.0 )
    {
        var core = unitLock.Target.Core;

        unitLock.Target.Core -= coreNeutralized;
        coreNeutralizedDone = Math.Abs(core - unitLock.Target.Core);
        unitLock.Target.OnCombatEvent(ParentRobot, new EnergyDispersionEventArgs(coreNeutralizedDone));

        var threatValue = (coreNeutralizedDone / 2) + 1;

        unitLock.Target.AddThreat(ParentRobot, new Threat(ThreatType.EnWar, threatValue));

        var drainAmount = (long)coreNeutralizedDone;
        if (drainAmount > 0)
        {
            if (ParentRobot is Player attacker)
                SeasonServiceLocator.Instance?.RecordActivity(attacker.Character.Id, SeasonActivityType.EnergyDrainDealt, drainAmount);
            if (unitLock.Target is Player victim)
                SeasonServiceLocator.Instance?.RecordActivity(victim.Character.Id, SeasonActivityType.EnergyDrainReceived, drainAmount);
        }
    }

    var packet = new CombatLogPacket(CombatLogType.EnergyNeutralize, unitLock.Target, ParentRobot, this);

    packet.AppendDouble(coreNeutralized);
    packet.AppendDouble(coreNeutralizedDone);
    packet.Send(unitLock.Target,ParentRobot);
}
```

- [ ] **Step 3: Check for other energy drain module classes**

Run the build and search for any other class that inherits from `EnergyDispersionModule`:

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Also grep for additional concrete drain classes:

```
grep -r "EnergyDispersionModule" src/ --include="*.cs" -l
```

If any additional class exists with its own `OnAction` that drains energy, apply the same hook pattern from Step 2 to that class.

- [ ] **Step 4: Manual validation**

1. Set rates: `#SeasonAddRate <season_id> 17 1 10` and `#SeasonAddRate <season_id> 18 1 10`.
2. Activate an energy neutralizer on a player target — verify `EnergyDrainDealt` for attacker and `EnergyDrainReceived` for the victim player.
3. Activate an energy neutralizer on an NPC — verify only `EnergyDrainDealt` for the player attacker (NPC has no season tracking).
4. Let an NPC use an energy neutralizer on a player — verify only `EnergyDrainReceived` for the player victim.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Modules/EnergyNeutralizerModule.cs
git commit -m "feat(seasons): wire EnergyDrainDealt and EnergyDrainReceived activity hooks"
```

---

### Task 9: Wire EnergyTransferDealt / EnergyTransferReceived Hook

**Files:**
- Modify: `src/Perpetuum/Modules/EnergyTransfererModule.cs`

`coreNeutralized` is energy actually removed from the giver (already clamped by their available core). `coreTransfered` is energy actually added to the receiver (may differ due to range modifiers). Track each direction separately with its actual value.

- [ ] **Step 1: Add usings**

Open `src/Perpetuum/Modules/EnergyTransfererModule.cs`. Add `using Perpetuum.Services.Seasons;` and `using Perpetuum.Players;` if not present.

- [ ] **Step 2: Modify OnAction**

Find `protected override void OnAction()` at line ~24. Replace the method with:

```csharp
protected override void OnAction()
{
    UnitLock unitLock = GetLock().ThrowIfNotType<UnitLock>(ErrorCodes.InvalidLockType);

    (ParentIsPlayer() && unitLock.Target is Npc).ThrowIfTrue(ErrorCodes.ThisModuleIsNotSupportedOnNPCs);

    if (!LOSCheckAndCreateBeam(unitLock.Target))
    {
        OnError(ErrorCodes.LOSFailed);

        return;
    }

    double coreAmount = _energyTransferAmount.Value;

    coreAmount = ModifyValueByOptimalRange(unitLock.Target, coreAmount);

    double coreNeutralized = 0.0;
    double coreTransfered = 0.0;

    if (coreAmount > 0.0)
    {
        double core = ParentRobot.Core;

        ParentRobot.Core -= coreAmount;
        coreNeutralized = Math.Abs(core - ParentRobot.Core);

        double targetCore = unitLock.Target.Core;

        unitLock.Target.Core += coreNeutralized;
        coreTransfered = Math.Abs(targetCore - unitLock.Target.Core);
        unitLock.Target.SpreadAssistThreatToNpcs(ParentRobot, new Threat(ThreatType.Support, coreAmount * 2));

        if (ParentRobot is Player giver && coreNeutralized > 0.0)
            SeasonServiceLocator.Instance?.RecordActivity(giver.Character.Id, SeasonActivityType.EnergyTransferDealt, (long)coreNeutralized);
        if (unitLock.Target is Player receiver && coreTransfered > 0.0)
            SeasonServiceLocator.Instance?.RecordActivity(receiver.Character.Id, SeasonActivityType.EnergyTransferReceived, (long)coreTransfered);
    }

    CombatLogPacket packet = new CombatLogPacket(CombatLogType.EnergyTransfer, unitLock.Target, ParentRobot, this);

    packet.AppendDouble(coreAmount);
    packet.AppendDouble(coreNeutralized);
    packet.AppendDouble(coreTransfered);
    packet.Send(unitLock.Target, ParentRobot);
}
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeds, 0 errors.

- [ ] **Step 4: Manual validation**

1. Set rates: `#SeasonAddRate <season_id> 19 1 10` and `#SeasonAddRate <season_id> 20 1 10`.
2. Activate energy transfer from a player to another player — verify `EnergyTransferDealt` for the giver and `EnergyTransferReceived` for the receiver.
3. Transfer with a range penalty (target near edge of range) — verify `EnergyTransferDealt` (coreNeutralized) may differ from `EnergyTransferReceived` (coreTransfered).
4. Confirm the module still correctly rejects targeting NPCs (existing error code behavior unchanged).

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Modules/EnergyTransfererModule.cs
git commit -m "feat(seasons): wire EnergyTransferDealt and EnergyTransferReceived activity hooks"
```

---

## Final Validation Checklist

- [ ] All 12 new activity types appear correctly in `#SeasonInfo <season_id>` output
- [ ] Training characters do not accumulate points for any new type (existing filter in `RecordActivity`)
- [ ] Activity outside an active season does not cause errors (null-safe `SeasonServiceLocator.Instance?` pattern)
- [ ] No performance regression visible in combat zone under normal player load
- [ ] IMPROVEMENT-005 backlog status updated to DONE
