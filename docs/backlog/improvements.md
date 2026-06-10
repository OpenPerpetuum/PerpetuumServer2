# Last ID used

040

## IMPROVEMENT-040 - AutoMarket: Decouple Raw Material Coverage from Trade List

Status: DONE
Priority: CRITICAL
Area: AutoMarket / Economy

### Implementation Summary

Implemented on branch `p36.6` (commits `715a43d`–`1d1dfa9`).

- **DB migration:** `docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql` — creates `automarket_rawmat_overrides`, `automarket_rawmat_weekly_tracking`, inserts `weekly_rawmat_cap_default = 500000000`, adds `IX_rmp_on_name`, renames view, creates `sp_RecordRawMatWeeklyPurchased`
- **View rename:** `v_required_raw_materials` → `v_trade_list_raw_material_demand` (demand signal only)
- **`v_all_production_costs`:** `raw_resources` CTE now scans entitydefaults directly (cf_raw_material bitmask)
- **`recalculate_raw_material_prices`:** material enumeration expanded to all cf_raw_material items
- **`usp_RefreshAutoMarketOrders`:** `#covered_rawmats` replaces `#raw_materials`; Steps 4+5 are cap-driven
- **`Market.cs`:** `sp_RecordRawMatWeeklyPurchased` called at 3 `FulfillSellOrderInstantly` sites
- **AdminTool:** Raw Materials tab (VM + View), Statistics Pricing Trace columns, repository updates
- **Recipe-graph demand signal:** analysed and rejected — C-only approach (gather-volume proxy) chosen; max scarcity for ungathered materials is self-correcting on a low-population server

### Problem

The current AutoMarket system identifies raw materials exclusively by recursively exploding items in `market_orders_configuration` (the trade list). This creates tight coupling: materials for items outside the trade list get no market support, and any newly added craftable item requires a manual trade list update before its raw material supply chain becomes active. The trade list's role is also overloaded — it currently drives both finished product orders and raw material demand calculations.

### Proposed Architecture

Decouple raw material coverage from the trade list:

- **Raw materials** — identified from `entitydefaults` (not from the trade list). Prices calculated independently. Infinite-style buy/sell orders placed for all qualifying materials with a **configurable weekly cap per material** (see Impact Analysis below).
- **Trade list** — scoped to finished product buy/sell/buyback orders only. Product prices derived from raw material prices (cost-plus), not set independently.

This inverts the current dependency:

```
Current:   trade list → raw material identification → raw material prices → orders
Proposed:  entitydefaults → raw material prices → orders (capped)
           trade list + raw material prices → product prices → orders
```

### Raw Material Coverage Filter

Use `entitydefaults` to enumerate qualifying raw materials. Filter criteria (exact requirements TBD during implementation):

- `enabled = 1`
- `hidden = 0`
- Category matches raw material category flag(s) — filtered by category ID exact match or category tree traversal (children of raw material category nodes)

Avoids coverage explosion from legacy/unobtainable items while automatically including newly added materials that meet the criteria.

### Price Calculation

Retain and extend the existing formula from IMPROVEMENT-030:

```
price = plasma_anchor × supply_demand_ratio × pvp_risk_multiplier
```

**PvP risk multiplier:** Preserved as-is. Materials gathered predominantly in PvP zones retain their risk premium.

**Supply/demand ratio:** Retain the existing formula (`daily_demand / daily_supply_avg`, clamped to `[ds_ratio_min, ds_ratio_max]`). Investigate whether adding recipe-graph-derived demand (from the `components` table) as a supplementary signal to the S/D ratio improves pricing accuracy. If the analysis shows negligible benefit (e.g. because recipe demand is already implicit in gather volume on a functioning server), this addition may be skipped. Document the decision.

**Recalculation cadence:** Daily, same as the existing 24-hour refresh cycle introduced in IMPROVEMENT-030. Startup-only recalculation was considered and rejected — prices must track the live economy between restarts.

### Weekly Cap Per Material — Impact Analysis Required

Replace the current arrangement (fixed 10,000,000 quantity for sell orders; budget-capped buy orders) with a **configurable weekly quantity cap per material**. Before implementation, analyze:

1. **NIC injection bound** — what weekly cap value keeps raw material buy-side NIC injection comparable to or lower than the current `daily_rawmat_budget_nic` regime?
2. **Supply adequacy** — does a weekly cap prevent the market from running dry for high-demand materials during active play periods?
3. **Per-material vs global cap** — whether a single global cap or per-category/per-material overrides are needed for balance.
4. **Interaction with daily budget** — determine whether the weekly cap replaces or works alongside the existing daily NIC budget guard.

The daily NIC budget cap must remain as a hard guardrail until the impact analysis confirms the weekly cap is safe.

### Affected Systems

- `recalculate_raw_material_prices` stored procedure — extend material enumeration to use `entitydefaults` filter
- `usp_RefreshAutoMarketOrders` — step 4 (raw material buy orders) and step 5 (raw material sell orders) reworked
- `v_required_raw_materials` view — may be retired or repurposed as a product-cost calculation helper
- `automarket_config` table — add `weekly_rawmat_cap_per_material` and category filter parameters
- AdminTool AutoMarket panel (IMPROVEMENT-031) — expose new cap config and coverage filter parameters

### Notes

- Cross-reference IMPROVEMENT-030 (AutoMarket overhaul) — builds on its pricing formula and config table.
- Cross-reference IMPROVEMENT-031 (AutoMarket AdminTool) — Config tab and Statistics tab need updates for new parameters.
- Cross-reference IMPROVEMENT-035 (player order signal) — raw material coverage expansion increases the surface area where player orders could manipulate S/D ratios; revisit IMPROVEMENT-035 deferral conditions after this is shipped.
- The recipe-graph demand signal analysis (S/D ratio extension) should be done before coding the pricing procedure — if the analysis is inconclusive or shows risk, skip it and document why.
- Category flag filter criteria (exact category IDs and whether to include children) must be confirmed against `entitydefaults` live data before generating the SQL filter.

## IMPROVEMENT-039 - Add economy health statistics beyond NIC flow reporting

Status: DONE
Priority: HIGH
Area: Admin Tool / Economy

### Description
The current economy report (IMPROVEMENT-034) tracks NIC flows (injections and sinks) but flow data alone is insufficient to evaluate true economy health. NIC flows show the rate of money creation and destruction — they do not show whether that money is circulating, concentrating, or causing real price inflation. Additional statistics are needed to give operators a complete diagnostic picture.

### Impact
Without supplementary statistics, operators cannot distinguish between a healthy growing economy and a stagnating inflationary one with the same NIC flow numbers. These metrics close the gap between "what is happening to NIC" and "what is happening to the economy."

### Proposed Statistics

#### Money Supply
- **Total NIC in circulation** — sum of all player and corporation wallet balances. Without this, the net surplus figures have no denominator; a +7.5B monthly surplus means something very different on a 10B vs 1T money supply.
- **Money supply trend** — total NIC in circulation over time (daily/weekly snapshots), the clearest single indicator of inflation pressure.

#### Wealth Distribution
- **Top 10 / top 1% wealth share** — what fraction of total NIC is held by the wealthiest players. High concentration means most players feel poor even if aggregate NIC is growing.
- **Median player wallet balance** — more representative than mean; large outliers skew mean heavily.
- **Idle NIC** — NIC held in wallets untouched for 30+ days. High idle NIC suggests players have nothing to spend it on.

#### Market Health
- **Market price index** — average transaction price for a basket of common goods (raw materials, common robot parts, basic consumables) tracked over time. This is the direct inflation indicator — rising prices confirm what NIC flow data only implies.
- **Market velocity** — total NIC value of completed market transactions per day. Low velocity with high money supply = hoarding, not circulation.
- **Unsold listing age distribution** — how long goods sit on the market before selling or expiring. Aging listings indicate insufficient demand.
- **AutoMarket vs player market share** — what percentage of economic activity is AutoMarket-driven vs player-driven. High AutoMarket share on a low-pop server is expected; a declining player share over time signals disengagement.

