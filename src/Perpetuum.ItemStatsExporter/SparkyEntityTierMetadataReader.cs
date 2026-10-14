using Perpetuum.Data;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyEntityTierMetadata
{
    public required int? TierType { get; init; }
    public required int? TierLevel { get; init; }
}

internal static class SparkyEntityTierMetadataReader
{
    private const string Query = "SELECT definition, tiertype, tierlevel FROM dbo.entitydefaults WHERE enabled = 1";

    public static IReadOnlyDictionary<int, SparkyEntityTierMetadata> Load()
    {
        var metadata = new Dictionary<int, SparkyEntityTierMetadata>();
        foreach (var record in Db.Query().CommandText(Query).Execute())
        {
            int definition = record.GetValue<int>("definition");
            var tier = new SparkyEntityTierMetadata
            {
                TierType = record.GetValue<int?>("tiertype"),
                TierLevel = record.GetValue<int?>("tierlevel")
            };

            if (!metadata.TryAdd(definition, tier))
            {
                throw new InvalidDataException($"Entity-default tier metadata contains duplicate definition ID {definition}.");
            }
        }

        return metadata;
    }
}
