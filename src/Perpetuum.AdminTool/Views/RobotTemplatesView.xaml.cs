using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class RobotTemplatesView : UserControl
    {
        public RobotTemplatesView()
        {
            InitializeComponent();
        }

        private RobotTemplatesViewModel? Vm => DataContext as RobotTemplatesViewModel;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.ReloadAsync();
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            while (true)
            {
                var win = new NewTemplateWindow { Owner = Window.GetWindow(this) };
                if (win.ShowDialog() != true) return;
                if (Vm.TryAddNew(win.Inputs.Name, out var error)) return;
                MessageBox.Show(Window.GetWindow(this), error, "Cannot create template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e) => Vm?.Save();
        private void OnDiscardClick(object sender, RoutedEventArgs e) => Vm?.Discard();
        private void OnValidateClick(object sender, RoutedEventArgs e) => Vm?.ValidateGenxy();

        private async void OnStructuredEditClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null) return;
            var row = Vm.SelectedRow;

            if (row.IsQueued)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "This template is already queued for INSERT. Reload after Commit before editing.",
                    "Cannot edit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var entities = await Vm.LoadEditorEntitiesAsync();
                var editorVm = new RobotTemplateEditorViewModel(entities, row.Description ?? "");
                var win = new RobotTemplateEditorWindow(editorVm) { Owner = Window.GetWindow(this) };
                if (win.ShowDialog() == true)
                {
                    row.Description = editorVm.ResultGenxy;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Editor failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.SelectedRow == null) return;
            var row = Vm.SelectedRow;

            string prompt = row.IsNew
                ? $"Discard the new (unsaved) template '{row.Name}'?"
                : $"Delete template id {row.Id} ('{row.Original.Name}')?\n\nThis queues a DELETE FROM robottemplates statement.";

            var ok = MessageBox.Show(Window.GetWindow(this), prompt, "Delete template",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            var changes = Vm.EnqueueDelete();
            Vm.RemoveRow(row);

            if (changes.Count > 0)
            {
                MessageBox.Show(Window.GetWindow(this),
                    $"Queued {changes.Count} destructive change(s). Use the main Commit button to apply.",
                    "Delete queued", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
