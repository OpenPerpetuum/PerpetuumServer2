namespace Perpetuum.AdminTool.Common
{
    public class EntityPickItem
    {
        public int Definition { get; init; }
        public string Name { get; init; } = "";

        // Exposed so consumers (structured editors, NPC-loot/relations dropdowns,
        // potential category filters) can match on category without an extra DB hit.
        public long CategoryFlags { get; init; }

        // Mirrors entitydefaults.enabled. Consumers (structured editors, future
        // selectors) hide disabled rows. Newly inserted rows default to enabled = 1
        // so they show up automatically once the cache refreshes post-commit.
        public bool Enabled { get; init; }

        public string Display => $"{Definition} — {Name}";
    }
}
