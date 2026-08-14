using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class PendingChangesViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private readonly IChangeApplier _changeApplier;
    private readonly ISqlScriptExporter _scriptExporter;
    private readonly string _authorEmail;

    [ObservableProperty] private string _outputDirectory;
    [ObservableProperty] private string _confirmationText = string.Empty;
    [ObservableProperty] private string _scriptPreview = "No pending changes.";
    [ObservableProperty] private string _statusMessage =
        "Editing modules will place their proposed database changes here.";
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private bool _isBusy;

    public PendingChangesViewModel(
        AppSettingsStore settingsStore,
        ChangeQueue queue,
        IChangeApplier changeApplier,
        ISqlScriptExporter scriptExporter,
        string authorEmail)
    {
        _settingsStore = settingsStore;
        Queue = queue;
        _changeApplier = changeApplier;
        _scriptExporter = scriptExporter;
        _authorEmail = authorEmail;
        _outputDirectory = string.IsNullOrWhiteSpace(settingsStore.Settings.SqlOutputDirectory)
            ? GetDefaultOutputDirectory()
            : settingsStore.Settings.SqlOutputDirectory;

        Queue.Items.CollectionChanged += OnQueueChanged;
        RefreshQueueState();
    }

    public ChangeQueue Queue { get; }

    public ObservableCollection<IPendingChange> Items => Queue.Items;

    public int PendingChangeCount => Items.Count;

    public int DestructiveCount => Items.Count(change => change.IsDestructive);

    public bool HasPending => Queue.HasPending;

    public string RequiredConfirmation => DestructiveCount > 0 ? "APPLY DELETE" : "APPLY";

    public string ConfirmationPrompt =>
        HasPending
            ? $"Type {RequiredConfirmation} to enable direct database application."
            : "Queue at least one change before applying.";

    partial void OnConfirmationTextChanged(string value)
    {
        ApplyDirectCommand.NotifyCanExecuteChanged();
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        ExportScriptCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ExportScriptCommand.NotifyCanExecuteChanged();
        ApplyDirectCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanExportScript))]
    private async Task ExportScriptAsync()
    {
        IsBusy = true;
        StatusIsError = false;
        StatusMessage = "Writing SQL script...";
        try
        {
            IPendingChange[] changes = Items.ToArray();
            string directory = OutputDirectory.Trim();
            string path = await _scriptExporter.ExportAsync(
                directory,
                "perpetuum_changes",
                changes,
                _authorEmail);

            _settingsStore.Settings.SqlOutputDirectory = directory;
            _settingsStore.Save();
            Queue.Remove(changes);
            StatusMessage = $"Exported {changes.Length} change(s) to {path}.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to export SQL script: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyDirect))]
    private async Task ApplyDirectAsync()
    {
        string requiredConfirmation = RequiredConfirmation;
        if (!string.Equals(ConfirmationText.Trim(), requiredConfirmation, StringComparison.Ordinal))
        {
            StatusIsError = true;
            StatusMessage = $"Type {requiredConfirmation} exactly before applying changes.";
            return;
        }

        IsBusy = true;
        StatusIsError = false;
        IPendingChange[] changes = Items.ToArray();
        StatusMessage = $"Applying {changes.Length} change(s) in one transaction...";
        try
        {
            await _changeApplier.ExecuteAsync(changes, _authorEmail);
            Queue.Remove(changes);
            StatusMessage = $"Applied {changes.Length} change(s) successfully.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to apply changes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        int count = Items.Count;
        Queue.Clear();
        StatusIsError = false;
        StatusMessage = $"Discarded {count} pending change(s).";
    }

    private bool CanExportScript()
    {
        return HasPending && !IsBusy && !string.IsNullOrWhiteSpace(OutputDirectory);
    }

    private bool CanApplyDirect()
    {
        return HasPending &&
               !IsBusy &&
               string.Equals(
                   ConfirmationText.Trim(),
                   RequiredConfirmation,
                   StringComparison.Ordinal);
    }

    private bool CanClear()
    {
        return HasPending && !IsBusy;
    }

    private void OnQueueChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ConfirmationText = string.Empty;
        RefreshQueueState();
    }

    private void RefreshQueueState()
    {
        ScriptPreview = HasPending
            ? SqlScriptBuilder.Build(Items, _authorEmail)
            : "No pending changes.";
        OnPropertyChanged(nameof(PendingChangeCount));
        OnPropertyChanged(nameof(DestructiveCount));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(RequiredConfirmation));
        OnPropertyChanged(nameof(ConfirmationPrompt));
        ExportScriptCommand.NotifyCanExecuteChanged();
        ApplyDirectCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private static string GetDefaultOutputDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return Path.Combine(documents, "PerpetuumAdminTool", "sql");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PerpetuumAdminTool",
            "sql");
    }
}