#### Sink Effectiveness
- **NIC sink breakdown per activity type** — how much each sink category contributes per active player, normalized by session count or login days. Reveals which sinks are load-bearing vs cosmetic.
- **Insurance coverage rate** — percentage of active robots that currently have insurance. Near-zero confirms the insurance system is effectively unused.

### Notes
- Cross-reference IMPROVEMENT-034 (NIC flow report) — these statistics extend that panel, not replace it
- Total NIC in circulation is the highest-priority addition; without it, all flow data lacks context
- Market price index requires selecting a representative basket of goods — coordinate with game design intent
- Some of these (wealth distribution, idle NIC) may have privacy/fairness implications if exposed to players; restrict to admin view only

### Implementation

Implemented as four tabs in the Economy Admin Tool panel (branch p36.5, commits 930c727 → f9a5cc1):

**Tab 1 — NIC Flow:** Existing panel extracted to `EconomyNicFlowViewModel` / `EconomyNicFlowView`.

**Tab 2 — Money Supply & Wealth:** Total NIC in circulation (characters.credit + corporations.wallet), 90-day trend from `economy_daily_snapshot` (written daily by `EconomySnapshotService`), top-10 wealth leaderboard, median wallet, top-1% share, idle NIC (≥30 days inactive).

**Tab 3 — Market Health:** Market velocity (daily NIC transacted from `marketaverageprices`), weighted price index for a configurable basket of items (`economy_price_index_basket`), live listing age distribution, AutoMarket vs player order mix. Basket items are editable via the global ChangeQueue.

**Tab 4 — Sink Effectiveness:** NIC-out per category normalized by 30-day active player count, insurance coverage rate.

**Server:** `EconomySnapshotService : IProcess` fires `usp_RecordEconomySnapshot` on startup and daily (idempotent MERGE on snapshot_date).

**DB migration required:** `docs/db_structure/migrations/IMPROVEMENT-039-economy-health.sql`

Design spec: `docs/superpowers/specs/2026-06-03-economy-health-stats-design.md`
Implementation plan: `docs/superpowers/plans/2026-06-03-economy-health-stats.md`

---

## IMPROVEMENT-038 - Explore and expand AutoMarket Plasma rate tuning tools

Status: TODO
Priority: HIGH
Area: AutoMarket / Economy / Admin Tool

### Description
AutoMarket Plasma is the single largest NIC injection source (~3.99B NIC in the last 30 days, ~46% of all injections). Operators currently have no confirmed tooling to tune plasma buy rates. Existing tools must be audited and, if insufficient, new admin controls must be added to allow safe, incremental rate adjustment without code deployments.

### Impact
Without operator control over plasma rates, the server has no practical lever to reduce the dominant inflation driver short of a code change and redeployment. Tunable rates would allow economy balancing to happen at runtime in response to observed NIC flow data.

### Investigation Scope
1. Audit existing admin tools and configuration for any plasma rate controls (rate multipliers, price floors/ceilings, per-item overrides)
2. Check whether plasma rates are hardcoded, database-driven, or formula-based
3. Determine what inputs drive the current rate (supply/demand history, fixed table, dynamic calculation)
4. Assess whether existing controls are sufficient for meaningful economy tuning

### Proposed additions (if controls are missing or insufficient)
- Admin Tool UI controls to adjust plasma rate multiplier or absolute price per commodity
- Per-item or per-category rate overrides stored in the database (not hardcoded)
- Rate change audit log so operators can track adjustments and correlate with economy report data
- Guardrails: min/max clamps to prevent accidental zero-rate or runaway injection

### Notes
- Cross-reference IMPROVEMENT-034 (economy report) — plasma NIC flows are already visible there; rate controls would close the loop from observation to action
- Cross-reference IMPROVEMENT-035 (AutoMarket supply/demand) — any rate tuning should remain consistent with the existing ds_min/ds_max clamping architecture
- Changes to plasma rates have direct, immediate impact on the largest injection source — changes should be incremental and monitored

---

## IMPROVEMENT-037 - Investigate System Credits & Refunds NIC injection source

Status: TODO
Priority: HIGH
Area: Economy / NIC Flows

### Description
The economy report shows System Credits & Refunds injected ~2.87B NIC in the last 30 days — roughly 33% of all server-side NIC injections. This is the second-largest injection source after AutoMarket Plasma, yet its origin and legitimacy are unclear. A full investigation is required.

### Impact
At ~95M NIC/day, this source alone is a significant inflation driver. If it represents legitimate gameplay mechanics (NPC trade refunds, mission cancellations, system compensations) it should be documented and tuned. If it is a bug, misconfiguration, or exploitable pathway, it must be fixed immediately.

### Investigation Scope
1. Identify all code paths that record a transaction under the "System Credits & Refunds" category
2. Determine whether each path is intentional design or a side-effect/bug
3. Check whether players can trigger refunds repeatedly or artificially (exploit vector)
4. Assess the expected volume — is 2.87B/month reasonable given current player activity, or anomalously high?
5. Cross-reference with player activity logs to see if a small number of accounts are responsible for a disproportionate share

### Notes
- Cross-reference IMPROVEMENT-034 (economy report) — this source is already tracked there
- If the source is legitimate but oversized, consider capping or rate-limiting refund eligibility
- If exploit-driven, cross-reference ISSUE backlog for related economy abuse issues

---

## IMPROVEMENT-036 - Investigate and improve the insurance system

Status: DONE
Priority: HIGH
Area: Economy / Insurance

### Description
The economy report shows Insurance Payouts = 0 for the last 30 days while Insurance Fees are near zero (70k/30d). The insurance system is either broken, unused, or being bypassed. This warrants a full investigation into how the system works, whether players can exploit it, and how it can be improved as a meaningful NIC sink.

### Impact
Insurance was presumably designed as a significant NIC sink (loss recovery funded by premium fees). With it effectively dormant, the economy loses a major pressure valve, contributing to ~7.58B NIC/month surplus and long-term inflation. Restoring or redesigning it could meaningfully reduce inflation without punishing active gameplay.

### Investigation Scope
1. Trace the full insurance lifecycle: premium charging, policy storage, payout triggering, NIC flow
2. Determine why payouts are zero — broken trigger, player avoidance, or design gap
3. Identify exploit vectors: avoiding premiums while still being eligible for payouts, double-claiming, gaming the payout calculation
4. Assess whether the current payout/fee ratio creates a net NIC sink or net NIC source
5. Propose rebalancing or redesign to make insurance a reliable and meaningful sink

### Proposed Improvements (to evaluate)
- Ensure insurance fees are charged consistently on all eligible assets
- Ensure payout triggers fire correctly on robot destruction
- Cap payout-to-fee ratio to guarantee insurance is always net-negative for the economy
- Consider making insurance opt-out rather than opt-in to increase coverage and fee collection
- Add insurance NIC flows to the economy report for ongoing monitoring

### Notes
- Cross-reference IMPROVEMENT-034 (economy report) — insurance flows are already surfaced there, confirming the zero-payout anomaly
- Insurance Fees (NIC Out) and Insurance Payouts (NIC In) must both be audited — a payout exceeding fees collected would make insurance a net injector, worsening inflation

### Implementation

Implemented on branch p36.5 (commits 36cf271 → e0e1dac):

