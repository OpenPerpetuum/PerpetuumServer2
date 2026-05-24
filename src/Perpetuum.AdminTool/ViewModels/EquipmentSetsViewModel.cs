using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Translations;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EquipmentSetsViewModel : ObservableObject
    {
        private readonly EquipmentSetRepository _repo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;
        private readonly TranslationsViewModel _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _newSetName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        [NotifyPropertyChangedFor(nameof(CanRename))]
        private EquipmentSetRow? _selectedSet;

        public bool HasSelection => SelectedSet != null;
        public bool CanRename    => SelectedSet is { IsNew: false };

        public ObservableCollection<EquipmentSetRow>          Sets                  { get; } = new();
        public ObservableCollection<EquipmentSetMemberRow>    Members               { get; } = new();
        public ObservableCollection<EquipmentSetThresholdRow> Thresholds            { get; } = new();
        public ObservableCollection<AggregateFieldPickItem>   AggregateFieldOptions { get; } = new();

        public EquipmentSetsViewModel(
            EquipmentSetRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel translations)
        {
            _repo         = repo;
            _queue        = queue;
            _lookups      = lookups;
            _translations = translations;
        }

        partial void OnSelectedSetChanged(EquipmentSetRow? value)
        {
            _ = LoadSetDetailAsync(value);
        }

        // ── Reload ───────────────────────────────────────────────────────────

        public async Task ReloadAsync()
        {
            IsLoading     = true;
            StatusMessage = "";
            StatusIsError = false;
            try
            {
                var sets   = await _repo.LoadAllSetsAsync();
                var fields = await _repo.LoadAggregateFieldsAsync();
                var store  = _translations.Store;

                Sets.Clear();
                foreach (var s in sets) Sets.Add(s);

                AggregateFieldOptions.Clear();
                foreach (var f in fields
                    .Select(f =>
                    {
                        var translated = "";
                        if (store != null)
                        {
                            var row = store.Rows.FirstOrDefault(r => r.Key == f.Name);
                            translated = row?[EnglishLangId] ?? "";
                        }
                        return new AggregateFieldPickItem { Id = f.Id, Name = f.Name, TranslatedName = translated };
                    })
                    .OrderBy(f => f.Display, StringComparer.OrdinalIgnoreCase))
                {
                    AggregateFieldOptions.Add(f);
                }

                var reselect = SelectedSet != null
                    ? Sets.FirstOrDefault(s => s.SetId == SelectedSet.SetId)
                    : null;
                SelectedSet = reselect;
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Reload failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        // ── Set detail load ───────────────────────────────────────────────────

        private async Task LoadSetDetailAsync(EquipmentSetRow? set)
        {
            Members.Clear();
            foreach (var t in Thresholds) t.PropertyChanged -= OnThresholdPropertyChanged;
            Thresholds.Clear();

            if (set == null || set.IsNew) return;

            IsLoading = true;
            try
            {
                var store      = _translations.Store;
                var members    = await _repo.LoadMembersAsync(set.SetId);
                var thresholds = await _repo.LoadThresholdsAsync(set.SetId);

                foreach (var m in members)
                {
                    var translated = "";
                    if (store != null)
                    {
                        var row = store.Rows.FirstOrDefault(r => r.Key == m.DefinitionName);
                        translated = row?[EnglishLangId] ?? "";
                    }
                    Members.Add(new EquipmentSetMemberRow
                    {
                        SetId          = m.SetId,
                        Definition     = m.Definition,
                        DefinitionName = m.DefinitionName,
                        TranslatedName = translated,
                    });
                }

                foreach (var t in thresholds)
                {
                    var display = AggregateFieldOptions
                        .FirstOrDefault(f => f.Id == t.AggregateFieldId)?.Display
                        ?? t.AggregateFieldId.ToString();
                    var row = new EquipmentSetThresholdRow
                    {
                        SetId            = t.SetId,
                        RequiredPieces   = t.RequiredPieces,
                        AggregateFieldId = t.AggregateFieldId,
                        FieldDisplay     = display,
                        BonusValue       = t.BonusValue,
                    };
                    row.PropertyChanged += OnThresholdPropertyChanged;
                    Thresholds.Add(row);
                }
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Failed to load set detail: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        // ── Threshold PropertyChanged → auto-UPSERT ───────────────────────────

        private void OnThresholdPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not EquipmentSetThresholdRow row) return;
            if (SelectedSet == null) return;

            if (e.PropertyName == nameof(EquipmentSetThresholdRow.AggregateFieldId))
            {
                row.FieldDisplay = AggregateFieldOptions
                    .FirstOrDefault(f => f.Id == row.AggregateFieldId)?.Display
                    ?? row.AggregateFieldId.ToString();
            }

            if (row.RequiredPieces > 0 && row.AggregateFieldId > 0)
            {
                _queue.Add(EquipmentSetChanges.BuildUpsertThreshold(
                    SelectedSet.SetId, SelectedSet.Name,
                    row.RequiredPieces, row.AggregateFieldId, row.BonusValue));
            }
        }

        // ── Create set ────────────────────────────────────────────────────────

        [RelayCommand]
        private void CreateSet()
        {
            var name = NewSetName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Set name is required.", isError: true);
                return;
            }
            if (Sets.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus($"A set named '{name}' already exists.", isError: true);
                return;
            }
            _queue.Add(EquipmentSetChanges.BuildInsertSet(name));
            var row = new EquipmentSetRow { SetId = 0, Name = name };
            Sets.Add(row);
            SelectedSet = row;
            NewSetName  = "";
            SetStatus($"'{name}' queued for insert. Commit to persist.", isError: false);
        }

        // ── Delete set ────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void DeleteSet()
        {
            if (SelectedSet == null) return;
            var memberCount    = Members.Count;
            var thresholdCount = Thresholds.Count;
            var msg =
                $"Delete set '{SelectedSet.Name}'?\n\n" +
                $"This will also remove {memberCount} member(s) and {thresholdCount} threshold row(s).\n\n" +
                "Continue?";
            if (MessageBox.Show(msg, "Delete set", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes) return;

            _queue.Add(EquipmentSetChanges.BuildDeleteSet(SelectedSet.SetId, SelectedSet.Name));
            Sets.Remove(SelectedSet);
            SelectedSet = null;
        }

        // ── Rename set ────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRename))]
        private void RenameSet(string newName)
        {
            if (SelectedSet == null) return;
            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                SetStatus("New name is required.", isError: true);
                return;
            }
            if (Sets.Any(s => !ReferenceEquals(s, SelectedSet) &&
                              string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus($"A set named '{newName}' already exists.", isError: true);
                return;
            }
            _queue.Add(EquipmentSetChanges.BuildRenameSet(SelectedSet.SetId, newName));
            SelectedSet.Name = newName;
            SetStatus($"Rename to '{newName}' queued.", isError: false);
        }

        // ── Add member ────────────────────────────────────────────────────────

        public void AddMember(Window owner)
        {
            if (SelectedSet == null) return;
            var assigned = Members.Select(m => m.Definition).ToHashSet();
            var vm  = new AddSetMemberViewModel(_lookups, _translations, assigned);
            var win = new AddSetMemberWindow(vm) { Owner = owner };
            if (win.ShowDialog() != true || vm.SelectedItem == null) return;

            var item = vm.SelectedItem;
            _queue.Add(EquipmentSetChanges.BuildInsertMember(SelectedSet.SetId, SelectedSet.Name, item.Definition));
            Members.Add(new EquipmentSetMemberRow
            {
                SetId          = SelectedSet.SetId,
                Definition     = item.Definition,
                DefinitionName = item.DefinitionName,
                TranslatedName = item.TranslatedName,
            });
        }

        // ── Remove member ─────────────────────────────────────────────────────

        [RelayCommand]
        private void RemoveMember(EquipmentSetMemberRow row)
        {
            if (SelectedSet == null) return;
            _queue.Add(EquipmentSetChanges.BuildDeleteMember(SelectedSet.SetId, SelectedSet.Name, row.Definition));
            Members.Remove(row);
        }

        // ── Add threshold ─────────────────────────────────────────────────────

        [RelayCommand]
        private void AddThreshold()
        {
            if (SelectedSet == null) return;
            var row = new EquipmentSetThresholdRow { SetId = SelectedSet.SetId };
            row.PropertyChanged += OnThresholdPropertyChanged;
            Thresholds.Add(row);
        }

        // ── Remove threshold ──────────────────────────────────────────────────

        [RelayCommand]
        private void RemoveThreshold(EquipmentSetThresholdRow row)
        {
            if (SelectedSet != null && row.RequiredPieces > 0 && row.AggregateFieldId > 0)
                _queue.Add(EquipmentSetChanges.BuildDeleteThreshold(
                    SelectedSet.SetId, SelectedSet.Name, row.RequiredPieces));
            row.PropertyChanged -= OnThresholdPropertyChanged;
            Thresholds.Remove(row);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusIsError = isError;
        }
    }
}
