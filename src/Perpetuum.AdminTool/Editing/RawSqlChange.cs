namespace Perpetuum.AdminTool.Editing
{
    public class RawSqlChange : IPendingChange
    {
        public string Description { get; }
        private readonly string _sql;

        public RawSqlChange(string description, string sql)
        {
            Description = description;
            _sql = sql;
        }

        public string ToSql() => _sql;
    }
}
