using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyMarketData
    {
        public IReadOnlyList<EconomyVelocityRow>   VelocityRows   { get; init; } = System.Array.Empty<EconomyVelocityRow>();
        public IReadOnlyList<EconomyPriceIndexRow> PriceIndexRows { get; init; } = System.Array.Empty<EconomyPriceIndexRow>();
        public EconomyListingAgeBuckets            AgeBuckets     { get; init; } = new();
        public int AutoMarketOrderCount { get; init; }
        public int PlayerOrderCount     { get; init; }
    }
}
