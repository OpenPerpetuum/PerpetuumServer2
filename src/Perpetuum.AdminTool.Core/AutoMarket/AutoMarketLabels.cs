namespace Perpetuum.AdminTool.AutoMarket
{
    internal static class AutoMarketLabels
    {
        internal record LabelMeta(string Label, string Description);

        internal static readonly IReadOnlyDictionary<string, LabelMeta> Map =
            new Dictionary<string, LabelMeta>
            {
                ["plasma_anchor_fraction"]    = new("Plasma Anchor Fraction",         "Fraction of alpha plasma price used as raw material pricing anchor"),
                ["plasma_buy_qty_fraction"]   = new("Plasma Buy Quantity",             "Fraction of gathered plasma placed as buy orders"),
                ["daily_plasma_budget_nic"]   = new("Daily Plasma Budget (NIC)",       "Max NIC spent on plasma buy orders per calendar day"),
                ["daily_rawmat_budget_nic"]   = new("Daily Rawmat Budget (NIC, 0=∞)",  "Max NIC spent on raw material buy orders per calendar day. 0 = unlimited."),
                ["weekly_rawmat_cap_default"] = new("Weekly Rawmat Cap (default, 0=∞)","Default max units AutoMarket buys per raw material per week. 0 = unlimited."),
                ["resource_ds_ratio_min"]     = new("S/D Ratio Min",                   "Lower clamp for supply/demand ratio in pricing formula"),
                ["resource_ds_ratio_max"]     = new("S/D Ratio Max",                   "Upper clamp for supply/demand ratio in pricing formula"),
                ["product_sell_margin"]       = new("Product Sell Margin",             "Production item sell orders priced at production_cost × this value"),
                ["raw_mat_sell_multiplier"]   = new("Rawmat Sell Multiplier",          "Raw material sell orders priced at production_cost × this value"),
                ["product_buyback_margin"]    = new("Product Buyback Margin",          "Buyback buy orders priced at production_cost × this value"),
            };
    }
}
