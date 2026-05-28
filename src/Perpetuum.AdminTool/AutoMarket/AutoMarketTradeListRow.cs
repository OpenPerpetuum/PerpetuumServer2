using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.AutoMarket
{
    public partial class AutoMarketTradeListRow : ObservableObject
    {
        public string DefinitionName { get; init; } = "";
        public string DisplayName    { get; set;  } = "";
        public int    OriginalAmount { get; set;  }

        [ObservableProperty] private int _amount;

        public bool IsDirty => Amount != OriginalAmount;

        partial void OnAmountChanged(int value) => OnPropertyChanged(nameof(IsDirty));
    }
}
