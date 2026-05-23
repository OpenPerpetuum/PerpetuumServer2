using System;
using System.Collections.ObjectModel;
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
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class SeasonsViewModel : ObservableObject
    {
        private readonly SeasonRepository _seasonRepo;
        private readonly PackageRepository _pkgRepo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;
        private readonly ConnectionSettings _connection;
        private readonly TranslationsViewModel? _translations;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        [ObservableProperty] private bool _showPackages;
        [ObservableProperty] private bool _isInDetail;
        [ObservableProperty] private SeasonDetailViewModel? _detailViewModel;

        public ObservableCollection<SeasonRow> Seasons { get; } = new();
        public PackagesViewModel PackagesVm { get; }

        public bool ShowSeasonsList => !ShowPackages && !IsInDetail;
        public bool ShowPackagesView => ShowPackages && !IsInDetail;

        public SeasonsViewModel(
            SeasonRepository seasonRepo,
            PackageRepository pkgRepo,
            ChangeQueue queue,
            LookupCache lookups,
            ConnectionSettings connection,
            TranslationsViewModel? translations = null)
        {
            _seasonRepo = seasonRepo;
            _pkgRepo = pkgRepo;
            _queue = queue;
            _lookups = lookups;
            _connection = connection;
            _translations = translations;
            PackagesVm = new PackagesViewModel(_pkgRepo, _seasonRepo, _queue, _lookups, _connection, translations);
        }

        partial void OnShowPackagesChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowSeasonsList));
            OnPropertyChanged(nameof(ShowPackagesView));
        }

        partial void OnIsInDetailChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowSeasonsList));
            OnPropertyChanged(nameof(ShowPackagesView));
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading seasons...";
            StatusIsError = false;
            try
            {
                var rows = await _seasonRepo.LoadAllSeasonsAsync();
                Seasons.Clear();
                foreach (var r in rows) Seasons.Add(r);

                await PackagesVm.LoadAsync();

                StatusMessage = $"Loaded {Seasons.Count} season(s).";
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

        [RelayCommand]
        private void ShowSeasons()
        {
            IsInDetail = false;
            ShowPackages = false;
        }

        [RelayCommand]
        private void ShowPackagesPanel()
        {
            IsInDetail = false;
            ShowPackages = true;
        }

        [RelayCommand]
        private void BackToList()
        {
            IsInDetail = false;
            DetailViewModel = null;
        }

        [RelayCommand]
        private void NavigateToSeason(SeasonRow? row)
        {
            if (row == null) return;
            var statsVm = new SeasonStatisticsViewModel(_seasonRepo);
            var detail = new SeasonDetailViewModel(
                row, _seasonRepo, _pkgRepo, _queue,
                PackagesVm, statsVm,
                _lookups, _connection, PackagesVm.Packages,
                _translations);
            DetailViewModel = detail;
            IsInDetail = true;
            _ = detail.LoadAsync();
        }

        [RelayCommand]
        private void NewSeason()
        {
            var wizardVm = new SeasonWizardViewModel(_queue, PackagesVm.Packages, () =>
            {
                StatusIsError = false;
                StatusMessage = "Wizard queued INSERT statements for new season.";
            });
            var win = new SeasonWizardWindow(wizardVm)
            {
                Owner = Application.Current?.MainWindow
            };
            win.ShowDialog();
        }
    }
}