- `insurance_config` table: `fee_pct = 0.10`, `payout_pct = 0.08` (operator-tunable)
- `usp_RecalculateInsurancePrices`: MERGE from `v_all_production_costs` into `insuranceprices`; guards against `payout_pct >= fee_pct`
- `InsurancePriceRefreshService`: daily auto-refresh + startup run, flushes in-memory cache after each run
- `InsuraceFacility`: fee extension bonus (`ext_production_insurance_fee`) now applied at both purchase and quote
- Dead static multipliers (`InsuranceFeeMultiplier`, `InsurancePayOutMultiplier`) removed from `InsuranceHelper`
- Migration deletes stale `insurance` policies, then seeds correct prices; apply while server is OFFLINE
- Admin Tool: "Insurance" tab (5th in Economy panel) with config editor, price table, Reload and Recalculate Now buttons

---

## IMPROVEMENT-035 - Factor player buy/sell orders into AutoMarket supply/demand rate calculation

Status: DEFERRED
Priority: MEDIUM
Area: AutoMarket / Economy

### Description
AutoMarket currently calculates supply and demand rates using only its own transaction history. Player-created buy and sell orders on the market represent real demand and supply signals that AutoMarket ignores. Including them in the rate calculation could produce more accurate pricing.

### Analysis Outcome (2026-06-03)

Full brainstorming and economic modelling completed. Decision: **defer**.

**Benefit is small in practice.** On a low-population server, player raw material order volume is thin — the signal would be near-zero most of the time, producing behaviour identical to today. The improvement only matters at population peaks.

**The existing system already captures most of the signal indirectly.** Product sell-through → `automarket_unsold_leftovers` → AutoMarket buys more raw materials next refresh. This indirect loop is slower but manipulation-proof.

**Manipulation guard is structurally weak.** A 30-minute age filter stops rapid pump-cancel cycles but not fake 1-NIC buy orders left open for 24 hours, which cost nothing to place. Closing that hole properly requires either a price floor on counted orders (circular dependency on the price being computed) or per-character quantity caps — roughly doubling implementation complexity.

**Manipulation ceiling:** ds_min/ds_max clamp [0.25, 4.0] and `daily_rawmat_budget_nic` bound the worst-case damage, but a coordinated attack on all raw materials simultaneously is a systemic risk.

### Conditions to Revisit

Reconsider only when:
1. IMPROVEMENT-034 (NIC flow statistics) is in place and provides operator visibility into raw material price trends.
2. That data shows a concrete, sustained divergence between AutoMarket raw material prices and player market prices that the existing indirect feedback loop is not correcting.
3. Population is high enough for player order volume to constitute a meaningful signal (not just noise).

### Notes
- Cross-reference ISSUE-022 (order placement exploit) — same class of abuse applies here.
- Cross-reference IMPROVEMENT-034 — prerequisite for gathering the data needed to justify revisiting.

---

## IMPROVEMENT-034 - Expand AutoMarket NIC flow statistics in Admin Tool

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Economy

### Description
The AutoMarket tab in the Admin Tool currently shows limited statistics. It needs a full NIC flow breakdown — both income and outgoing — to give operators a complete picture of the server economy. This includes, but is not limited to: market taxes, transaction fees, mission rewards, crafting costs, repair fees, insurance payouts, and any other server-side NIC sources or sinks.

### Impact
Without full NIC flow visibility, operators cannot diagnose inflation, NIC sinks underperforming, or unexpected injections. A comprehensive view enables data-driven economy tuning and early detection of exploits or misconfigurations.

### Implementation

Implemented as a new top-level **Economy** panel in the Admin Tool (separate from AutoMarket). Data sourced from existing `charactertransactions` and `corporationtransactions` tables (classified by `transactiontype` into named categories) plus `plasma_sold` and `rawmat_purchased` for AutoMarket flows. No schema changes or server-side code changes required.

**NIC In categories:** Mission Rewards, Insurance Payouts, Intrusion Income, AutoMarket Plasma, System Credits & Refunds.

**NIC Out categories:** Market Fees & Taxes, Production Costs, Repair Costs, Insurance Fees, Infrastructure Costs, Extension Learning, Spark Costs, Corporate & Alliance Fees, Other Fees, AutoMarket Raw Materials.

Time periods: Today / Last 7 Days / Last 30 Days / All Time. Net balance shown with green/red coloring.

Design spec: `docs/superpowers/specs/2026-06-03-economy-nic-flow-design.md`
Implementation plan: `docs/superpowers/plans/2026-06-03-economy-nic-flow.md`
Branch: p36.5 (commits 9a3a1b2 → a147494)

### Notes
- `SiegeFee(37)`, `SiegeFeeRefund(38)`, and `SiegePoolPayback(41)` are unclassified — siege subsystem appears dormant; add to appropriate categories when siege activity resumes.
- `transactiondate` uses `getdate()` (local server time); queries compare against `GETUTCDATE()`. Accurate as long as SQL Server runs in UTC (standard deployment).
- Cross-reference IMPROVEMENT-035 — this panel provides the operator visibility prerequisite for revisiting player order signal in AutoMarket pricing.

---

## IMPROVEMENT-002 - Refactor Hardcoded System Characters and Channels

Status: TODO
Priority: HIGH
Area: Chat / Seasons / Infrastructure

### Description
System characters (e.g. `[OPP] Announcer`) and system channels (e.g. `Seasons Info`) are currently referenced by hardcoded name strings scattered across the codebase. These should be centralised and driven by configuration or well-defined constants so they can be changed without touching multiple call sites.

### Impact
Hardcoded strings are fragile: a rename or new deployment environment requires hunting down every occurrence. Centralising them reduces maintenance cost, eliminates copy-paste errors, and makes the system easier to extend (e.g. adding a new announcement channel for a different feature).

### Proposed Implementation
- Audit the codebase for all string literals that reference system character names and channel names.
- Introduce a `SystemCharacters` static class (or config-backed equivalent) with named constants / properties for each system character (e.g. `SystemCharacters.Announcer`).
- Introduce a `SystemChannels` static class (or config-backed equivalent) with named constants / properties for each system channel (e.g. `SystemChannels.SeasonsInfo`).
- Replace all hardcoded occurrences with references to these constants.
- Where values should be operator-configurable (e.g. different server deployments), back them with `gameConfig` or a dedicated config section rather than compile-time constants.
- Update the Admin Tool if it surfaces any of these names directly.

### Notes
Audit starting points: seasons announcement code, chat subsystem, any admin tool chat/broadcast helpers.
Keep backward compatibility with existing DB channel records — constants should match stored names unless a migration is also performed.

---

## IMPROVEMENT-007 - NPC Rank System

Status: TODO
Priority: LOW
Area: NPCs

### Description
Add a manually assigned rank field to NPC definitions so that NPCs can be categorised and distinguished by rank level (e.g. grunt, elite, commander, boss). Rank should be a lightweight data attribute — no automated assignment logic.

### Impact
Provides a clear, queryable signal for distinguishing NPC threat levels without relying on inferred stats or naming conventions. Useful for display, loot table differentiation, season activity targeting (e.g. "kill 5 elite NPCs"), and future AI behaviour tuning.

### Proposed Implementation
- Add a `rank` column (tinyint or small enum-backed int, nullable) to the NPC definition table; `NULL` means unranked.
- Define a fixed rank scale (e.g. 0 = Minion, 1 = Standard, 2 = Elite, 3 = Commander, 4 = Boss) as named constants in code — consult existing NPC categorisation patterns before finalising values.
- Rank is assigned manually via the Admin Tool or direct DB edit; no automated inference.
- Expose rank in the NPC definition read path so it is available to callers (loot, season activity handlers, UI).
- Admin Tool: surface the rank field in the NPC editor as a dropdown.

### Notes
Keep the rank scale small and stable — it will be referenced by season activity configs and potentially loot rules, so changes after rollout are costly.
If season activity types need to filter by NPC rank (see [[IMPROVEMENT-005]]), the rank value must be accessible at the point where kill events are emitted.

