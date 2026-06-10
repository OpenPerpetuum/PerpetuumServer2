namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketCoveredMaterialRow
    {
        public string  DefinitionName       { get; init; } = "";
        public string  DisplayName          { get; set;  } = "";
        public double  CurrentPrice         { get; init; }
        public long    EffectiveCap         { get; init; }  // BIGINT: COALESCE(override, global default)
        public int?    WeeklyCapOverride    { get; set;  }  // INT NULL: matches DB column type
        public long    BoughtThisWeek       { get; init; }
        public bool    CreateBuyOrders      { get; set;  }
        public bool    CreateSellOrders     { get; set;  }

        // Originals for change detection — need set because QueueSave updates them after dispatch
        public int?    OriginalCapOverride  { get; set;  }
        public bool    OriginalBuyOrders    { get; set;  }
        public bool    OriginalSellOrders   { get; set;  }

        public bool HasOverride =>
            WeeklyCapOverride.HasValue || !CreateBuyOrders || !CreateSellOrders;

        public bool IsAtDefaults =>
            !WeeklyCapOverride.HasValue && CreateBuyOrders && CreateSellOrders;
    }
}
