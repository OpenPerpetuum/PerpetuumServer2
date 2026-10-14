using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyRobotDictionaryLabelTests
{
    [Fact]
    public void EnglishAggregateStatWithoutDictionaryLabelIsOmitted()
    {
        var metadata = new AggregateFieldMetadata
        {
            Id = 765,
            EnumName = "missileMiss",
            LabelTranslationKey = "Aggregate_unknown_765",
            MeasurementUnitKey = "percent",
            MeasurementUnit = "%",
            MeasurementMultiplier = 1,
            Digits = 0
        };
        var stat = new StatValue
        {
            FieldId = metadata.Id,
            FieldEnumName = metadata.EnumName,
            LabelTranslationKey = metadata.LabelTranslationKey,
            Label = "Missile miss",
            Value = 90,
            Metadata = metadata
        };

        AnswerStat? answer = Program.BuildAnswerStat(
            stat,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["percent"] = "%" },
            languageId: 0);

        Assert.Null(answer);
    }

    [Fact]
    public void EnglishAggregateStatUsesDictionaryLabelWhenPresent()
    {
        var metadata = new AggregateFieldMetadata
        {
            Id = 765,
            EnumName = "missileMiss",
            LabelTranslationKey = "Aggregate_unknown_765",
            MeasurementUnitKey = "percent",
            MeasurementUnit = "%",
            MeasurementMultiplier = 1,
            Digits = 0
        };
        var stat = new StatValue
        {
            FieldId = metadata.Id,
            FieldEnumName = metadata.EnumName,
            LabelTranslationKey = metadata.LabelTranslationKey,
            Label = "Missile miss",
            Value = 90,
            Metadata = metadata
        };

        AnswerStat? answer = Program.BuildAnswerStat(
            stat,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Aggregate_unknown_765"] = "Missile guidance accuracy",
                ["percent"] = "%"
            },
            languageId: 0);

        Assert.NotNull(answer);
        Assert.Equal("Missile guidance accuracy", answer.Label);
    }
}
