namespace Perpetuum.AdminTool.Editing
{
    public class RawSqlChange : IPendingChange
    {
        public string Description { get; }
        public bool IsDestructive { get; }
        private readonly string _sql;

        public RawSqlChange(string description, string sql, bool isDestructive = false)
        {
            Description = description;
            _sql = sql;
            IsDestructive = isDestructive;
        }

        public string ToSql() => _sql;
    }
}