---

## IMPROVEMENT-008 - NPC Role System

Status: TODO
Priority: LOW
Area: NPCs / AI

### Description
Add a role field to NPC definitions to classify each NPC by its intended combat function (e.g. Combat, Ewar, Support). Roles are assigned manually and serve as a semantic tag for AI behaviour selection, season activity filtering, and general NPC distinction.

### Impact
Role classification gives AI subsystems and content systems a stable, queryable signal for NPC function without relying on module loadout inference or naming conventions. Enables future AI improvements (e.g. role-aware targeting, formation logic) and allows season objectives to target specific NPC roles (e.g. "neutralise 3 Ewar NPCs").

### Proposed Implementation
- Add a `role` column (tinyint or small enum-backed int, nullable) to the NPC definition table; `NULL` means no role assigned.
- Define an initial role set as named constants: Combat, Ewar, Support — keep the set open to extension but stable at the value level.
- Role is assigned manually via Admin Tool or direct DB edit; no automated inference.
- Expose role in the NPC definition read path so it is available to AI handlers, loot logic, and season activity handlers.
- Admin Tool: surface the role field in the NPC editor as a dropdown alongside the rank field (see [[IMPROVEMENT-007]]).
- AI subsystem: role is available as a hint for future behaviour selection — no behavioural changes required in this improvement, just the data plumbing.

### Notes
Role and rank (see [[IMPROVEMENT-007]]) are complementary attributes — implement consistently (same table, same read path, same Admin Tool panel).
If season activity types need to filter by NPC role (see [[IMPROVEMENT-005]]), role must be accessible at the point where kill events are emitted.
Keep the initial role set conservative; adding roles later is cheaper than changing existing ones after downstream systems reference them.

---

## IMPROVEMENT-010 - Seasons Scoring Balancing Tab

Status: TODO
Priority: LOW
Area: Seasons / Admin Tool

### Description
Add a Scoring Balancing tab to the season editor in the Admin Tool. The tab presents a consolidated view of tiers, objectives, activity point rates, and the computed number of activities required per objective — all editable inline — so season designers can tune scoring balance without cross-referencing multiple screens or raw DB rows.

### Impact
Season balance currently requires manual cross-referencing of tier thresholds, objective point values, and activity rates in separate views or directly in the DB. A unified balancing surface reduces errors, makes trade-offs immediately visible, and significantly speeds up the iteration loop for season design.

### Proposed Implementation
- **Tiers panel** — list all tiers for the season (name, point threshold, reward); editable inline.
- **Objectives panel** — list all objectives (name, activity type, target filter if any, point value); editable inline; derived column shows point contribution as a percentage of the next tier threshold.
- **Activity Rates panel** — list all activity types configured for the season with their point-per-event rate; editable inline.
- **Activities-to-Objective column** — for each objective, compute and display `objective_target / activity_rate` (i.e. how many raw activity events are needed to complete it at the current rate); updates live as rates or targets are edited.
- All edits are staged locally and applied in a single save transaction to avoid partial state.
- Read-only summary row at the bottom: estimated total points available if all objectives are completed, compared against the top tier threshold — flags imbalance if the gap is large.

### Notes
The activities-to-objective computation is a display convenience; the authoritative values remain the stored point rates and objective targets.
Depends on [[IMPROVEMENT-005]] for the full set of activity types surfaced in the rates panel.
Depends on [[IMPROVEMENT-009]] for targeted objectives appearing in the objectives panel with their filter displayed.
Edits made here must write through the same save paths used by the individual objective and activity rate editors — no parallel write logic.

---

## IMPROVEMENT-013 - Daily objectives grant their own reward packages on completion

Status: TODO
Priority: MEDIUM
Area: Seasons / Objectives

### Description
When a player completes a daily objective they should receive a dedicated reward package, separate from and in addition to any season point accumulation. Each daily objective should have a configurable reward package (items, NIC, or other reward types) that is granted immediately on completion.

### Impact
Without per-completion rewards, daily objectives only contribute points toward season tiers — offering no immediate gratification. Instant reward packages make daily objectives more compelling, encourage consistent daily engagement, and allow designers to tune short-term incentives independently of long-term tier progression.

### Proposed Implementation
- Extend the daily objective definition to include an optional `reward_package_id` (or equivalent structured reward payload) specifying what is granted on completion.
- On objective completion, trigger the reward grant pipeline with the associated package — reuse the existing reward distribution mechanism (used for season tier rewards or similar) rather than introducing a new path.
- Reward packages should be configurable per objective and per season; different daily objectives within the same season may grant different packages.
- If an objective has no reward package configured, completion behaves as today (points only) — no breaking change to existing objectives.
- Admin Tool: surface the reward package field in the daily objective editor.

### Notes
Depends on [[IMPROVEMENT-006]] — daily objectives infrastructure must exist before per-completion rewards can be wired in.
Reward packages must be granted exactly once per completion per character per day — idempotency is critical given the daily reset cycle.
Consult the existing tier reward grant path for the reward package schema and delivery mechanism before designing the new hook.

---

## IMPROVEMENT-014 - Standalone daily objectives/missions outside of Seasons

Status: TODO
Priority: LOW
Area: Objectives / Missions

### Description
Introduce a daily objective (or daily mission) system that operates independently of the Seasons system. These objectives generate no season points and have no season dependency — they simply reset daily and grant reward packages on completion, available to all players at all times regardless of whether a season is active.

### Impact
Season-tied daily objectives are only meaningful during an active season, leaving a gap in daily engagement loops during off-season periods. A standalone daily objective system provides consistent daily incentives year-round, retains player engagement between seasons, and caters to players who are not focused on competitive season rankings.

### Proposed Implementation
- Design the standalone daily objective system as a distinct subsystem from Seasons — it should not depend on a season being active, should not write to season activity or point tables, and should have its own objective definitions, completion tracking, and daily reset scheduling.
- Reuse the daily reset scheduler and objective completion/reward grant mechanisms from [[IMPROVEMENT-006]] and [[IMPROVEMENT-013]] where possible — extract shared infrastructure rather than duplicating it.
- Objective definitions: activity type, target filter (optional, see [[IMPROVEMENT-009]] patterns), completion threshold, reward package.
- Completion tracking: per-character, scoped to the current day's reset window; idempotent reset at UTC midnight (or configurable reset time).
- Reward grant: on completion, deliver the configured reward package via the existing reward distribution path — no points emitted.
- Admin Tool: a dedicated section for managing standalone daily objective templates (create, edit, enable/disable, assign reward packages); separate from the Seasons objective editor.

### Notes
The absence of point generation is intentional and must be enforced — these objectives must not accidentally write to any season scoring table.
If the daily reset infrastructure from [[IMPROVEMENT-006]] is not yet built, this system should share that implementation rather than introducing a parallel reset scheduler.
Consider whether standalone daily objectives should be visible in the same in-game UI as season daily objectives, or in a separate panel — a clear UX distinction prevents player confusion about what generates season points.

---

## IMPROVEMENT-015 - Seasons: Distance Travelled Activity Type

Status: TODO
Priority: LOW
Area: Seasons / Activities

### Problem
Distance travelled was scoped out of [[IMPROVEMENT-005]] due to zone-thread-safety concerns. There is no existing hook point for movement/distance metrics in the zone update loop, and per-movement-event `RecordActivity` calls would be too frequent.

### Impact
Without this type, season designers cannot reward exploration or movement-intensive playstyles. It is a lower-priority gap since the 12 types from IMPROVEMENT-005 already cover most playstyle categories.

