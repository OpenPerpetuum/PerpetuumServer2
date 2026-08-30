# IMPROVEMENT-023 — Same-IP Gate for NIC Activities: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent same-machine accounts from farming season points by trading NIC with themselves, by suppressing `NicEarned`/`NicSpent` season recording when both counterparties share the same session IP.

**Architecture:** `ActivityEvent` gains an optional `CounterpartyAccountId`; `SeasonService.RecordActivity` does an IP lookup when it is set and returns early on a match. Market NIC recording is moved from the wallet callback (which has no counterparty) to explicit call sites in `Market.cs`. Transport assignment calls already exist explicitly and just need the counterparty account ID added.

**Tech Stack:** C# 12 / .NET 8, SQL Server (`accountonlinetime` table), `Perpetuum.Services.Seasons`, `Perpetuum.Services.MarketEngine`, `Perpetuum.Services.MissionEngine.TransportAssignments`

**Spec:** `docs/superpowers/specs/2026-05-21-improvement-023-same-ip-gate-design.md`

---

## File Map

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/ActivityEvent.cs` | Add `CounterpartyAccountId` optional field |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Add `GetMostRecentSessionIp` helper + same-IP gate in `RecordActivity` |
| `src/Perpetuum/Players/Player.cs` | Fix latent `TOP 1 ORDER BY` bug in PvpKill IP query |
| `src/Perpetuum/Accounting/Characters/CharacterWallet.cs` | Remove `marketSell` from `NicEarned` case; remove `marketBuy` from `NicSpent` case |
| `src/Perpetuum/Services/MarketEngine/Market.cs` | Add explicit `RecordActivity` calls for player-to-player fills and vendor fills |
| `src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs` | Add `CounterpartyAccountId` to all 7 existing `RecordActivity` calls |

---

## Task 1: Extend `ActivityEvent` and add the gate in `SeasonService`

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/ActivityEvent.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

### Step 1.1 — Add `CounterpartyAccountId` to `ActivityEvent`

Open `src/Perpetuum/Services/Seasons/ActivityEvent.cs`. Replace:

```csharp
public record ActivityEvent(long Amount, int? DefinitionId = null);
```

With:

```csharp
public record ActivityEvent(long Amount, int? DefinitionId = null, int? CounterpartyAccountId = null);
```

- [ ] Make the change above.

### Step 1.2 — Add the IP helper and gate in `SeasonService`

Open `src/Perpetuum/Services/Seasons/SeasonService.cs`.

**Add the private helper** anywhere in the private helpers region at the bottom of the class (search for `// ── Helpers ──` to find it):

```csharp
private static string? GetMostRecentSessionIp(int accountId)
    => Db.Query()
        .CommandText("SELECT TOP 1 ip FROM accountonlinetime WHERE accountid = @accountId ORDER BY loggedin DESC")
        .SetParameter("@accountId", accountId)
        .ExecuteScalar<string>();
```

**Add the same-IP guard** inside `RecordActivity` (line 174). Insert it after the `IsInTraining()` early-return (currently around line 186–187) and before `double basePoints = 0;`:

```csharp
if (evt.CounterpartyAccountId.HasValue)
{
    var myIp    = GetMostRecentSessionIp(Character.Get(characterId).AccountId);
    var theirIp = GetMostRecentSessionIp(evt.CounterpartyAccountId.Value);
    if (myIp != null && theirIp != null &&
        string.Equals(myIp, theirIp, StringComparison.OrdinalIgnoreCase))
        return;
}
```

The resulting order of early-returns in `RecordActivity` should be:
1. Season null / expired check
2. No matching rate check
3. Training character check
4. **Same-IP check (new)**
5. `double basePoints = 0;` and rest of logic

- [ ] Add the helper method.
- [ ] Add the gate block in `RecordActivity`.

### Step 1.3 — Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds, 0 errors.

- [ ] Run the build and confirm it passes.

### Step 1.4 — Commit

