namespace Perpetuum.AdminTool.AutoMarket
{
    internal record AutoMarketOrderData(
        int    ItemDefinition,
        string DefinitionName,
        bool   IsSell,
        double Price,
        int    Quantity,
        string MarketDefinitionName);
}
