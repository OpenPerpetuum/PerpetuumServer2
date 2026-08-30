using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyMoneySupplyData
    {
        public long   TotalNic      { get; init; }
        public long   MedianNic     { get; init; }
        public double Top1PctShare  { get; init; }
        public long   IdleNic       { get; init; }
        public IReadOnlyList<EconomySnapshotRow>          SnapshotRows  { get; init; } = System.Array.Empty<EconomySnapshotRow>();
        public IReadOnlyList<EconomyWealthRow>            Top10Rows     { get; init; } = System.Array.Empty<EconomyWealthRow>();
        public IReadOnlyList<EconomyCorporationWealthRow> Top10CorpRows { get; init; } = System.Array.Empty<EconomyCorporationWealthRow>();
    }
}
