using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppSettingsStore _store;
        private readonly AppSession _session;

        [ObservableProperty] private ApplyMode _currentMode;
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private int _pendingChangeCount;

        public ChangeQueue Changes => _session.Changes;
        public string ConnectedAs => _session.DisplayName;
        public AppSettingsStore Store => _store;
        public AppSession Session => _session;

        public MainViewModel(AppSettingsStore store, AppSession session)
        {
            _store = store;
            _session = session;
            _currentMode = session.CurrentMode;
            UpdateStatus();

            Changes.Items.CollectionChanged += (_, _) =>
            {
                PendingChangeCount = Changes.Items.Count;
                UpdateStatus();
            };
        }

        partial void OnCurrentModeChanged(ApplyMode value)
        {
            _session.CurrentMode = value;
            _store.Settings.DefaultApplyMode = value;
            _store.Save();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusText =
                $"Mode: {CurrentMode}    |    Pending changes: {Changes.Items.Count}    |    {ConnectedAs}";
        }

        [RelayCommand]
        private void AddStubChange()
        {
            // Phase 1 placeholder. Replaced by real edits in later phases.
            var change = new RawSqlChange(
                "Stub change (Phase 1 placeholder — does nothing).",
                "-- no-op\nSELECT 1;");
            Changes.Add(change);
        }

        [RelayCommand]
        private void ClearChanges()
        {
            Changes.Clear();
            PendingChangeCount = 0;
            UpdateStatus();
        }

        public void OpenSettings(Window owner)
        {
            var vm = new ConnectionSettingsViewModel(_store);
            var w = new ConnectionSettingsWindow(vm) { Owner = owner };
            w.ShowDialog();
            UpdateStatus();
        }

        public async Task CommitAsync(Window owner)
        {
            if (!Changes.HasPending)
            {
                MessageBox.Show(owner, "No pending changes.", "Nothing to apply",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var script = SqlScriptBuilder.Build(Changes.Items, _session.Email);

            if (CurrentMode == ApplyMode.SqlScript)
            {
                var dir = _store.Settings.SqlOutputDirectory;
                if (string.IsNullOrWhiteSpace(dir))
                {
                    MessageBox.Show(owner,
                        "SQL output directory is not configured. Open Connection settings to set one.",
                        "Cannot save script", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    Directory.CreateDirectory(dir);
                    var fileName = $"admintool_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
                    var path = Path.Combine(dir, fileName);
                    await File.WriteAllTextAsync(path, script);

                    MessageBox.Show(owner,
                        $"Wrote {Changes.Items.Count} change(s) to:\n{path}",
                        "Script saved", MessageBoxButton.OK, MessageBoxImage.Information);

                    Changes.Clear();
                    UpdateStatus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, ex.Message, "Failed to write script",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // Direct DB mode
            var confirmVm = new ConfirmSqlViewModel(
                $"About to apply {Changes.Items.Count} change(s) directly to the database.",
                script);
            var confirmWin = new ConfirmSqlWindow(confirmVm) { Owner = owner };
            if (confirmWin.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var applier = new ChangeApplier(_store.Settings.Connection);
                await applier.ExecuteAsync(Changes.Items.ToArray());

                MessageBox.Show(owner,
                    $"Applied {Changes.Items.Count} change(s) successfully.",
                    "Done", MessageBoxButton.OK, MessageBoxImage.Information);

                Changes.Clear();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, ex.Message, "Failed to apply changes",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Logout()
        {
            _session.AccountId = null;
            _session.Email = null;
            _session.AccessLevel = AccessLevel.notDefined;
            _session.Changes.Clear();
        }
    }
}
