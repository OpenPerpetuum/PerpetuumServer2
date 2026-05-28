namespace Perpetuum.AdminTool.AutoMarket
{
    public record AutoMarketOrderData(
        int    ItemDefinition,
        string DefinitionName,
        bool   IsSell,
        double Price,
        int    Quantity,
        string MarketDefinitionName);
}
