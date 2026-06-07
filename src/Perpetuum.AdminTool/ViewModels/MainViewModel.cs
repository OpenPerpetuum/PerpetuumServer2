using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Seasons;
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
        public LookupCache Lookups => _session.Lookups;
        public TranslationsViewModel Translations { get; }
        public EntitiesViewModel Entities { get; }
        public RobotTemplatesViewModel RobotTemplates { get; }
        public RobotTemplateRelationsViewModel RobotTemplateRelations { get; }
        public NpcLootViewModel NpcLoot { get; }
        public PresencesViewModel Presences { get; }
        public FlocksViewModel Flocks { get; }
        public SeasonsViewModel Seasons { get; }
        public EquipmentSetsViewModel EquipmentSets { get; }
        public AutoMarketViewModel AutoMarket { get; }
        public EconomyViewModel Economy { get; }

        public MainViewModel(AppSettingsStore store, AppSession session)
        {
            _store = store;
            _session = session;
            _currentMode = session.CurrentMode;
            Translations = new TranslationsViewModel(store);
            Translations.Load();
            Entities = new EntitiesViewModel(store, session, session.Changes, Translations, session.Lookups);
            RobotTemplates = new RobotTemplatesViewModel(store, session.Changes, session.Lookups);
            RobotTemplateRelations = new RobotTemplateRelationsViewModel(store, session.Changes, session.Lookups);
            NpcLoot = new NpcLootViewModel(store, session.Changes, session.Lookups);
            Presences = new PresencesViewModel(store, session.Changes);
            Flocks = new FlocksViewModel(store, session.Changes, session.Lookups);
            Seasons = new SeasonsViewModel(
                new SeasonRepository(store.Settings.Connection),
                new PackageRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups,
                store.Settings.Connection,
                Translations);
            EquipmentSets = new EquipmentSetsViewModel(
                new EquipmentSetRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups,
                Translations);
            AutoMarket = new AutoMarketViewModel(
                new AutoMarketRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups,
                Translations);
            Economy = new EconomyViewModel(
                new EconomyRepository(store.Settings.Connection),
                new EconomyMoneySupplyRepository(store.Settings.Connection),
                new EconomyMarketHealthRepository(store.Settings.Connection),
                new EconomySinkRepository(store.Settings.Connection),
                new EconomyInsuranceRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups);
            UpdateStatus();

            _ = InitializeLookupsAsync();

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

        private async Task InitializeLookupsAsync()
        {
            try
            {
                await _session.Lookups.RefreshAllAsync(_store.Settings.Connection);
            }
            catch
            {
                // Per-tab Reload buttons are the fallback if startup refresh fails
                // (e.g. transient DB hiccup). Don't surface a modal here.
            }
        }

        // Drains queued new-entity definition names into the translation store.
        // Each name becomes a key; English (lang 0) is pre-filled with the name itself
        // so the user has a working English label out of the box. Existing keys are
        // skipped silently so we never clobber user-entered values. Returns a short
        // human-readable note appended to the commit-success dialog (or "" if nothing
        // was done).
        private string ApplyPendingTranslationKeys()
        {
            var pending = Changes.PendingNewEntityNames;
            if (pending.Count == 0) return "";

            var store = Translations.Store;
            if (store == null) return "";

            var added = 0;
            try
            {
                foreach (var name in pending)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (!store.TryAddKey(name, out _)) continue;
                    var row = store.Rows.FirstOrDefault(r => r.Key == name);
                    if (row != null) row[0] = name;
                    added++;
                }

                if (added == 0) return "";

                store.Save();
                return $"\n\nAlso created {added} translation key(s) (English = definitionName).";
            }
            catch (Exception ex)
            {
                return $"\n\nNote: failed to update translation files: {ex.Message}";
            }
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
            if (w.ShowDialog() == true)
            {
                // Reload translations so a freshly-set GameRoot path takes effect.
                Translations.Load();
            }
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
                    var fileName = SqlScriptBuilder.BuildFileName("season", Seasons.DetailViewModel?.Season.Name);
                    var path = Path.Combine(dir, fileName);
                    await File.WriteAllTextAsync(path, script);

                    var translationNote = ApplyPendingTranslationKeys();

                    MessageBox.Show(owner,
                        $"Wrote {Changes.Items.Count} change(s) to:\n{path}{translationNote}",
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

            // Extra guard for destructive operations (DELETE).
            var destructiveCount = Changes.Items.Count(c => c.IsDestructive);
            if (destructiveCount > 0)
            {
                var warn = MessageBox.Show(owner,
                    $"{destructiveCount} of {Changes.Items.Count} pending change(s) are destructive (DELETE).\n\n" +
                    "Applying directly to the database is irreversible from this tool.\n\n" +
                    "Continue?",
                    "Destructive operations pending",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (warn != MessageBoxResult.Yes) return;
            }

            var confirmVm = new ConfirmSqlViewModel(
                $"About to apply {Changes.Items.Count} change(s) directly to the database" +
                (destructiveCount > 0 ? $" ({destructiveCount} destructive)." : "."),
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

                // Commits may have touched entitydefaults / robottemplates — refresh both
                // caches so dropdowns reflect the new state on the next edit.
                try { await _session.Lookups.RefreshAllAsync(_store.Settings.Connection); }
                catch { /* non-fatal — per-tab Reload still works */ }

                var translationNote = ApplyPendingTranslationKeys();

                MessageBox.Show(owner,
                    $"Applied {Changes.Items.Count} change(s) successfully.{translationNote}",
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