```
git add src/Perpetuum/Services/Seasons/ActivityEvent.cs src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add same-IP gate to RecordActivity via CounterpartyAccountId"
```

- [ ] Commit.

---

## Task 2: Fix the latent IP query bug in the PvpKill gate

**Files:**
- Modify: `src/Perpetuum/Players/Player.cs` (around line 1093)

The existing PvpKill gate queries `accountonlinetime` without `TOP 1 ORDER BY loggedin DESC`. For accounts with multiple session rows (any player who has logged in more than once) this returns a non-deterministic row. Fix both queries in `HandlePlayerDead`.

### Step 2.1 — Update both queries in `HandlePlayerDead`

In `Player.cs`, around line 1093, replace:

```csharp
var victimIp = Db.Query()
    .CommandText("select ip from accountonlinetime where accountid = @accountId")
    .SetParameter("@accountId", this.Character.AccountId)
    .ExecuteScalar<string>();
var killerIp = Db.Query()
    .CommandText("select ip from accountonlinetime where accountid = @accountId")
    .SetParameter("@accountId", killerPlayer.Character.AccountId)
    .ExecuteScalar<string>();
```

With:

```csharp
var victimIp = Db.Query()
    .CommandText("SELECT TOP 1 ip FROM accountonlinetime WHERE accountid = @accountId ORDER BY loggedin DESC")
    .SetParameter("@accountId", this.Character.AccountId)
    .ExecuteScalar<string>();
var killerIp = Db.Query()
    .CommandText("SELECT TOP 1 ip FROM accountonlinetime WHERE accountid = @accountId ORDER BY loggedin DESC")
    .SetParameter("@accountId", killerPlayer.Character.AccountId)
    .ExecuteScalar<string>();
```

- [ ] Make the change above.

### Step 2.2 — Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds, 0 errors.

- [ ] Run the build and confirm it passes.

### Step 2.3 — Commit

```
git add src/Perpetuum/Players/Player.cs
git commit -m "fix(seasons): use TOP 1 ORDER BY loggedin DESC in PvpKill IP query"
```

- [ ] Commit.

---

## Task 3: Remove market transaction types from `CharacterWallet`

**Files:**
- Modify: `src/Perpetuum/Accounting/Characters/CharacterWallet.cs`

The wallet fires `RecordActivity` for `marketSell` → `NicEarned` and `marketBuy` → `NicSpent`, but has no counterparty handle. Recording is moving to explicit call sites in `Market.cs` where both characters are available. Remove these two types from the wallet switch now so that market fills no longer double-record via the wallet after the explicit calls are added in Task 4.

**Important:** After this step and before Task 4 is complete, vendor market fills will temporarily lose NIC recording. This is an intermediate state only — Task 4 adds the explicit calls back.

### Step 3.1 — Remove `marketSell` from the `NicEarned` case

Open `src/Perpetuum/Accounting/Characters/CharacterWallet.cs`. In `OnCommited`, find the `NicEarned` case (around line 82). Remove `TransactionType.marketSell` from the case list and `TransactionType.buyOrderPayBack`:

Current state:
```csharp
case TransactionType.marketSell:
case TransactionType.buyOrderPayBack:
case TransactionType.missionPayOut:
case TransactionType.refund:
case TransactionType.InsurancePayOut:
case TransactionType.GoodiePackCredit:
    SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)change));
    break;
```

New state (remove only `marketSell`; keep `buyOrderPayBack` — it has no clean counterparty and is out of scope):

```csharp
case TransactionType.buyOrderPayBack:
case TransactionType.missionPayOut:
case TransactionType.refund:
case TransactionType.InsurancePayOut:
case TransactionType.GoodiePackCredit:
    SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)change));
    break;
```

- [ ] Remove `marketSell` from the `NicEarned` case list.

### Step 3.2 — Remove `marketBuy` from the `NicSpent` case

