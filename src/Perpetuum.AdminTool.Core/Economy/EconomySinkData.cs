using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomySinkData
    {
        public int    ActivePlayerCount     { get; init; }
        public double InsuranceCoveragePct  { get; init; }
        public IReadOnlyList<EconomySinkRow> SinkRows { get; init; } = System.Array.Empty<EconomySinkRow>();
    }
}
