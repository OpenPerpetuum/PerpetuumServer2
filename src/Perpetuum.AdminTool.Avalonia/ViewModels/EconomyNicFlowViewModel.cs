using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class EconomyNicFlowViewModel : ObservableObject
{
    private readonly IEconomyRepository _repository;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Load the current NIC sources and sinks from the server database.";

    public EconomyNicFlowViewModel(IEconomyRepository repository)
    {
        _repository = repository;
    }

    public ObservableCollection<EconomyNicFlowRow> NicIn { get; } = new();

    public ObservableCollection<EconomyNicFlowRow> NicOut { get; } = new();

    public bool IsNotLoading => !IsLoading;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading NIC flow...";
        try
        {
            (List<EconomyNicFlowRow> nicIn, List<EconomyNicFlowRow> nicOut) =
                await _repository.LoadNicFlowAsync();
            Replace(NicIn, nicIn);
            Replace(NicOut, nicOut);
            StatusMessage = $"Loaded {nicIn.Count + nicOut.Count} NIC flow rows.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load NIC flow: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void Replace(
        ObservableCollection<EconomyNicFlowRow> destination,
        IEnumerable<EconomyNicFlowRow> source)
    {
        destination.Clear();
        foreach (EconomyNicFlowRow row in source)
        {
            destination.Add(row);
        }
    }
}