In the same file, find the `NicSpent` case (around line 56). Remove `TransactionType.marketBuy` from the case list:

Current state (excerpt):
```csharp
case TransactionType.hangarRent:
case TransactionType.marketBuy:
case TransactionType.hangarRentAuto:
```

New state:
```csharp
case TransactionType.hangarRent:
case TransactionType.hangarRentAuto:
```

- [ ] Remove `marketBuy` from the `NicSpent` case list.

### Step 3.3 — Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds, 0 errors. (NIC recording for market fills is temporarily absent — restored in Task 4.)

- [ ] Run the build and confirm it passes.

### Step 3.4 — Commit

```
git add src/Perpetuum/Accounting/Characters/CharacterWallet.cs
git commit -m "refactor(seasons): remove marketSell/marketBuy from wallet NIC recording (moving to explicit call sites)"
```

- [ ] Commit.

---

## Task 4: Add explicit `RecordActivity` calls in `Market.cs`

**Files:**
- Modify: `src/Perpetuum/Services/MarketEngine/Market.cs`

There are five insertion points. Work through them in order.

### Step 4.1 — `FulfillBuyOrderInstantly`, sub-case: `itemOnMarket.Quantity > quantity` (player sell)

This sub-case starts around line 393. The `CashIn` (line 412) and `PayOutToSeller` (line 415) are already in place. After `PayOutToSeller` on line 415, and before `//the remaining amount` comment, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * quantity),
                      CounterpartyAccountId: seller.AccountId));
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(marketSellOrder.price * quantity),
                      CounterpartyAccountId: buyer.AccountId));
```

- [ ] Insert the two `RecordActivity` calls after line 415 (after the `PayOutToSeller` call in the first quantity sub-case).

### Step 4.2 — `FulfillBuyOrderInstantly`, sub-case: `itemOnMarket.Quantity == quantity` (player sell)

This sub-case is around line 417. After `PayOutToSeller` at line 432, and before `marketSellOrder.quantity = 0;`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * quantity),
                      CounterpartyAccountId: seller.AccountId));
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(marketSellOrder.price * quantity),
                      CounterpartyAccountId: buyer.AccountId));
```

- [ ] Insert the two `RecordActivity` calls after line 432 in the second quantity sub-case.

### Step 4.3 — `FulfillBuyOrderInstantly`, sub-case: `itemOnMarket.Quantity < quantity` (player sell, partial fill)

In this sub-case (around line 436), the actual transacted quantity is `itemOnMarket.Quantity` (not the full `quantity`). After `PayOutToSeller` at line 458 (for the actual transacted items), insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * itemOnMarket.Quantity),
                      CounterpartyAccountId: seller.AccountId));
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(marketSellOrder.price * itemOnMarket.Quantity),
                      CounterpartyAccountId: buyer.AccountId));
```

Note: use `itemOnMarket.Quantity` (the stock actually available), not `quantity` (what the buyer requested).

- [ ] Insert the two `RecordActivity` calls after line 458 in the third quantity sub-case, using `itemOnMarket.Quantity`.

### Step 4.4 — `BuyOrderFulfilledToCharacter` (seller sells into existing player buy order)

This method starts at line 600. Its signature is:
```csharp
public void BuyOrderFulfilledToCharacter(Character seller, bool useSellersCorporationWallet, MarketOrder buyOrder, int boughtQuantity, Container container, Item boughtItem, Character buyer)
```

After `PayOutToSeller` at line 636, and before `_centralBank.SubAmount`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(buyOrder.price * boughtItem.Quantity),
                      CounterpartyAccountId: buyer.AccountId));
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(buyOrder.price * boughtItem.Quantity),
                      CounterpartyAccountId: seller.AccountId));
```

- [ ] Insert the two `RecordActivity` calls after line 636 in `BuyOrderFulfilledToCharacter`.

### Step 4.5 — Vendor sell order fills (finite and infinite) — buyer's `NicSpent`, no counterparty

