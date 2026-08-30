# IMPROVEMENT-023 — Same-IP Gate for NIC Earning/Spending Activities

**Date:** 2026-05-21
**Status:** Approved
**Backlog:** `docs/backlog/improvements.md` → IMPROVEMENT-023

---

## Summary

Enforce a same-IP gate on season activity recording so that characters on the same machine cannot farm season points by trading NIC with themselves. When both sides of a NIC transaction share the same originating IP address, neither side earns season points for that transaction.

---

## Scope

Gated transaction paths:

| Path | Activity types | Both chars available at call site? |
|---|---|---|
| Market player-to-player sell order fill | `NicEarned` (seller), `NicSpent` (buyer) | Yes — `Market.FulfillBuyOrderInstantly` |
| Market player-to-player buy order fill | `NicEarned` (seller), `NicSpent` (buyer) | Yes — `Market.FulfillSellOrderInstantly` / `BuyOrderFulfilledToCharacter` |
| Transport assignment payments | `NicEarned`, `NicSpent` | Yes — fields on `TransportAssignment` |

Out of scope:

- **Vendor market orders** (`isVendorItem == true`) — seller is a system entity, no player counterparty.
- **`buyOrderPayBack`** — excess-deposit refund when a sell order fills below buy-order price; counterparty not cleanly available at refund point; amount is small; farming incentive negligible.
- **Direct player trade** (`TradeSpent`/`TradeGained`) — currently records no NIC season activity; no change in this task. Both parties are always online, so the gate is trivial to add if NIC recording is introduced later.
- **Non-counterparty NIC types** (`missionPayOut`, `refund`, `InsurancePayOut`, `extensionLearn`, etc.) — system-to-player payments; no player counterparty; not gatable.

---

## Architecture

### 1. `ActivityEvent` — new optional field

**File:** `src/Perpetuum/Services/Seasons/ActivityEvent.cs`

```csharp
public record ActivityEvent(long Amount, int? DefinitionId = null, int? CounterpartyAccountId = null);
```

All existing call sites remain valid — `CounterpartyAccountId` defaults to `null`.

---

### 2. Gate in `SeasonService.RecordActivity`

**File:** `src/Perpetuum/Services/Seasons/SeasonService.cs`

Insert an early-return guard at the top of `RecordActivity`, after the season-active / rate-exists / training-character checks, before any point accumulation:

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

Private helper — also fixes a latent bug in the PvpKill gate (same query, no `TOP 1`/`ORDER BY`):

```csharp
private static string? GetMostRecentSessionIp(int accountId)
    => Db.Query()
        .CommandText("SELECT TOP 1 ip FROM accountonlinetime WHERE accountid = @accountId ORDER BY loggedin DESC")
        .SetParameter("@accountId", accountId)
        .ExecuteScalar<string>();
```

**Null-guard behaviour:** if either account has no session record (defensive edge case), the gate is skipped and activity records normally. This avoids penalising legitimate players due to missing data.

---

### 3. Market.cs — move NIC recording to explicit call sites

**File:** `src/Perpetuum/Services/MarketEngine/Market.cs`

**`CharacterWallet.OnCommited()` changes** (`src/Perpetuum/Accounting/Characters/CharacterWallet.cs`):
- Remove `TransactionType.marketSell` from the `NicEarned` case.
- Remove `TransactionType.marketBuy` from the `NicSpent` case.
- All other transaction types remain unchanged.

**`Market.FulfillBuyOrderInstantly`** (buyer buys from existing player sell order — non-vendor path only):

After the `CashIn` / `PayOutToSeller` block, add explicit recording:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * quantity),
                      CounterpartyAccountId: seller.AccountId));

SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(marketSellOrder.price * quantity),
                      CounterpartyAccountId: buyer.AccountId));
```

This applies to all three quantity-branching sub-cases inside the non-vendor path (`quantity < itemOnMarket.Quantity`, `==`, `>`). In each sub-case, use the actual transacted quantity (not the requested `quantity` where they differ).

**`Market.BuyOrderFulfilledToCharacter`** (seller sells into existing player buy order):

After `PayOutToSeller`:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(buyOrder.price * boughtItem.Quantity),
                      CounterpartyAccountId: buyer.AccountId));

SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(buyOrder.price * boughtItem.Quantity),
                      CounterpartyAccountId: seller.AccountId));
```

`buyer` is already resolved via `Character.GetByEid(buyOrder.submitterEID)` earlier in the method.

**Vendor paths:** no change. Vendor order fills remain wallet-driven (`marketSell` is removed from the wallet switch, so vendor fills must also get explicit calls — but for vendor orders there is no player counterparty, so record without `CounterpartyAccountId`):

