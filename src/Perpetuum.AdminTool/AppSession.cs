using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool
{
    public class AppSession
    {
        public int? AccountId { get; set; }
        public string? Email { get; set; }
        public AdminAccessLevel AccessLevel { get; set; } = AdminAccessLevel.NotDefined;
        public ApplyMode CurrentMode { get; set; } = ApplyMode.SqlScript;
        public ChangeQueue Changes { get; } = new ChangeQueue();
        public LookupCache Lookups { get; } = new LookupCache();

        public bool IsAuthenticated => AccountId.HasValue;

        public string DisplayName =>
            IsAuthenticated ? $"{Email} ({AccessLevel})" : "(not signed in)";
    }
}
