using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class FlocksView : UserControl
    {
        public FlocksView()
        {
            InitializeComponent();
        }

        private FlocksViewModel? Vm => DataContext as FlocksViewModel;

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
            if (Vm.Lookups.Entities.Count == 0 || Vm.PresencePicks.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "Pick lists are empty. Reload the Flocks tab first.",
                    "Cannot add flock",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            while (true)
            {
                var win = new AddFlockRowWindow(Vm.SuggestNextPresenceId(), Vm.Lookups, Vm.PresencePicks)
                {
                    Owner = Window.GetWindow(this)
                };
                if (win.ShowDialog() != true) return;

                var v = win.ViewModel;
                var seed = new FlockSnapshot
                {
                    Id = 0,
                    Name = v.Name,
                    PresenceId = v.PresenceId,
                    FlockMemberCount = v.FlockMemberCount,
                    Definition = v.Definition,
                    SpawnOriginX = v.SpawnOriginX,
                    SpawnOriginY = v.SpawnOriginY,
                    SpawnRangeMin = v.SpawnRangeMin,
                    SpawnRangeMax = v.SpawnRangeMax,
                    RespawnSeconds = v.RespawnSeconds,
                    TotalSpawnCount = v.TotalSpawnCount,
                    HomeRange = v.HomeRange,
                    Note = v.Note,
                    RespawnMultiplierLow = v.RespawnMultiplierLow,
                    Enabled = v.Enabled,
                    IsCallForHelp = v.IsCallForHelp,
                    BehaviorType = v.BehaviorType,
                    NpcSpecialType = v.NpcSpecialType
                };
                if (Vm.TryAddNew(seed, out var error)) return;

                MessageBox.Show(Window.GetWindow(this), error, "Cannot add flock",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null) return;
            var row = Vm.SelectedRow;

            string prompt = row.IsNew
                ? $"Discard the new (unsaved) flock '{row.Name}'?"
                : $"Remove flock id {row.Id} ('{row.Name}')?\n\n" +
                  "On Save, a DELETE FROM npcflock statement will be queued.";

            var ok = MessageBox.Show(Window.GetWindow(this), prompt, "Remove flock",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            Vm.RemoveSelected();
        }
    }
}
