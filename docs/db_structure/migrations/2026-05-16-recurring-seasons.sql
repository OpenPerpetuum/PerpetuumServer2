-- IMPROVEMENT-001: Recurring Seasons
-- Adds recurrence support to the seasons table.
-- All columns are additive; existing rows are unaffected (defaults keep existing behavior).

ALTER TABLE seasons
    ADD is_recurring         BIT           NOT NULL DEFAULT 0,
        recurrence_gap_days  INT           NULL,
        recurrence_iteration INT           NOT NULL DEFAULT 1,
        recurrence_base_name NVARCHAR(255) NULL;