### Proposed Fix
- Instrument the zone movement system to accumulate distance per character over a configurable tick interval (e.g. every 5 seconds)
- At the end of each interval, emit a single `RecordActivity(characterId, DistanceTravelled, accumulatedDistance)` call
- The accumulator must be zone-thread-safe — stored per-unit alongside other movement state, written only from the zone update loop
- Amount unit: metres (or internal distance units); `unit_scale` in rates handles point conversion

### Notes
Accumulation interval should be configurable to avoid excessive DB writes in high-population zones.
Must not introduce blocking or allocation in the hot movement path — accumulate, don't write inline.
Consult `docs/CONCERNS.md` zone update loop constraints before implementation.

---

## IMPROVEMENT-016 - Admin Tool: ChangeQueue deduplication

Status: TODO
Priority: LOW
Area: Admin Tool / Editing

### Description
The `ChangeQueue` does not deduplicate queued changes. If the user clicks "Queue Save" on the same row multiple times, multiple SQL statements for the same entity accumulate in the script. The last write wins at commit time, so correctness is preserved, but the script is noisier than necessary and harder to audit.

### Impact
Low. The issue only manifests if a user repeatedly clicks "Queue Save" on the same row within a session. Scripts remain correct; they are just verbose. Affects all tabs that use "Queue Save": Activity Rates, Objectives, Tiers (after IMPROVEMENT-012).

### Proposed Fix
- Give each queued change a stable key composed of table + primary key (e.g. `"season_tiers:{seasonId}:{tierId}"`).
- When a change with the same key is added, replace the existing entry rather than appending.
- Keep the existing `ObservableCollection<IPendingChange>` as the backing store; deduplicate on `Add`.
- Update `IPendingChange` with an optional `Key` property; `RawSqlChange` exposes it; `ChangeQueue.Add` checks for collision.

### Notes
Depends on [[IMPROVEMENT-012]] being complete — Tiers tab must use the queue before deduplication applies to it.
Key must be stable across multiple `Queue Save` clicks on the same row, not a generated GUID.
Destructive changes (DELETE) should also replace any prior non-destructive change for the same key.

---

## IMPROVEMENT-024 - Server Restart: Daily Objective Announcement and Admin Tool Statistics

Status: DONE
Priority: HIGH
Area: Seasons / Objectives / Admin Tool

### Description
Two related improvements to daily objective visibility:

1. **Server restart announcement** — on startup, if an active season with daily objectives is configured, announce the current day's active objectives to all players via the existing announcement channel. If the daily pool for the current day has not yet been generated (e.g. first query after midnight on a fresh restart), run the pooling selection logic (see [[IMPROVEMENT-022]]) before announcing, so the announcement reflects the actual set players will see.

2. **Admin Tool Season Statistics tab** — surface the current active daily objective set and per-objective completion counts on the Season Statistics tab. For each daily objective active today, display: objective name, activity type, target (if any), and the number of distinct characters who have completed it on the current day.

### Impact
Without the announcement, players who log in after a server restart have no immediate indication that daily objectives are available or what they are — they must navigate to the objectives panel themselves. For the Admin Tool, operators currently have no at-a-glance view of how many players are completing each daily objective on a given day, making it impossible to assess engagement or spot broken objectives without raw DB queries.

### Proposed Implementation

