using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class PresencesView : UserControl
    {
        public PresencesView()
        {
            InitializeComponent();
        }

        private PresencesViewModel? Vm => DataContext as PresencesViewModel;

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
            while (true)
            {
                var win = new AddPresenceRowWindow(Vm.ZoneSpawnPicks)
                {
                    Owner = Window.GetWindow(this)
                };
                if (win.ShowDialog() != true) return;

                var v = win.ViewModel;
                var seed = new PresenceSnapshot
                {
                    Id = 0,
                    Name = v.Name,
                    TopX = v.TopX,
                    TopY = v.TopY,
                    BottomX = v.BottomX,
                    BottomY = v.BottomY,
                    Note = v.Note,
                    SpawnId = v.SpawnId,
                    Enabled = v.Enabled,
                    Roaming = v.Roaming,
                    RoamingRespawnSeconds = v.RoamingRespawnSeconds,
                    PresenceType = v.PresenceType,
                    MaxRandomFlock = v.MaxRandomFlock,
                    RandomCenterX = v.RandomCenterX,
                    RandomCenterY = v.RandomCenterY,
                    RandomRadius = v.RandomRadius,
                    DynamicLifetime = v.DynamicLifetime,
                    IsBodyPull = v.IsBodyPull,
                    IsRespawnAllowed = v.IsRespawnAllowed,
                    SafeBodyPull = v.SafeBodyPull,
                    IzGroupId = v.IzGroupId,
                    GrowthSeconds = v.GrowthSeconds
                };
                if (Vm.TryAddNew(seed, out var error)) return;

                MessageBox.Show(Window.GetWindow(this), error, "Cannot add presence",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnShowFlocksClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "Select a presence first.",
                    "No presence selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var row = Vm.SelectedRow;
            if (row.IsNew || row.Id <= 0)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "This presence is unsaved (no IDENTITY id yet). Save first, then reload.",
                    "Cannot list flocks",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var vm = new PresenceFlocksViewModel(Vm.Settings, row.Id, row.Name);
            var win = new PresenceFlocksWindow(vm) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null) return;
            var row = Vm.SelectedRow;

            string prompt = row.IsNew
                ? $"Discard the new (unsaved) presence '{row.Name}'?"
                : $"Remove presence id {row.Id} ('{row.Name}')?\n\n" +
                  "On Save, a DELETE FROM npcpresence statement will be queued. " +
                  "If any npcflock rows still reference this presence the FK will block the delete.";

            var ok = MessageBox.Show(Window.GetWindow(this), prompt, "Remove presence",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            Vm.RemoveSelected();
        }
    }
}
