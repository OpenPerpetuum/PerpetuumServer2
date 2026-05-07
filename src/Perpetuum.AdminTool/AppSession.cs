using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool
{
    public class AppSession
    {
        public int? AccountId { get; set; }
        public string? Email { get; set; }
        public AccessLevel AccessLevel { get; set; } = AccessLevel.notDefined;
        public ApplyMode CurrentMode { get; set; } = ApplyMode.SqlScript;
        public ChangeQueue Changes { get; } = new ChangeQueue();
        public LookupCache Lookups { get; } = new LookupCache();

        public bool IsAuthenticated => AccountId.HasValue;

        public string DisplayName =>
            IsAuthenticated ? $"{Email} ({AccessLevel})" : "(not signed in)";
    }
}
