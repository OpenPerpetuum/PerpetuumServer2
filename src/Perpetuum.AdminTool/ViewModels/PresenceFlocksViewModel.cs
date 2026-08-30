using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class PresenceFlocksViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;

        public int PresenceId { get; }
        public string PresenceName { get; }

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        public ObservableCollection<FlockSummary> Flocks { get; } = new();

        public PresenceFlocksViewModel(AppSettingsStore settings, int presenceId, string presenceName)
        {
            _settings = settings;
            PresenceId = presenceId;
            PresenceName = presenceName;
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading flocks...";
            StatusIsError = false;
            try
            {
                var repo = new FlockRepository(_settings.Settings.Connection);
                var rows = await repo.LoadByPresenceAsync(PresenceId);
                Flocks.Clear();
                foreach (var r in rows) Flocks.Add(r);
                StatusMessage = $"{Flocks.Count} flock(s) reference this presence.";
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
    }
}
