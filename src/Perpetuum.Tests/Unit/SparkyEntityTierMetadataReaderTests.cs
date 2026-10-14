using Perpetuum.Data;
using Perpetuum.ItemStatsExporter;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit;

[Collection(PerpetuumStaticsCollection.Name)]
public sealed class SparkyEntityTierMetadataReaderTests
{
    private const string Query = "SELECT definition, tiertype, tierlevel FROM dbo.entitydefaults WHERE enabled = 1";
    private readonly FakeDb _db;

    public SparkyEntityTierMetadataReaderTests(PerpetuumStaticsFixture fixture)
    {
        _ = fixture;
        _db = FakeDb.Install();
    }

    [Fact]
    public void LoadPreservesNullAndNumericTierValuesFromEnabledEntityDefaults()
    {
        _db.When(Query, FakeResultSet.FromRows(
            ["definition", "tiertype", "tierlevel"],
            [101, null, null],
            [102, 2, null],
            [103, 2, 0],
            [104, 2, 1]));

        IReadOnlyDictionary<int, SparkyEntityTierMetadata> metadata = SparkyEntityTierMetadataReader.Load();

        Assert.Equal(new SparkyEntityTierMetadata { TierType = null, TierLevel = null }, metadata[101]);
        Assert.Equal(new SparkyEntityTierMetadata { TierType = 2, TierLevel = null }, metadata[102]);
        Assert.Equal(new SparkyEntityTierMetadata { TierType = 2, TierLevel = 0 }, metadata[103]);
        Assert.Equal(new SparkyEntityTierMetadata { TierType = 2, TierLevel = 1 }, metadata[104]);
        Assert.Equal(Query, Assert.Single(_db.Commands).CommandText);
    }

    [Fact]
    public void LoadRejectsDuplicateDefinitionIds()
    {
        _db.When(Query, FakeResultSet.FromRows(
            ["definition", "tiertype", "tierlevel"],
            [101, 1, 1],
            [101, 1, 2]));

        Assert.Throws<InvalidDataException>(() => SparkyEntityTierMetadataReader.Load());
    }
}