**Server restart announcement:**
- Wire into `SeasonService.RefreshCache`, which is already called on startup and whenever the season cache is invalidated.
- After the cache is refreshed, check whether an active season exists with `is_daily` objectives configured for the current UTC day.
- If the daily pool for today has not yet been materialised (no rows in `season_objective_progress` for today's `day_window` and active season), trigger the deterministic pool selection from [[IMPROVEMENT-022]] first.
- Compose an announcement message listing the active daily objective names (and targets where applicable), then dispatch it via the existing Seasons Info channel / Announcer character — reuse the announcement path used for season start/end notifications.
- If no active season or no daily objectives are configured, skip silently — no error or empty announcement.

**Admin Tool Season Statistics tab:**
- Add a "Today's Daily Objectives" section to the Season Statistics tab (or a new sub-panel within it).
- Query: for the selected season and current UTC `day_window`, return the active daily objective IDs (applying pool selection if `daily_objectives_per_day` is set), joined with `season_objective_progress` to count distinct `character_id` values where `completed = 1` per objective.
- Display as a grid: Objective Name | Activity Type | Target | Completions Today.
- Refresh on demand (button or tab activation) — no live polling required.
- The query must respect the same deterministic pool selection as the server side so the displayed objectives match what players actually see.

### Notes
Depends on [[IMPROVEMENT-006]] — daily objective infrastructure (schema, progress tracking) must be in place.
Depends on [[IMPROVEMENT-022]] — pool selection logic must be extractable/reusable by both the announcement path and the Admin Tool query.
The announcement fires from `SeasonService.RefreshCache` — guard against duplicate announcements if `RefreshCache` is called multiple times within the same day (e.g. track the last announced `day_window` in memory and skip if it matches).
If no season is active at restart time but one activates later (e.g. scheduled start), the announcement is not retroactively sent — it only fires at server startup.
Admin Tool completion count reflects the running day only; historical per-day stats are out of scope for this improvement.

---

## IMPROVEMENT-025 - Equipment Set Synergy Bonuses

Status: DONE
Priority: MEDIUM
Area: Combat / Items / Modules

### Description
Introduce an equipment set mechanic: modules belonging to the same named set grant the equipping character additional stat bonuses that scale proportionally with the number of set pieces currently fitted. The more set pieces equipped, the stronger the cumulative synergy bonus.

### Impact
Adds a meaningful progression layer on top of individual module selection, encouraging themed loadouts and giving players a tangible reward for committing to a set. Increases build diversity and long-term equipment goals without requiring new combat systems.

### Proposed Implementation

**Data layer:**
- Add a `set_id` (or `set_name`) column to `entitydefaults` (or a new `equipment_sets` table) to group modules into named sets.
- Add a `equipment_set_bonuses` table: `(set_id, required_pieces, aggregate_field, bonus_value)` — each row defines a bonus unlocked at a specific piece count threshold. Alternatively, use a linear scaling formula stored per set (e.g. `bonus_per_piece`) to avoid per-threshold rows.

**Server runtime:**
- On robot fitting change (equip/unequip), scan all fitted modules for `set_id` values, count pieces per set, then evaluate the bonus table for each set.
- Apply resulting bonuses as robot aggregate modifiers using the existing `RobotExtensions`/aggregate field pipeline — no new combat math required.
- Bonuses must be recalculated whenever the robot's fitting changes (equip, unequip, robot swap).
- Ensure bonuses are stripped correctly when modules are removed mid-combat or robot is unfit.

**Content:**
- Define at least one pilot set to validate the pipeline end-to-end.
- Follow naming convention from `docs/content/claude_game_content_guide.md` (`set_` prefix suggested).

**Client / UI:**
- Module tooltip should indicate set membership and current active bonus count.
- Requires client-side data delivery for set metadata (set name, total pieces, bonuses per threshold) — evaluate whether existing tooltip aggregate extension protocol is sufficient or if a new packet field is needed.

### Notes
Bonus recalculation must not run inside the zone update hot path synchronously — trigger on fitting events only.
Stacking rules (e.g. can a player equip two copies of the same set piece?) should be defined before implementation.
Consider whether set bonuses interact with existing robot extension bonuses additively or via a separate modifier layer.

---

## IMPROVEMENT-026 - Wear & Tear Mechanic

Status: TODO
Priority: LOW
Area: Items / Modules / Economy

### Description
Equipped and actively-used items gradually lose condition (health or a dedicated durability stat), reducing their efficiency proportionally. Items that reach critical condition become degraded; items left unrepaired eventually break or are destroyed. Periodic repair via an NPC service or player skill restores condition.

### Impact
Adds an ongoing maintenance loop that drives NPC interaction, credit sinks, and crafting demand. Encourages players to manage loadouts actively and creates meaningful consequences for extended combat or negligence. Increases economic depth by making repair services and spare parts relevant.

### Proposed Implementation

**Data layer:**
- Add a `condition` (or `durability`) field to the item instance table (e.g. `items` or equivalent), defaulting to max value on spawn.
- Add per-definition `max_durability` and `durability_loss_rate` columns to `entitydefaults` (or a separate `item_wear_config` table).
- Add a `broken` flag or a `condition = 0` sentinel to represent destroyed/non-functional state.

**Server runtime:**
- Hook into the existing damage/combat pipeline and module activation events to decrement condition by the configured rate on each relevant tick or activation.
- Apply an efficiency scalar to module aggregate contributions proportional to remaining condition (e.g. 50% condition → some % stat penalty). Define the penalty curve (linear vs stepped) before implementation.
- Broadcast condition changes to the client so the UI can reflect degradation.
- At condition = 0, disable the module (treat as unfit or non-functional) without destroying the item unless the design calls for permanent destruction.

**Repair:**
- Add a repair interaction with NPC repairers (cost scales with item tier and missing condition).
- Optionally support player-side repair via a skill or consumable.
- Repair must respect zone safety — no blocking DB writes in the zone update loop.

**Client / UI:**
- Module tooltip and fitting screen should display current condition / max condition.
- Add a visual indicator (colour, icon overlay) when condition falls below a warning threshold.
- Requires client protocol additions for condition field delivery; assess whether existing item attribute packet can carry this or a new field is needed.

### Notes
Define which item categories wear (active modules only, all fitted items, weapons, etc.) before implementation to scope the data changes.
Determine whether condition persists on trade/storage or resets — this has significant economy implications.
Avoid running condition decay calculations in the zone update hot path; prefer event-driven hooks on module activation and combat events.
Consider interaction with existing repair/maintenance NPC infrastructure if any exists.

---

## IMPROVEMENT-027 - Equipment Set Bonus Values in Effect Display

Status: DONE
Priority: HIGH
Area: Combat / Items / UI

### Problem

The set bonus effect applied by `SetBonusEffectApplicator` uses `.EnableModifiers(false)`, so no property modifier values are embedded in the effect. The client receives the `effect_equipment_set_bonus` effect token and can show an icon, but has no bonus amounts to display — the player cannot see what they actually gained.

### Impact

Players equipping set pieces receive silent bonuses with no in-UI feedback. This makes the mechanic invisible and undermines the design intent of rewarding themed loadouts.

### Proposed Fix

Embed the actual `ItemPropertyModifier` values into each set's effect using the same `.WithPropertyModifiers()` builder pattern already used by `RemoteCommandTranslatorModule.SetupEffect()`.

**Required changes:**

1. **`EquipmentSetBonusResult`** — replace the flat `IReadOnlyList<ItemPropertyModifier> Modifiers` with `IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> ModifiersPerSet` keyed by set ID. Retain `ActiveSetIds` or derive it from the dictionary keys.

2. **`EquipmentSetBonusCalculator.Compute()`** — the per-set grouping loop already exists; retain modifiers per set ID instead of collecting them into a flat list.

3. **`SetBonusEffectApplicator.Update()`** — accept the full `EquipmentSetBonusResult` (or the `ModifiersPerSet` dictionary). When creating a new set effect, call `.EnableModifiers(true)` and chain `.WithPropertyModifiers(modifiersForThisSet)`. Effect removal logic is unchanged.

4. **`Robot.OnUpdate()`** — pass the per-set modifier data when calling `_setBonusEffectApplicator.Update()`. `_setBonusModifiers` field may be removed if no other consumer needs the flat list.

**Reuse note:** The `ModuleProperty` class hierarchy from `RemoteCommandTranslatorModule` is not applicable here — set bonus values are static DB-sourced thresholds, not dynamically computed from ammo. The reusable element is solely the `EffectBuilder.WithPropertyModifiers()` call pattern.

### Performance Notes

`SetBonusEffectApplicator.Update()` is called every `OnUpdate()` tick but creates or removes effects only when the active set composition changes (set-difference check). Modifiers are passed only at effect-creation time, not on every tick. `EquipmentSetBonusCalculator.Compute()` already runs on fitting events only, not in the hot path. The per-set grouping change inside `Compute()` is a trivial restructure with no hot-path impact. No performance concern.

### Notes

Verify that the client-side effect display pipeline for `effect_equipment_set_bonus` actually reads and renders `PropertyModifiers` from the effect packet — confirm before declaring the work complete.

---

## IMPROVEMENT-028 - AdminTool Equipment Set Management

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Items / Modules

### Description

Extend the AdminTool with a dedicated UI for managing equipment sets (introduced in IMPROVEMENT-025) and the synergy bonuses they provide. Operators currently have no in-tool way to create sets, assign modules to sets, or configure per-threshold bonuses — all changes require direct DB edits.

### Impact

Without tooling, managing equipment sets is error-prone and requires database access. A purpose-built AdminTool panel lowers the barrier for content creators, reduces the risk of inconsistent data, and makes set configuration auditable from within the admin interface.

### Proposed Implementation

**Equipment Sets panel:**
- List all defined sets (from `equipment_sets` table) with name and ID.
- Create / rename / delete sets.
- View which module definitions are assigned to each set.

**Module assignment:**
- In the existing module/item definition editor, expose a "Set" dropdown (nullable) that lets operators assign or clear the module's set membership (`set_id` on `entitydefaults` or equivalent column).

**Bonus threshold editor:**
- For each set, display the bonus rows from `equipment_set_bonuses` (`required_pieces`, `aggregate_field`, `bonus_value`).
- Allow adding, editing, and removing bonus threshold rows.
- Validate that `required_pieces` values are positive integers and that `aggregate_field` references a known aggregate field ID.

**Read path:**
- Surface current set assignments and bonus rows without requiring a server restart — query live DB state.

### Notes

Follow existing AdminTool patterns for CRUD panels (look at NPC or loot table editors as reference).
Consider read-only vs. edit permissions if the AdminTool has role-based access controls.
Deleting a set should warn if modules are still assigned to it.

---

## IMPROVEMENT-029 - Pin Daily Activity Announcements in Discord

Status: DONE
Priority: HIGH
Area: Seasons / Announcements / Discord Integration

### Problem

Daily activity announcements are sent to players but quickly get buried by subsequent in-game chat messages. The in-game channel topic is not a viable alternative due to its character limit. Relying on players scrolling back to find the announcement is not sustainable.

### Impact

Players miss the current day's active objectives because the announcement disappears from view. Objective visibility is critical for engagement — if players cannot easily see what objectives are active, participation drops.

### Proposed Fix

When a daily activity announcement is dispatched to the integrated Discord channel, automatically pin the message so it remains visible regardless of subsequent chat volume.

- After sending the announcement message to Discord, retrieve the message ID from the Discord API response.
- Call the Discord "Pin Message" endpoint for the channel to pin the message.
- Before pinning the new announcement, unpin the previous day's announcement (if any) to avoid the pin list growing indefinitely — store the last pinned message ID (in memory or a small config/DB record) so it can be unpinned on the next announcement cycle.
- If the unpin or pin call fails (e.g. bot lacks Manage Messages permission), log a warning but do not block the announcement itself.

### Notes

Requires the Discord bot/webhook integration to have the `Manage Messages` permission in the target channel.
If the current integration uses an incoming webhook rather than a bot token, pinning is not possible via webhooks — a bot token with the `Manage Messages` permission will be required. Assess the current integration type before implementing.
The last pinned message ID can be stored in memory across restarts only if a restart always re-announces; otherwise persist it (a single-row config table or a flat file entry is sufficient).

---

## IMPROVEMENT-030 - AutoMarket Overhaul: NIC Injection Control, Dynamic Risk-Aware Pricing, and Performance Refactor

Status: DONE
Priority: HIGH
Area: Economy / AutoMarket / Database

### Problem

The AutoMarket has three interconnected problems that together drive hyperinflation:

1. **Plasma buy orders are a NIC faucet.** Every plasma sale to the bot calls `PayOutToSeller`, which creates NIC from nothing — there is no vendor wallet being drained. The buy quantity equals 100% of all plasma gathered in the past 7 days (`cdp.gathered`), making the bot procyclical: more farming → larger buy orders → more NIC created. No daily spending limit exists.

2. **Raw material prices are backwards and static.** `recalculate_raw_material_prices` distributes plasma NIC proportionally to gather volume, which means more supply → higher price (opposite of supply/demand). The static `raw_material_prices` fallback table requires manual maintenance and ignores zone risk — alpha and gamma materials are priced identically per the formula.

3. **Performance and thread-safety concerns.** `usp_RefreshAutoMarketOrders` uses four SQL cursors for order placement (row-by-row, slow). `MarketAutoOrdersManager` fires blocking DB operations synchronously from the process loop. `resources_gathered` lacks zone origin data.

### Impact

Inflation continues unchecked while the AutoMarket runs. Raw material prices do not reflect actual gather difficulty or zone risk, making the crafting economy unrealistic. Cursor-based SQL and blocking process-loop operations are latent performance risks.

### Proposed Fix

**Part A — NIC Injection Control:**
- New `automarket_config` table for all configurable parameters (anchor fraction, buy quantity fraction, daily budget).
- `usp_RefreshAutoMarketOrders`: multiply plasma buy quantity by `plasma_buy_qty_fraction` (default 0.60); add hard daily NIC budget cap derived from `plasma_sold.income`.
- `MarketAutoOrdersManager`: change refresh interval from 3 days to 1 day.

**Part B — Zone-Aware Gather Tracking:**
- Add `is_pvp BIT NOT NULL DEFAULT 0` to `resources_gathered_daily` and `resources_gathered`.
- Add `@is_pvp BIT = 0` parameter to `sp_RecordResourceGathered`; update `consolidate_statistics` to preserve it in the merge key.
- Update 5 C# gather call sites (`DrillerModule`, `HarvesterModule`, `LargeDrillerModule`, `LargeHarvesterModule`, `LootContainer`) to pass `!zone.Configuration.Protected`.

**Part C — Dynamic Risk-Aware Raw Material Pricing:**
- Rewrite `recalculate_raw_material_prices` with a new formula: `price = plasma_anchor × supply_demand_ratio × pvp_risk_multiplier`. Plasma anchor = live alpha plasma price × configurable fraction (default 0.15). Supply/demand ratio clamped 0.25–4.0. Risk multiplier 1.0 (all PvE) to 2.0 (all PvP); ungathered materials default to max scarcity + max risk.
- Remove the `raw_material_prices` fallback from `v_all_production_costs`. The table is deprecated but left in place.

**Part D — Performance and Thread-Safety Refactoring:**
- Analyze `MarketAutoOrdersManager.Update(time)`: determine process thread ownership; if blocking DB calls on the main process loop are confirmed, offload via `Task.Run` with proper exception handling following existing codebase patterns.
- Replace SQL cursors in `usp_RefreshAutoMarketOrders` with set-based `INSERT ... SELECT` where analysis confirms a performance benefit. Evaluate DELETE-all + INSERT-all vs. MERGE for the order refresh pattern.
- Assess lock contention between frequent `sp_RecordResourceGathered` inserts and `consolidate_statistics` MERGE under load.

### Implementation Notes

Completed in branch p36.4. All code changes committed to server runtime. Operator must execute the following SQL DDL against live database before new logic takes effect:

**Schema changes (Part B):**
1. `ALTER TABLE resources_gathered_daily ADD is_pvp BIT NOT NULL DEFAULT 0`
2. `ALTER TABLE resources_gathered ADD is_pvp BIT NOT NULL DEFAULT 0`

**Configuration table (Part A):**
3. `CREATE TABLE automarket_config (id INT PRIMARY KEY, plasma_buy_qty_fraction DECIMAL(5,4), daily_nic_budget BIGINT, plasma_anchor_fraction DECIMAL(5,4))`
4. Insert default row: `INSERT INTO automarket_config VALUES (1, 0.60, [calculate from current gather], 0.15)`

**Stored procedure changes (Parts A, B, C):**
5. `ALTER PROCEDURE sp_RecordResourceGathered` — add `@is_pvp BIT = 0` parameter
6. `ALTER PROCEDURE consolidate_statistics` — add `is_pvp` to GROUP BY and MERGE key
7. `ALTER PROCEDURE recalculate_raw_material_prices` — rewrite with new formula (see design spec)
8. `ALTER PROCEDURE usp_RefreshAutoMarketOrders` — apply budget cap and set-based inserts

**View changes (Part C):**
9. `ALTER VIEW v_all_production_costs` — remove `raw_material_prices` dependency, use dynamic pricing from procedure

**Execution notes:**
- Schema changes 1-2 are safe (backward-compatible defaults).
- Execute configuration table creation (3-4) before stored procedure changes.
- Procedures 5-9 must be executed in order: schema → config → procedures → view.
- No data migration required; existing tables and values remain unchanged.
- After DDL execution, refresh server cache (`gameConfig.ConfigManager` or admin command) to load `automarket_config`.

### Notes

Full design spec: `docs/superpowers/specs/2026-05-27-automarket-overhaul-design.md`

The `raw_material_prices` table is not dropped — only removed from active query paths — to preserve historical reference and allow rollback.
The `@is_pvp` parameter on `sp_RecordResourceGathered` defaults to `0`, so any call site not yet updated silently falls back to PvE treatment rather than failing.
Part D refactoring is scoped to analysis + targeted fixes only; broad restructuring of the market engine is out of scope.

---

## IMPROVEMENT-031 - AdminTool: AutoMarket Management and Statistics

Status: DONE
Priority: HIGH
Area: Admin Tool / Economy / AutoMarket

### Description

Add a dedicated **AutoMarket** panel to the AdminTool with four tabs: Config, Trade List, Statistics, and Orders. Operators currently have no in-tool way to tune AutoMarket parameters, manage the item trade list, or inspect economy health — all changes require direct DB access.

Follows the Seasons panel pattern: single nav entry, tabbed ViewModel, MVVM + ChangeQueue. No new server-side API is needed except one thin request handler for the manual refresh trigger.

### Tab 1 — Config

Editable grid of all `automarket_config` parameters with human-readable labels:
`plasma_anchor_fraction`, `plasma_buy_qty_fraction`, `daily_plasma_budget_nic`, `daily_rawmat_budget_nic`, `product_sell_margin`, `raw_mat_sell_multiplier`, `product_buyback_margin`, `resource_ds_ratio_min`, `resource_ds_ratio_max`.

Changes are queued via `ChangeQueue` and committed through the existing SQL script / direct-apply pipeline.

A **Refresh Now** toolbar button sends a server request to immediately trigger `MarketAutoOrdersManager` — requires one new thin request handler wired via the existing `Commands.cs` / Autofac pattern.

### Tab 2 — Trade List

Editable grid of `market_orders_configuration` rows. Columns: translated item name, definition name (read-only), amount (editable). Translated names via the existing translations system; falls back to `definitionname`.

- **Add item** — searchable item picker backed by `entitydefaults`, filterable by translated or internal name.
- **Remove item** — warns if the item is a dependency of others (via `v_required_raw_materials`).
- **Queue Save** per row — follows the ChangeQueue deduplication pattern ([[IMPROVEMENT-016]]).

A read-only sub-panel below the grid shows the derived raw materials that will be generated from the current trade list (via `v_required_raw_materials`), also with translated names.

### Tab 3 — Statistics

Read-only dashboard, refreshes on demand.

- **NIC Flow** — plasma NIC in and rawmat NIC out for today / last 7 days / total (from `plasma_sold` and `rawmat_purchased`); net delta per period; today's spend vs daily cap shown as a ratio.
- **Pricing Trace** — per raw material: translated name, plasma anchor input, supply/demand ratio, PvP risk multiplier, resulting price. Explains why each material is priced as it is.
- **Gather Breakdown** — per raw material: gather volume over last 7 days split by PvP vs PvE (from `resources_gathered_daily.is_pvp`). Validates risk multiplier inputs.

### Tab 4 — Orders

Read-only live snapshot of all active AutoMarket orders. Columns: translated item name, order type (Buy / Sell / Buyback), price, amount, translated market/base name, category (Plasma / Raw Material / Production Item). Filterable by order type and category.

Market/base names use translated display names via the existing translations system, with fallback to internal name.

### Impact

Without this panel, every config change, trade list edit, and economy health check requires direct DB access. The AdminTool gives operators a safe, auditable surface for the most frequently tuned AutoMarket levers introduced in [[IMPROVEMENT-030]] and [[ISSUE-024]].

### Proposed Implementation

**Server side:**
- Add one new `Commands.cs` entry and request handler (`AutoMarketRefreshHandler` or similar) that calls `MarketAutoOrdersManager` refresh method directly.
- Register via Autofac following existing handler patterns.

**AdminTool:**
- `AutoMarketViewModel` — root VM, owns tab VMs, wires Refresh Now command via server request.
- `AutoMarketConfigViewModel` — loads `automarket_config`; editable rows; ChangeQueue integration.
- `AutoMarketTradeListViewModel` — loads `market_orders_configuration`; item picker dialog; derived raw material sub-panel; ChangeQueue integration.
- `AutoMarketStatisticsViewModel` — loads NIC flow aggregates, pricing trace, gather breakdown; refresh-on-demand.
- `AutoMarketOrdersViewModel` — loads live market order snapshot; filter support; refresh-on-demand.
- Corresponding XAML Views for each VM.
- Wire `AutoMarketViewModel` into `MainViewModel` following the same pattern as `SeasonsViewModel`.

**No new DB tables required.** All data comes from existing tables and views introduced in IMPROVEMENT-030 and ISSUE-024.

### Notes

Translations: use the existing translations system throughout (item names, market/base names). Fall back to internal names if no translation exists — never show raw definition IDs to the operator.
ChangeQueue deduplication for Config and Trade List tabs — see [[IMPROVEMENT-016]].
The derived raw materials sub-panel in Trade List is read-only and does not generate ChangeQueue entries.
The Refresh Now button should be disabled while a refresh is in progress and should surface any server-side error to the operator.
Pricing Trace data source: query the last computed values from `resource_market_prices` (or equivalent output of `recalculate_raw_material_prices`) — no live re-computation in the AdminTool.

### Implementation

Implemented via plan `docs/superpowers/plans/2026-05-28-automarket-admintool.md` (14 tasks, branch p36.4).
Refresh Now calls SPs directly from AdminTool DB connection (no server-side handler needed).
`{x:Static}` binding on source-generator types causes MC1000 BAML errors — worked around with instance forwarder properties on `AutoMarketOrdersViewModel`.

---

## IMPROVEMENT-032 - Export: Generate Full SQL Scripts for Seasons, Items, and Robots

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Content / Tooling

### Description

Add an **Export** feature to the Admin Tool that generates a complete, self-contained SQL script for a selected entity — a season, an item definition, or a robot definition. The script must capture all dependent data (definitions, extensions, tech tree nodes, effects, module assignments, crafting recipes, etc.) so it can be replayed on a clean database to recreate the entity from scratch.

### Impact

Currently there is no way to extract a game entity as portable SQL. Transferring content between server instances, creating backups of handcrafted entities, or sharing content with other operators requires direct DB access and manual query construction. An export tool reduces this friction significantly and acts as a lightweight content migration mechanism.

### Proposed Implementation

- **Export targets:** Season (full chain: season record, activities, objectives, reward packages, reward items), Item definition (entitydefaults row, extensions, aggregate fields, tech tree nodes, crafting recipe, market config), Robot definition (entitydefaults row, chassis slots, head/leg/chassis component links, extensions, tech tree nodes).
- **Output format:** Idempotent SQL script using `MERGE` / `IF NOT EXISTS` / `DELETE + INSERT` patterns consistent with the existing content pipeline (see `docs/content/claude_game_content_guide.md`). Scripts must be replayable without manual ID editing — resolve foreign keys dynamically by name where possible, or embed explicit ID resolution CTEs.
- **UI surface:** Export button/menu entry in each relevant Admin Tool panel (Seasons panel, item editor, robot editor). Opens a dialog showing the generated script with a Copy and a Save As option.
- **Scope boundary:** Export is read-only and generates SQL text only — it does not execute the script or modify any data.

### Notes

- Never hardcode definition or extension IDs in generated output — resolve via `entitydefaults`/`extensions` name lookups exactly as the manual content guide mandates.
- The generated script should include a header comment identifying the export source, entity name, and export timestamp.
- Consult `docs/content/claude_game_content_guide.md` sections 2 and 24 for dependency order before implementing the traversal logic.
- Consider a shared `SqlExportBuilder` utility class to avoid duplicating script-generation logic across the three entity types.

---

## IMPROVEMENT-033 - Equipment Set Rewards for Seasons

Status: DONE
Priority: HIGH
Area: Seasons / Rewards

### Description

At every reward grant point in the Seasons system — tier rewards, objective completion rewards, and leaderboard rewards — add support for specifying an **equipment set** as a reward option. When a reward of this type is granted, the player receives one randomly selected item from the named equipment set instead of a fixed item.

### Impact

Tier rewards, objective rewards, and leaderboard rewards currently support only fixed item grants. Equipment set rewards add designer-controlled randomness: a player is guaranteed an item from a curated pool (a themed set) but does not know which piece they will receive. This increases perceived value, supports set-collection engagement loops, and reduces designer overhead by allowing one reward entry to cover an entire set rather than requiring individual item reward rows.

### Proposed Implementation

**Data layer:**
- Extend the reward package schema to include an optional `equipment_set_id` column (FK to `equipment_sets`) alongside the existing item definition reference. Exactly one of `item_definition_id` or `equipment_set_id` should be non-null per reward row.
- On reward grant, if `equipment_set_id` is set: query all module definitions belonging to that set, select one at random, and grant that item via the standard item grant pipeline.
- If the equipment set has no members at grant time, log a warning and skip the reward (no crash, no silent data corruption).

**Server runtime:**
- Extend the reward grant path (shared by tier, objective, and leaderboard rewards) to handle the `equipment_set_id` case — keep the branching in the reward delivery layer, not scattered across each reward trigger site.
- Random selection should be uniform across all set members unless a weighted variant is later requested.

**Admin Tool:**
- In the reward package editor (used by tier rewards, objective rewards, and leaderboard rewards), add an "Equipment Set" reward type option alongside the existing item picker.
- When "Equipment Set" is selected, show a dropdown of defined equipment sets; hide the item definition picker.

### Notes

- Reuse the equipment set membership data already introduced by IMPROVEMENT-025 (`equipment_sets` / module-to-set assignment) — do not introduce a parallel set definition mechanism.
- Consult `docs/content/claude_game_content_guide.md` for reward package SQL patterns before generating migration SQL.
- Validate that the selected set has at least one member before saving in the Admin Tool (warn, do not hard-block).
- Random selection occurs at grant time on the server, not at reward package definition time.