Removing `marketBuy` from the wallet switch also removes NIC recording for players buying from vendor sell orders. Restore it with explicit no-counterparty calls.

**Finite vendor sell order** (inside the `if (marketSellOrder.quantity > 0)` block, around line 519–520):
After `_marketHelper.CashIn(buyer, ..., TransactionType.marketBuy)` at line 520, and before `Message.Builder.SetCommand(Commands.MarketSellOrderUpdate)`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * boughtQuantity)));
```

**Infinite vendor sell order** (lines 543–558):
After `_marketHelper.CashIn(buyer, ..., TransactionType.marketBuy)` at line 544, and before `itemOnMarket = publicContainer.CreateAndAddItem(...)`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * quantity)));
```

- [ ] Insert the `NicSpent` call after the finite vendor `CashIn` (line 520).
- [ ] Insert the `NicSpent` call after the infinite vendor `CashIn` (line 544).

### Step 4.6 — Vendor buy order fills — seller's `NicEarned`, no counterparty

Removing `marketSell` from the wallet switch also removes NIC recording for players selling to vendor buy orders. Restore it with explicit no-counterparty calls.

**`FiniteVendorBuyOrderTakesTheItem`** (line 648): After `PayOutToSeller` at line 664, and before `_centralBank.SubAmount`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(vendorBuyOrder.price * boughtItem.Quantity)));
```

**`FulfillSellOrderInstantly`, vendor finite path, partial fill** (around line 727): After `PayOutToSeller` at line 727, and before `_marketHandler.InsertAveragePrice`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(buyOrder.price * buyOrder.quantity)));
```

**`FulfillSellOrderInstantly`, infinite vendor buy order** (around line 782): After `PayOutToSeller` at line 782, and before `_centralBank.SubAmount`, insert:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(buyOrder.price * itemToSell.Quantity)));
```

- [ ] Insert `NicEarned` after `PayOutToSeller` in `FiniteVendorBuyOrderTakesTheItem` (line 664).
- [ ] Insert `NicEarned` after `PayOutToSeller` in `FulfillSellOrderInstantly` vendor finite partial-fill (line 727).
- [ ] Insert `NicEarned` after `PayOutToSeller` in `FulfillSellOrderInstantly` infinite vendor path (line 782).

### Step 4.7 — Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds, 0 errors.

- [ ] Run the build and confirm it passes.

### Step 4.8 — Commit

```
git add src/Perpetuum/Services/MarketEngine/Market.cs
git commit -m "feat(seasons): move market NicEarned/NicSpent recording to call sites with same-IP gate"
```

- [ ] Commit.

---

## Task 5: Add `CounterpartyAccountId` to transport assignment calls

**Files:**
- Modify: `src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs`

There are 7 existing `RecordActivity` calls. Each needs a `CounterpartyAccountId` added. The two fields `ownercharacter` and `volunteercharacter` are always populated at the time these methods run.

### Step 5.1 — `PayCollateralToPrincipal`

Owner earns the collateral; volunteer is the counterparty.

Find:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral)));
```

Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral), CounterpartyAccountId: volunteercharacter.AccountId));
```

- [ ] Update `PayCollateralToPrincipal`.

### Step 5.2 — `PaybackCollateral`

Volunteer earns the collateral back; owner is the counterparty.

Find:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral)));
```

Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral), CounterpartyAccountId: ownercharacter.AccountId));
```

- [ ] Update `PaybackCollateral`.

### Step 5.3 — `PaybackHalfCollateral`

Volunteer earns half the collateral back; owner is the counterparty.

Find:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral * COLLATERAL_PENALTY)));
```

Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(collateral * COLLATERAL_PENALTY), CounterpartyAccountId: ownercharacter.AccountId));
```

- [ ] Update `PaybackHalfCollateral`.

### Step 5.4 — `PaybackReward`

Owner earns the reward back; volunteer is the counterparty.

Find:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(reward)));
```

