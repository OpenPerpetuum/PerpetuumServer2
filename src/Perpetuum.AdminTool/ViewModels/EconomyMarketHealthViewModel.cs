using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyMarketHealthViewModel : ObservableObject
    {
        private readonly EconomyMarketHealthRepository _repo;
        private readonly ChangeQueue                   _changes;
        private readonly LookupCache                   _lookups;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        [ObservableProperty] private int _ageBucketToday;
        [ObservableProperty] private int _ageBucketD1To7;
        [ObservableProperty] private int _ageBucketD7To30;
        [ObservableProperty] private int _ageBucketD30Plus;
        [ObservableProperty] private int _autoMarketOrderCount;
        [ObservableProperty] private int _playerOrderCount;

        [ObservableProperty] private EntityPickItem? _selectedNewItem;

        public ObservableCollection<EconomyVelocityRow>          VelocityRows   { get; } = new();
        public ObservableCollection<EconomyPriceIndexRow>         PriceIndexRows { get; } = new();
        public ObservableCollection<EconomyPriceIndexBasketItem>  BasketItems    { get; } = new();
        public ObservableCollection<EntityPickItem>               AvailableItems => _lookups.Entities;

        public EconomyMarketHealthViewModel(
            EconomyMarketHealthRepository repo,
            ChangeQueue changes,
            LookupCache lookups)
        {
            _repo    = repo;
            _changes = changes;
            _lookups = lookups;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var marketData = await _repo.LoadMarketDataAsync();
                var basket     = await _repo.LoadBasketAsync();

                VelocityRows.Clear();
                foreach (var r in marketData.VelocityRows)   VelocityRows.Add(r);

                PriceIndexRows.Clear();
                foreach (var r in marketData.PriceIndexRows) PriceIndexRows.Add(r);

                AgeBucketToday   = marketData.AgeBuckets.Today;
                AgeBucketD1To7   = marketData.AgeBuckets.D1To7;
                AgeBucketD7To30  = marketData.AgeBuckets.D7To30;
                AgeBucketD30Plus = marketData.AgeBuckets.D30Plus;
                AutoMarketOrderCount = marketData.AutoMarketOrderCount;
                PlayerOrderCount     = marketData.PlayerOrderCount;

                BasketItems.Clear();
                foreach (var b in basket) BasketItems.Add(b);

                StatusMessage = $"Loaded at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void QueueSaveBasketItem(EconomyPriceIndexBasketItem item)
        {
            var desc = $"economy_price_index_basket: update id={item.Id}";
            var existing = _changes.Items.FirstOrDefault(c => c.Description == desc);
            if (existing != null) _changes.Items.Remove(existing);

            _changes.Add(new RawSqlChange(desc,
                $"UPDATE economy_price_index_basket SET weight = {SqlLiteral.Of(item.Weight)} WHERE id = {SqlLiteral.Of(item.Id)}"));
            StatusMessage = $"Weight change for '{item.DefinitionName}' queued.";
        }

        [RelayCommand]
        private void RemoveBasketItem(EconomyPriceIndexBasketItem item)
        {
            BasketItems.Remove(item);
            if (item.Id > 0)
            {
                _changes.Add(new RawSqlChange(
                    $"economy_price_index_basket: delete id={item.Id}",
                    $"DELETE FROM economy_price_index_basket WHERE id = {SqlLiteral.Of(item.Id)}",
                    isDestructive: true));
            }
            StatusMessage = $"'{item.DefinitionName}' removed from basket (queued).";
        }

        [RelayCommand]
        private void AddBasketItem()
        {
            if (SelectedNewItem == null) return;
            if (BasketItems.Any(b => b.Definition == SelectedNewItem.Definition))
            {
                StatusMessage = $"'{SelectedNewItem.Name}' is already in the basket.";
                StatusIsError = true;
                return;
            }

            var newItem = new EconomyPriceIndexBasketItem
            {
                Id             = 0,
                Definition     = SelectedNewItem.Definition,
                DefinitionName = SelectedNewItem.Name,
            };
            newItem.Weight = 1.0;
            BasketItems.Add(newItem);

            _changes.Add(new RawSqlChange(
                $"economy_price_index_basket: insert {SelectedNewItem.Name}",
                $"INSERT INTO economy_price_index_basket (definition, weight) VALUES ({SqlLiteral.Of(SelectedNewItem.Definition)}, 1.0)"));

            StatusMessage = $"'{SelectedNewItem.Name}' added to basket (queued).";
            SelectedNewItem = null;
            StatusIsError   = false;
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();
    }
}
