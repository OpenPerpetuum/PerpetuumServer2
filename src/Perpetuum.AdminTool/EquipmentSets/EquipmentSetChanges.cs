using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public static class EquipmentSetChanges
    {
        // Returns either the literal integer (existing sets) or a subquery (new sets).
        private static string SetIdExpr(int setId, string setName) =>
            setId > 0
                ? setId.ToString()
                : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(setName)})";

        public static IPendingChange BuildInsertSet(string name) =>
            new RawSqlChange(
                $"equipment_sets: insert '{name}'",
                $"INSERT INTO equipment_sets (name) VALUES ({SqlLiteral.Of(name)})");

        public static IPendingChange BuildRenameSet(int setId, string newName) =>
            new RawSqlChange(
                $"equipment_sets: rename id {setId} to '{newName}'",
                $"UPDATE equipment_sets SET name = {SqlLiteral.Of(newName)} WHERE set_id = {setId}");

        public static IPendingChange BuildDeleteSet(int setId, string name) =>
            new RawSqlChange(
                $"equipment_sets: cascade delete '{name}' (id {setId})",
                $"DELETE FROM equipment_set_bonus_thresholds WHERE set_id = {setId};\n" +
                $"DELETE FROM equipment_set_members             WHERE set_id = {setId};\n" +
                $"DELETE FROM equipment_sets                    WHERE set_id = {setId}",
                isDestructive: true);

        public static IPendingChange BuildInsertMember(int setId, string setName, int definition) =>
            new RawSqlChange(
                $"equipment_set_members: add definition {definition} to set '{setName}'",
                $"INSERT INTO equipment_set_members (set_id, definition) " +
                $"VALUES ({SetIdExpr(setId, setName)}, {definition})");

        public static IPendingChange BuildDeleteMember(int setId, string setName, int definition) =>
            new RawSqlChange(
                $"equipment_set_members: remove definition {definition} from set '{setName}'",
                $"DELETE FROM equipment_set_members " +
                $"WHERE set_id = {SetIdExpr(setId, setName)} AND definition = {definition}",
                isDestructive: true);

        public static IPendingChange BuildUpsertThreshold(
            int setId, string setName, int requiredPieces, int aggregateFieldId, double bonusValue)
        {
            var sid = SetIdExpr(setId, setName);
            var val = bonusValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return new RawSqlChange(
                $"equipment_set_bonus_thresholds: upsert set '{setName}' pieces {requiredPieces}",
                $"MERGE INTO equipment_set_bonus_thresholds AS target " +
                $"USING (SELECT {sid} AS set_id, {requiredPieces} AS required_pieces) AS src " +
                $"ON target.set_id = src.set_id AND target.required_pieces = src.required_pieces " +
                $"WHEN MATCHED THEN UPDATE SET aggregate_field = {aggregateFieldId}, bonus_value = {val} " +
                $"WHEN NOT MATCHED THEN INSERT (set_id, required_pieces, aggregate_field, bonus_value) " +
                $"VALUES (src.set_id, {requiredPieces}, {aggregateFieldId}, {val})");
        }

        public static IPendingChange BuildDeleteThreshold(int setId, string setName, int requiredPieces) =>
            new RawSqlChange(
                $"equipment_set_bonus_thresholds: delete set '{setName}' pieces {requiredPieces}",
                $"DELETE FROM equipment_set_bonus_thresholds " +
                $"WHERE set_id = {SetIdExpr(setId, setName)} AND required_pieces = {requiredPieces}",
                isDestructive: true);
    }
}
