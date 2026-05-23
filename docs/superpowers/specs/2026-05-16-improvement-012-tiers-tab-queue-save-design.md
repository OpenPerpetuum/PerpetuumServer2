# IMPROVEMENT-012 — Seasons Tiers Tab: On-the-fly Save via Queue Save

**Date:** 2026-05-16
**Status:** Approved
**Area:** Seasons / Admin Tool

---

## Problem

The Tiers tab in the Seasons Admin Tool uses an inconsistent save mechanic compared to the Activity Rates and Objectives tabs:

- **Activity Rates / Objectives:** Each row has a "Queue Save" button. The user edits the row then explicitly queues the change. All queued changes are committed together in a single transaction.
- **Tiers (current):** "+ Add Tier" immediately auto-queues an INSERT. Editing an existing tier's properties has no save path — `SeasonChanges.BuildUpdateTier()` exists but is never called. Grid edits to existing tiers are silently discarded.

---

## Goal

Make the Tiers tab behave identically to the Objectives tab:
- Adding a tier defers the INSERT until "Queue Save" is clicked.
- Editing an existing tier and clicking "Queue Save" queues an UPDATE.
- All tier changes participate in the shared single-transaction commit alongside rates and objectives.

---

## Scope

Three targeted changes. No new abstractions. No schema changes. No changes to `SeasonChanges.cs`.

| Location | Change |
|---|---|
| `SeasonDetailViewModel.AddTier()` | Remove the immediate `_queue.Add` call. Create and add the row to `Tiers` only. |
| `SeasonDetailViewModel` | Add `QueueSaveTierCommand` / `QueueSaveTier(SeasonTierRow? row)` method. |
| `SeasonDetailView.xaml` — Tiers DataGrid | Add "Queue Save" `DataGridTemplateColumn` bound to the new command. |

---

## Save Flow (after change)

1. User clicks **"+ Add Tier"** → row created with `Id = 0`, defaults applied, added to `Tiers` collection. Nothing queued.
2. User edits TierNumber, TierName, PointsRequired, RewardPackage in the grid.
3. User clicks **"Queue Save"** on the row:
   - `row.Id == 0` → `SeasonChanges.BuildInsertTier(row)` queued.
   - `row.Id > 0` → `SeasonChanges.BuildUpdateTier(row)` queued.
   - `StatusMessage` updated: `"Queued [INSERT|UPDATE] for tier '{tierName}'."`.
4. User clicks **Commit** → `SqlScriptBuilder` wraps all queued changes in a single transaction.

Remove flow is unchanged — "Remove" on an existing tier queues a DELETE.

---

## Edge Cases

- **Queue Save before editing:** Produces an INSERT/UPDATE with the default values currently in the row. User is responsible for filling values before queueing — same expectation as Objectives.
- **Queue Save called twice on the same row:** Two SQL statements for the same tier appear in the change script. The second write wins at commit time. Harmless; consistent with existing tab behaviour. See [[IMPROVEMENT-016]] for future deduplication.
- **Remove after Queue Save:** Queues DELETE after a prior INSERT/UPDATE. Net effect at commit: no row (INSERT+DELETE) or updated-then-deleted row (UPDATE+DELETE). Acceptable edge case; consistent with existing tabs.
- **Existing tiers (Id > 0):** Loaded from DB. Edit + "Queue Save" emits UPDATE via `BuildUpdateTier`.

---

## Out of Scope

- ChangeQueue deduplication (tracked as [[IMPROVEMENT-016]]).
- Configurable tier reset time.
- Any changes to tier DB schema.

---

## Files Affected

- `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`
- `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

## Files Referenced (no changes)

- `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` — `BuildInsertTier`, `BuildUpdateTier` already present.
- `src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs` — `Id`, `IsNew` fields already present.
- `src/Perpetuum.AdminTool/Editing/ChangeQueue.cs` — no change.
