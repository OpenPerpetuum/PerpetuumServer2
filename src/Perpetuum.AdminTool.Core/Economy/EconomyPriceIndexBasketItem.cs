using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Economy
{
    public partial class EconomyPriceIndexBasketItem : ObservableObject
    {
        public int    Id             { get; init; }
        public int    Definition     { get; init; }
        public string DefinitionName { get; init; } = "";
        [ObservableProperty] private double _weight;
    }
}
