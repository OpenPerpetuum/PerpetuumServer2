using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.AutoMarket
{
    public partial class AutoMarketConfigRow : ObservableObject
    {
        public string ParamName    { get; init; } = "";
        public string Label        { get; init; } = "";
        public string Description  { get; init; } = "";
        public double OriginalValue { get; set; }

        [ObservableProperty] private double _paramValue;

        public bool IsDirty => Math.Abs(ParamValue - OriginalValue) > 1e-9;

        partial void OnParamValueChanged(double value) => OnPropertyChanged(nameof(IsDirty));
    }
}
