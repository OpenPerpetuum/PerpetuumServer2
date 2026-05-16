using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class PackagesViewModel : ObservableObject
    {
        private readonly PackageRepository _repo;
        private readonly SeasonRepository _seasonRepo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;
        private readonly ConnectionSettings _connection;
        private readonly TranslationsViewModel? _translations;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private PackageRow? _selectedPackage;

        public ObservableCollection<PackageRow> Packages { get; } = new();
        public ObservableCollection<PackageRow> FilteredPackages { get; } = new();
        public ObservableCollection<PackageItemRow> SelectedPackageItems { get; } = new();
        public ObservableCollection<PackageUsageRow> SelectedPackageUsage { get; } = new();

        public ObservableCollection<PackageItemPickItem> PickItems { get; } = new();
        private Dictionary<int, string> _pickNamesByDefinition = new();

        public bool HasSelection => SelectedPackage != null;
        public bool IsActiveSeason => SelectedPackageUsage.Any(u => u.IsActive);
        public bool CanDeleteSelected => SelectedPackage != null && SelectedPackage.SeasonCount == 0;
        public bool CanSaveNewPackage => SelectedPackage is { IsNew: true, Id: 0 };

        public string UsageDescription
        {
            get
            {
                if (SelectedPackage == null) return "";
                if (SelectedPackageUsage.Count == 0) return "Not used by any season.";
                var lines = SelectedPackageUsage
                    .Select(u => $"• {u.SeasonName} — {u.Context}: {u.Detail}")
                    .ToList();
                return $"Used by {SelectedPackageUsage.Count} reference(s):\n" + string.Join("\n", lines);
            }
        }

        public PackagesViewModel(
            PackageRepository repo,
            SeasonRepository seasonRepo,
            ChangeQueue queue,
            LookupCache lookups,
            ConnectionSettings connection,
            TranslationsViewModel? translations = null)
        {
            _repo = repo;
            _seasonRepo = seasonRepo;
            _queue = queue;
            _lookups = lookups;
            _connection = connection;
            _translations = translations;
        }

        partial void OnFilterTextChanged(string value) => RefreshFilter();

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanDeleteSelected));
            OnPropertyChanged(nameof(CanSaveNewPackage));
            _ = LoadSelectedDetailAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading packages...";
            StatusIsError = false;
            try
            {
                var pkgs = await _repo.LoadAllPackagesAsync();
                Packages.Clear();
                foreach (var p in pkgs) Packages.Add(p);

                RebuildPickItems();
                RefreshFilter();

                if (SelectedPackage != null)
                {
                    var match = Packages.FirstOrDefault(p => p.Id == SelectedPackage.Id);
                    SelectedPackage = match;
                }
                else
                {
                    SelectedPackageItems.Clear();
                    SelectedPackageUsage.Clear();
                    OnPropertyChanged(nameof(UsageDescription));
                    OnPropertyChanged(nameof(IsActiveSeason));
                }

                StatusMessage = $"Loaded {Packages.Count} package(s).";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void RebuildPickItems()
        {
            var englishNames = BuildEnglishNameMap();
            var fresh = PackageItemPickItem.BuildFilteredList(_lookups.Entities, englishNames);
            PickItems.Clear();
            foreach (var p in fresh) PickItems.Add(p);
            _pickNamesByDefinition = fresh.ToDictionary(p => p.Definition, p => p.DisplayName);
        }

        private Dictionary<string, string> BuildEnglishNameMap()
        {
            var store = _translations?.Store;
            if (store == null) return new Dictionary<string, string>();
            var map = new Dictionary<string, string>(store.Rows.Count, System.StringComparer.Ordinal);
            foreach (var row in store.Rows)
            {
                var english = row[0];
                if (!string.IsNullOrEmpty(english))
                    map[row.Key] = english;
            }
            return map;
        }

        private void RefreshFilter()
        {
            FilteredPackages.Clear();
            var f = (FilterText ?? "").Trim();
            foreach (var p in Packages)
            {
                if (f.Length == 0 || p.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
                    FilteredPackages.Add(p);
            }
        }

        private async Task LoadSelectedDetailAsync()
        {
            SelectedPackageItems.Clear();
            SelectedPackageUsage.Clear();

            if (SelectedPackage == null || SelectedPackage.Id <= 0)
            {
                OnPropertyChanged(nameof(UsageDescription));
                OnPropertyChanged(nameof(IsActiveSeason));
                return;
            }

            try
            {
                var items = await _repo.LoadPackageItemsAsync(SelectedPackage.Id);
                foreach (var it in items)
                {
                    if (_pickNamesByDefinition.TryGetValue(it.Definition, out var name))
                        it.DisplayName = name;
                    else if (_lookups.EntityNamesByDefinition.TryGetValue(it.Definition, out var fallback))
                        it.DisplayName = fallback;
                    else
                        it.DisplayName = $"(def {it.Definition})";
                    it.SelectedPickItem = PickItems.FirstOrDefault(p => p.Definition == it.Definition);
                    SelectedPackageItems.Add(it);
                }

                var usage = await _repo.LoadSeasonUsageAsync(SelectedPackage.Id);
                foreach (var u in usage) SelectedPackageUsage.Add(u);
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Detail load failed: {ex.Message}";
            }

            OnPropertyChanged(nameof(UsageDescription));
            OnPropertyChanged(nameof(IsActiveSeason));
        }

        [RelayCommand]
        private void NewPackage()
        {
            var row = new PackageRow { Id = 0, Name = "New Package", IsNew = true, ItemCount = 0, SeasonCount = 0 };
            Packages.Add(row);
            RefreshFilter();
            SelectedPackage = row;
            StatusIsError = false;
            StatusMessage = "New package created locally. Edit the name, add items, then click 'Save Package'.";
        }

        [RelayCommand]
        private void SaveNewPackage()
        {
            if (SelectedPackage == null || !SelectedPackage.IsNew) return;
            if (string.IsNullOrWhiteSpace(SelectedPackage.Name))
            {
                MessageBox.Show("Package name cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var items = SelectedPackageItems.ToList();
            _queue.Add(PackageChanges.BuildInsertPackageWithItems(SelectedPackage.Name, items));
            SelectedPackageItems.Clear();
            StatusIsError = false;
            StatusMessage = $"Queued INSERT for package '{SelectedPackage.Name}' with {items.Count} item(s). Commit the queue to save.";
        }

        [RelayCommand]
        private void DeletePackage()
        {
            if (SelectedPackage == null) return;
            if (!CanDeleteSelected)
            {
                MessageBox.Show(
                    "Cannot delete a package that is referenced by any season. Remove its tier/leaderboard references first.",
                    "Package in use",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var ok = MessageBox.Show(
                $"Delete package '{SelectedPackage.Name}' (id {SelectedPackage.Id})? This is destructive.",
                "Delete package",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (SelectedPackage.Id > 0)
                _queue.Add(PackageChanges.BuildDeletePackage(SelectedPackage.Id));

            var row = SelectedPackage;
            SelectedPackage = null;
            Packages.Remove(row);
            RefreshFilter();
            StatusIsError = false;
            StatusMessage = $"Queued DELETE for package '{row.Name}'.";
        }

        [RelayCommand]
        private void AddItem()
        {
            if (SelectedPackage == null)
            {
                MessageBox.Show("Select a package first.", "No package",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (PickItems.Count == 0)
            {
                MessageBox.Show("Picker list is empty. Reload entities and try again.",
                    "No picks available",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var pick = PickItems[0];

            if (SelectedPackage.Id > 0)
            {
                _queue.Add(PackageChanges.BuildInsertPackageItem(SelectedPackage.Id, pick.Definition, 1));
            }

            var row = new PackageItemRow
            {
                Id = 0,
                PackageId = SelectedPackage.Id,
                Quantity = 1,
                IsNew = true
            };
            row.SelectedPickItem = pick;
            SelectedPackageItems.Add(row);
            SelectedPackage.ItemCount = SelectedPackage.ItemCount + 1;
            StatusIsError = false;
            var savedNote = SelectedPackage.Id > 0 ? "" : " (not yet queued — click 'Save Package' to queue)";
            StatusMessage = $"Added '{pick.DisplayName}' x1{savedNote}.";
        }

        [RelayCommand]
        private void RemoveItem(PackageItemRow? row)
        {
            if (row == null || SelectedPackage == null) return;
            var ok = MessageBox.Show(
                $"Remove item '{row.DisplayName}' x{row.Quantity}?",
                "Remove item",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0)
                _queue.Add(PackageChanges.BuildDeletePackageItem(row.Id));

            SelectedPackageItems.Remove(row);
            SelectedPackage.ItemCount = System.Math.Max(0, SelectedPackage.ItemCount - 1);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for package item id {row.Id}."
                : "Removed unsaved item.";
        }
    }
}
