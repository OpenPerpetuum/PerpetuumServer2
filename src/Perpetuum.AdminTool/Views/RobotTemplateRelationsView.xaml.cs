using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class RobotTemplateRelationsView : UserControl
    {
        public RobotTemplateRelationsView()
        {
            InitializeComponent();
        }

        private RobotTemplateRelationsViewModel? Vm => DataContext as RobotTemplateRelationsViewModel;

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

            if (Vm.Lookups.Templates.Count == 0 || Vm.Lookups.Entities.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "Lookups not loaded. Reload the Relations tab first (and ensure robottemplates and entitydefaults have rows).",
                    "Cannot add relation",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            while (true)
            {
                var win = new AddTemplateRelationRowWindow(Vm.Lookups)
                {
                    Owner = Window.GetWindow(this)
                };
                if (win.ShowDialog() != true) return;

                var v = win.ViewModel;
                if (Vm.TryAddNew(v.Definition, v.TemplateId, v.ItemScoreSum, v.RaceId,
                                 v.MissionLevel, v.MissionLevelOverride, v.KillEp, v.Note,
                                 out var error))
                {
                    return;
                }
                MessageBox.Show(Window.GetWindow(this), error, "Cannot add relation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null) return;
            var row = Vm.SelectedRow;

            string prompt = row.IsNew
                ? $"Discard the new (unsaved) relation for definition {row.Definition}?"
                : $"Remove the relation for definition {row.Definition} (→ template {row.TemplateId})?\n\n" +
                  "On Save, a DELETE FROM robottemplaterelation statement will be queued.";

            var ok = MessageBox.Show(Window.GetWindow(this), prompt, "Remove relation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            Vm.RemoveSelected();
        }
    }
}
