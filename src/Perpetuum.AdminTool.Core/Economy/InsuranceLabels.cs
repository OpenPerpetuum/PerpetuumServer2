using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    internal static class InsuranceLabels
    {
        internal record LabelMeta(string Label, string Description);

        internal static readonly IReadOnlyDictionary<string, LabelMeta> Map =
            new Dictionary<string, LabelMeta>
            {
                ["fee_pct"]    = new("Fee %",    "Insurance fee charged at purchase, as a fraction of production cost (e.g. 0.10 = 10%)"),
                ["payout_pct"] = new("Payout %", "Insurance payout on robot death, as a fraction of production cost (must be less than Fee %)"),
            };
    }
}
