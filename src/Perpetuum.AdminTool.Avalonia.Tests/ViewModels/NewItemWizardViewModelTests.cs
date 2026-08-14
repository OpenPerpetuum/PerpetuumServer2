using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class NewItemWizardViewModelTests
{
    [Fact]
    public async Task LoadAndQueue_CreatesCompleteCraftablePrototypeChange()
    {
        var queue = new ChangeQueue();
        var repository = new StubNewItemRepository();
        var viewModel = new NewItemWizardViewModel(
            repository,
            new StubEntityRepository(),
            queue);

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.BasicPanel.IsCraftable = true;
        viewModel.BasicPanel.HasPrototype = true;
        viewModel.BasicPanel.DefinitionName = "def_native_item";
        viewModel.BasicPanel.CategoryFlags = 256;
        viewModel.StatsPanel.Rows.Add(new NewStatRow { FieldId = 1, NewValue = 12.5 });
        viewModel.ProductionPanel.Components.Add(new NewComponentRow
        {
            IngredientDefinition = 77,
            Amount = 3
        });
        viewModel.ResearchPanel.ResearchCostRows.Add(new ResearchCostRow
        {
            PointTypeId = 2,
            Amount = 100
        });
        viewModel.OptionsVisualPanel.HasDefinitionConfig = true;
        viewModel.OptionsVisualPanel.DefinitionConfigRows.Add(new DefinitionConfigRow
        {
            ColumnName = "tint",
            RawValue = "#AABBCC"
        });

        viewModel.QueueItemCommand.Execute(null);

        RawSqlChange change = Assert.IsType<RawSqlChange>(Assert.Single(queue.Items));
        string sql = change.ToSql();
        Assert.Contains("INSERT INTO entitydefaults", sql);
        Assert.Contains("@cprgDef", sql);
        Assert.Contains("@prDef", sql);
        Assert.Contains("INSERT INTO aggregatevalues", sql);
        Assert.Contains("INSERT INTO components", sql);
        Assert.Contains("INSERT INTO itemresearchlevels", sql);
        Assert.Contains("INSERT INTO techtreenodeprices", sql);
        Assert.Contains("INSERT INTO definitionconfig", sql);
        Assert.Contains("def_native_item_desc", queue.PendingNewEntityNames);
        Assert.True(viewModel.IsQueued);
    }

    [Fact]
    public async Task Queue_RejectsAnExistingDefinitionName()
    {
        var queue = new ChangeQueue();
        var viewModel = new NewItemWizardViewModel(
            new StubNewItemRepository(), new StubEntityRepository(), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.BasicPanel.DefinitionName = "def_existing";
        viewModel.BasicPanel.CategoryFlags = 1;

        viewModel.QueueItemCommand.Execute(null);

        Assert.Empty(queue.Items);
        Assert.True(viewModel.StatusIsError);
        Assert.Contains("unique", viewModel.StatusMessage);
    }

    private sealed class StubEntityRepository : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync()
        {
            var existing = new EntityDefaultRow(new EntityDefaultSnapshot
            {
                Definition = 77,
                DefinitionName = "def_existing",
                CategoryFlags = 256,
                Enabled = true,
                Purchasable = true
            });
            return Task.FromResult(new EntitiesSnapshot
            {
                Rows = [existing],
                Fields = new Dictionary<int, AggregateFieldInfo>
                {
                    [1] = new() { Id = 1, Name = "mass" }
                }
            });
        }
    }

    private sealed class StubNewItemRepository : INewItemRepository
    {
        public Task<NewItemLookups> LoadAsync(
            IReadOnlyList<AggregateFieldInfo> aggregateFields,
            IReadOnlyList<EntityPickItem> entities,
            Dictionary<string, string>? englishNames = null)
        {
            return Task.FromResult(new NewItemLookups
            {
                AggregateFields = aggregateFields,
                EnabledItems = [new PackageItemPickItem(77, "Existing")],
                PointTypes = [new PointTypePickItem(2, "research")],
                DefinitionConfigColumns = [new DefinitionConfigColumnInfo("tint", "nvarchar")]
            });
        }

        public Task<CloneExtendedData> LoadCloneExtendedAsync(int definition) =>
            Task.FromResult(new CloneExtendedData());
    }
}
