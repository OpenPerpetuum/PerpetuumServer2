-- Insert effects table row for effect_equipment_set_bonus (ID 139)
-- (IMPROVEMENT-025)
-- Idempotent: safe to run multiple times.
-- The effects.id column is IDENTITY(1,1), so IDENTITY_INSERT is required
-- when inserting a specific ID value.

SET IDENTITY_INSERT effects ON;

IF NOT EXISTS (SELECT 1 FROM effects WHERE id = 139)
BEGIN
    INSERT INTO effects
        (id, name, description, effectcategory, duration, isaura, auraradius, ispositive, display, saveable)
    VALUES
        (139,
         N'Set Bonus Active',
         N'An equipment set bonus is active on this robot.',
         0,
         0,
         0,
         0,
         1,
         1,
         0);
END;

SET IDENTITY_INSERT effects OFF;