In `PaybackReward` specifically (there may be multiple `ownercharacter` `NicEarned` calls — match by method context). Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(reward), CounterpartyAccountId: volunteercharacter.AccountId));
```

- [ ] Update `PaybackReward`.

### Step 5.5 — `PayOutReward`

Volunteer earns the reward + collateral; owner is the counterparty.

Find:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(reward + collateral)));
```

Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(volunteercharacter.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)Math.Abs(reward + collateral), CounterpartyAccountId: ownercharacter.AccountId));
```

- [ ] Update `PayOutReward`.

### Step 5.6 — `TakeCollateral`

Owner records `NicSpent` (note: the existing call uses `ownercharacter.Id`); volunteer is the counterparty.

Find (in `TakeCollateral`):
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(collateral)));
```

Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(collateral), CounterpartyAccountId: volunteercharacter.AccountId));
```

- [ ] Update `TakeCollateral`.

### Step 5.7 — `CashInOnSubmit`

Owner spends the reward escrow; volunteer is the counterparty.

Find (in `CashInOnSubmit`):
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(reward)));
```

Replace with:
```csharp
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(reward), CounterpartyAccountId: volunteercharacter.AccountId));
```

- [ ] Update `CashInOnSubmit`.

### Step 5.8 — Build

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds, 0 errors.

- [ ] Run the build and confirm it passes.

### Step 5.9 — Commit

```
git add src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs
git commit -m "feat(seasons): add same-IP gate to transport assignment NIC activity recording"
```

- [ ] Commit.

---

## Task 6: Manual validation

No automated test suite exists. Validate the following scenarios against a running server with an active season configured with `NicEarned` and `NicSpent` activity rates.

### Step 6.1 — Same-IP player market trade (gate fires)

- Log in two accounts from the **same machine** (same IP in `accountonlinetime`).
- Account A: create a sell order for any item at a set price.
- Account B: buy that item.
- Check `season_activity_log` (or equivalent season score table): neither account should have received `NicEarned` or `NicSpent` points for this transaction.
- Confirm the item transfer and NIC transfer completed normally (game mechanics unaffected).

- [ ] Verify same-IP market trade is gated.

### Step 6.2 — Different-IP player market trade (gate does not fire)

- Repeat Step 6.1 with Account A logged in from a **different machine** (different IP).
- Both accounts should receive the expected season points for `NicEarned` / `NicSpent`.

- [ ] Verify different-IP market trade records season points normally.

### Step 6.3 — Vendor market trade (no gate)

- Buy an item from a vendor sell order (NPC-listed item with `isVendorItem = true`).
- Buyer should receive `NicSpent` season points — no gate applies (vendor has no player counterparty).
- Sell an item to a vendor buy order.
- Seller should receive `NicEarned` season points.

- [ ] Verify vendor trades record NIC season points without gating.

### Step 6.4 — Transport assignment, same IP

- Submit a transport assignment with owner and volunteer on the **same machine**.
- Complete the assignment (or have the volunteer deliver).
- Confirm no `NicEarned` / `NicSpent` season points are awarded to either party for collateral or reward payments.

- [ ] Verify same-IP transport assignment is gated.

### Step 6.5 — Transport assignment, different IP

- Repeat Step 6.4 with owner and volunteer on **different machines**.
- Both parties should receive the expected season points for their respective payments.

- [ ] Verify different-IP transport assignment records normally.

### Step 6.6 — PvpKill regression

- Kill a player character from a different IP.
- Confirm the killer receives the `PvpKill` season point.
- Kill a player character from the same IP.
- Confirm neither party receives `PvpKill` season points.

- [ ] Verify PvpKill gate is unaffected.

### Step 6.7 — No active season

- Disable or expire the active season.
- Execute a player-to-player market trade.
- Confirm the server does not error; the gate short-circuits before the IP lookup when no season is active (existing `season == null` guard fires first).

- [ ] Verify no errors when no season is active.
