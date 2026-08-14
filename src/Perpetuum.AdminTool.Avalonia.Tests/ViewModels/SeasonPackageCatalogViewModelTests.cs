using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class SeasonPackageCatalogViewModelTests
{
    [Fact]
    public async Task Load_PopulatesPackagesSeasonsEquipmentAndAllActivityTypes()
    {
        var viewModel = Create(new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedSeason = viewModel.Seasons[0];
        await WaitUntilAsync(() => viewModel.ActivityRates.Count == Enum.GetValues<SeasonActivityType>().Length);

        Assert.Single(viewModel.Packages);
        Assert.Single(viewModel.Seasons);
        Assert.Single(viewModel.EquipmentSets);
        Assert.Equal(Enum.GetValues<SeasonActivityType>().Length, viewModel.ActivityRates.Count);
        Assert.Contains(viewModel.ActivityRates, row => row.ActivityType == SeasonActivityType.NpcKill && row.PointsPerUnit == 2);
        Assert.Single(viewModel.Objectives);
        Assert.Equal("Reward", viewModel.Objectives[0].SelectedPackage?.Name);
    }

    [Fact]
    public void NewPackage_QueuesPackageAndContentsAsOneAtomicBatch()
    {
        var queue = new ChangeQueue();
        var viewModel = Create(queue);
        viewModel.NewPackageName = "Starter Rewards";

        viewModel.QueueNewPackageCommand.Execute(null);
        viewModel.PackageItems.Add(new PackageItemRow { Definition = 123, Quantity = 4 });
        viewModel.QueueSaveNewPackageCommand.Execute(null);

        string sql = Assert.Single(queue.Items).ToSql();
        Assert.Contains("DECLARE @pkgId_", sql);
        Assert.Contains("INSERT INTO packages", sql);
        Assert.Contains("INSERT INTO packageitems", sql);
        Assert.Contains("123, 4", sql);
    }

    [Fact]
    public void Wizard_QueuesCompleteSeasonBatch()
    {
        var queue = new ChangeQueue();
        var viewModel = Create(queue);
        viewModel.Packages.Add(new PackageRow { Id = 7, Name = "Reward" });
        viewModel.StartSeasonWizardCommand.Execute(null);
        var wizard = Assert.IsType<Perpetuum.AdminTool.ViewModels.SeasonWizardViewModel>(viewModel.Wizard);
        wizard.Name = "Linux Season";
        wizard.EndTime = wizard.StartTime.AddDays(14);
        wizard.ActivityRates[0].PointsPerUnit = 2;
        wizard.AddObjectiveRowCommand.Execute(null);
        wizard.AddTierRowCommand.Execute(null);
        wizard.AddLeaderboardRowCommand.Execute(null);

        wizard.FinishCommand.Execute(null);

        string sql = Assert.Single(queue.Items).ToSql();
        Assert.Contains("INSERT INTO seasons", sql);
        Assert.Contains("season_activity_rates", sql);
        Assert.Contains("season_objectives", sql);
        Assert.Contains("season_tiers", sql);
        Assert.Contains("season_leaderboard_rewards", sql);
    }

    [Fact]
    public void ExistingSeasonRemoval_IsMarkedDestructive()
    {
        var queue = new ChangeQueue();
        var viewModel = Create(queue);
        var objective = new SeasonObjectiveRow { Id = 9, SeasonId = 5, Name = "Old objective" };
        viewModel.Objectives.Add(objective);
        viewModel.SelectedObjective = objective;

        viewModel.QueueRemoveSelectedObjectiveCommand.Execute(null);

        Assert.True(Assert.Single(queue.Items).IsDestructive);
        Assert.Empty(viewModel.Objectives);
    }

    [Fact]
    public async Task ExportSelectedSeason_ShowsPortableSql()
    {
        var viewModel = new SeasonPackageCatalogViewModel(
            new StubPackageRepository(), new StubSeasonRepository(), new StubEntityRepository(),
            new ChangeQueue(), contentExporter: new StubContentExporter());
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedSeason = viewModel.Seasons[0];

        await viewModel.ExportSelectedSeasonCommand.ExecuteAsync(null);

        Assert.Equal("season export 5", viewModel.ExportScript);
    }

    private static SeasonPackageCatalogViewModel Create(ChangeQueue queue) => new(
        new StubPackageRepository(), new StubSeasonRepository(), new StubEntityRepository(), queue,
        key => $"translated:{key}");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class StubPackageRepository : IPackageRepository
    {
        public Task<List<PackageRow>> LoadAllPackagesAsync() => Task.FromResult(new List<PackageRow>
        {
            new() { Id = 7, Name = "Reward", ItemCount = 1 }
        });
        public Task<List<PackageItemRow>> LoadPackageItemsAsync(int packageId) => Task.FromResult(new List<PackageItemRow>());
        public Task<List<PackageUsageRow>> LoadSeasonUsageAsync(int packageId) => Task.FromResult(new List<PackageUsageRow>());
    }

    private sealed class StubEntityRepository : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync() => Task.FromResult(new EntitiesSnapshot());
    }

    private sealed class StubSeasonRepository : ISeasonRepository
    {
        public Task<List<SeasonRow>> LoadAllSeasonsAsync() => Task.FromResult(new List<SeasonRow>
        {
            new(new SeasonSnapshot { Id = 5, Name = "Season", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddDays(7) })
        });
        public Task<List<SeasonActivityRateRow>> LoadActivityRatesAsync(int seasonId) => Task.FromResult(new List<SeasonActivityRateRow>
        {
            new() { Id = 1, SeasonId = seasonId, ActivityType = SeasonActivityType.NpcKill, PointsPerUnit = 2, UnitScale = 1 }
        });
        public Task<List<SeasonObjectiveRow>> LoadObjectivesAsync(int seasonId) => Task.FromResult(new List<SeasonObjectiveRow>
        {
            new() { Id = 2, SeasonId = seasonId, Name = "Objective", PackageId = 7 }
        });
        public Task<List<SeasonTierRow>> LoadTiersAsync(int seasonId) => Task.FromResult(new List<SeasonTierRow>());
        public Task<List<SeasonLeaderboardRewardRow>> LoadLeaderboardRewardsAsync(int seasonId) => Task.FromResult(new List<SeasonLeaderboardRewardRow>());
        public Task<int> LoadParticipantCountAsync(int seasonId) => Task.FromResult(3);
        public Task<int> LoadActiveLast7DaysAsync(int seasonId) => Task.FromResult(2);
        public Task<List<TierDistributionRow>> LoadTierDistributionAsync(int seasonId) => Task.FromResult(new List<TierDistributionRow>());
        public Task<List<LeaderboardEntryRow>> LoadTop10LeaderboardAsync(int seasonId) => Task.FromResult(new List<LeaderboardEntryRow>());
        public Task<List<ObjectiveCompletionRow>> LoadObjectiveCompletionAsync(int seasonId) => Task.FromResult(new List<ObjectiveCompletionRow>());
        public Task<double> LoadAvgPointsPerDayAsync(int seasonId) => Task.FromResult(1.5);
        public Task<List<EquipmentSetRow>> LoadEquipmentSetsAsync() => Task.FromResult(new List<EquipmentSetRow>
        {
            new() { SetId = 8, Name = "Equipment" }
        });
        public Task<List<TodaysDailyObjectiveRow>> LoadTodaysDailyObjectivesAsync(int seasonId) => Task.FromResult(new List<TodaysDailyObjectiveRow>());
    }
}
