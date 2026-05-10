using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AttributeFlagsPickerViewModel : ObservableObject
    {
        public ObservableCollection<BitItem> Items { get; } = new();

        public AttributeFlagsPickerViewModel(ulong initialValue)
        {
            foreach (var bit in AttributeFlagsCatalog.Bits)
            {
                Items.Add(new BitItem(bit, AttributeFlagsCatalog.IsSet(initialValue, bit.Position)));
            }
        }

        public ulong ComposeValue()
        {
            ulong value = 0;
            foreach (var item in Items)
            {
                if (item.IsChecked) value |= item.Bit.Mask;
            }
            return value;
        }

        public partial class BitItem : ObservableObject
        {
            public AttributeFlagsCatalog.Bit Bit { get; }

            [ObservableProperty] private bool _isChecked;

            public BitItem(AttributeFlagsCatalog.Bit bit, bool isChecked)
            {
                Bit = bit;
                _isChecked = isChecked;
            }

            public string Display => Bit.Display;
        }
    }
}
