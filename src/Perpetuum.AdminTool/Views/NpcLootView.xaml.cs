using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class NpcLootView : UserControl
    {
        public NpcLootView()
        {
            InitializeComponent();
        }

        private NpcLootViewModel? Vm => DataContext as NpcLootViewModel;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.ReloadAsync();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            Grid.CommitEdit(DataGridEditingUnit.Row, true);
            Vm?.SaveAll();
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            if (Vm.Lookups.Entities.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "No entitydefaults loaded. Reload the NPC loot tab first.",
                    "Cannot add rule",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            while (true)
            {
                var win = new AddNpcLootRowWindow(Vm.SuggestNextDefinition(), Vm.Lookups)
                {
                    Owner = Window.GetWindow(this)
                };
                if (win.ShowDialog() != true) return;

                var v = win.ViewModel;
                if (Vm.TryAddNew(v.Definition, v.LootDefinition, v.MinQuantity, v.Quantity,
                                 v.Probability, v.DontDamage, v.Repackaged, out var error))
                {
                    return;
                }
                MessageBox.Show(Window.GetWindow(this), error, "Cannot add loot rule",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null) return;
            var row = Vm.SelectedRow;

            string prompt = row.IsNew
                ? $"Discard the new (unsaved) rule (NPC {row.Definition} → item {row.LootDefinition})?"
                : $"Remove rule (NPC {row.Definition} → item {row.LootDefinition})?\n\n" +
                  "On Save, a DELETE FROM npcloot statement will be queued.";

            var ok = MessageBox.Show(Window.GetWindow(this), prompt, "Remove rule",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            Vm.RemoveSelected();
        }
    }
}