```csharp
// vendor sell order filled by player buyer
SeasonServiceLocator.Instance?.RecordActivity(buyer.Id, SeasonActivityType.NicSpent,
    new ActivityEvent((long)(marketSellOrder.price * boughtQuantity)));

// vendor buy order filled by player seller
SeasonServiceLocator.Instance?.RecordActivity(seller.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)(vendorBuyOrder.price * boughtItem.Quantity)));
```

---

### 4. TransportAssignment.cs — add counterparty to existing calls

**File:** `src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs`

Each existing `RecordActivity` call gains `CounterpartyAccountId`. The two fields `ownercharacter` and `volunteercharacter` are always set at call time.

| Method | Character recording | Counterparty |
|---|---|---|
| `PayCollateralToPrincipal` | `ownercharacter` earns | `volunteercharacter.AccountId` |
| `PaybackCollateral` | `volunteercharacter` earns | `ownercharacter.AccountId` |
| `PaybackHalfCollateral` | `volunteercharacter` earns | `ownercharacter.AccountId` |
| `PaybackReward` | `ownercharacter` earns | `volunteercharacter.AccountId` |
| `PayOutReward` | `volunteercharacter` earns | `ownercharacter.AccountId` |
| `TakeCollateral` | `ownercharacter` spends | `volunteercharacter.AccountId` |
| `CashInOnSubmit` | `ownercharacter` spends | `volunteercharacter.AccountId` |

Example transformation:

```csharp
// before
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)Math.Abs(collateral)));

// after
SeasonServiceLocator.Instance?.RecordActivity(ownercharacter.Id, SeasonActivityType.NicEarned,
    new ActivityEvent((long)Math.Abs(collateral),
                      CounterpartyAccountId: volunteercharacter.AccountId));
```

---

## PvpKill gate — latent bug fix (incidental)

The existing gate in `Player.HandlePlayerDead` uses:

```csharp
"select ip from accountonlinetime where accountid = @accountId"
```

Without `TOP 1 ORDER BY loggedin DESC`, this returns a non-deterministic row when multiple session rows exist (every player who has logged in more than once). The new `GetMostRecentSessionIp` helper in `SeasonService` fixes this. The PvpKill query itself should be updated to match, or refactored to call the shared helper if it becomes accessible. This is a correctness fix, not a behaviour change under normal single-session conditions.

---

## Files changed

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/ActivityEvent.cs` | Add `CounterpartyAccountId` optional field |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Add gate guard + `GetMostRecentSessionIp` helper |
| `src/Perpetuum/Accounting/Characters/CharacterWallet.cs` | Remove `marketSell` from `NicEarned`, `marketBuy` from `NicSpent` |
| `src/Perpetuum/Services/MarketEngine/Market.cs` | Add explicit `RecordActivity` calls at player-to-player fill sites; add no-counterparty calls for vendor fills |
| `src/Perpetuum/Services/MissionEngine/TransportAssignments/TransportAssignment.cs` | Add `CounterpartyAccountId` to all 7 existing calls |

---

## Manual validation steps

1. Start a season with `NicEarned` and `NicSpent` rates configured.
2. **Same-IP player trade:** Log in two accounts from the same machine. Have one list a sell order, the other buy it. Verify neither earns season points for the transaction.
3. **Different-IP player trade:** Repeat with accounts from different machines (or different IPs). Verify both earn season points normally.
4. **Vendor trade:** Buy an item from a vendor sell order. Verify the buyer earns `NicSpent` points (no gate — no player counterparty).
5. **Transport assignment, same IP:** Submit and complete a transport assignment between two accounts on the same machine. Verify neither earns season points for the reward/collateral payments.
6. **Transport assignment, different IP:** Repeat with different-machine accounts. Verify season points are earned.
7. **PvpKill regression:** Verify PvP kills still gate correctly on same-IP (no regression from helper extraction).
8. **No active season:** Verify all NIC transactions complete without error when no season is active.

---

## Risks and regressions

- **Vendor fill regression:** Removing `marketSell`/`marketBuy` from the wallet switch means vendor fill paths lose NIC recording unless explicit calls are added. The spec requires explicit no-counterparty calls for vendor paths — this must not be missed.
- **NAT false positives:** Two legitimate players behind the same NAT (office, household) will be incorrectly gated. This is a known limitation documented in the backlog note. Operators can disable `NicEarned`/`NicSpent` activity rates entirely for seasons where this is a concern.
- **Missing `TOP 1` in PvpKill:** The existing PvpKill gate has the same non-deterministic query; fixing it in the shared helper does not fix the PvpKill call site itself. Both should be updated together.
