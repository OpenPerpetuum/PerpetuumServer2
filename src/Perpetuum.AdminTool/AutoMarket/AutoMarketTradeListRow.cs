using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.AutoMarket
{
    public partial class AutoMarketTradeListRow : ObservableObject
    {
        public string DefinitionName { get; init; } = "";
        public string DisplayName    { get; set;  } = "";
        public int    OriginalAmount { get; set;  }
        public bool   OriginalCreateSellOrders    { get; set; }
        public bool   OriginalCreateBuybackOrders { get; set; }

        [ObservableProperty] private int  _amount;
        [ObservableProperty] private bool _createSellOrders;
        [ObservableProperty] private bool _createBuybackOrders;

        public bool IsDirty =>
            Amount             != OriginalAmount
            || CreateSellOrders    != OriginalCreateSellOrders
            || CreateBuybackOrders != OriginalCreateBuybackOrders;

        partial void OnAmountChanged(int value)             => OnPropertyChanged(nameof(IsDirty));
        partial void OnCreateSellOrdersChanged(bool value)    => OnPropertyChanged(nameof(IsDirty));
        partial void OnCreateBuybackOrdersChanged(bool value) => OnPropertyChanged(nameof(IsDirty));
    }
}
