using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyProfileValidatorTests
{
    private static readonly IReadOnlySet<int> Languages = new HashSet<int> { 0, 1, 2, 3 };

    [Fact]
    public void Accepts_a_bounded_profile_with_literal_supported_route_aliases()
    {
        var profile = Draft() with
        {
            PersonaStyle = "Clear and practical, with occasional dry humor.",
            RouteAliases =
            [
                new SparkyRouteAlias("show recipe", SparkyIntent.StructuredItem, Priority: 10, LanguageId: 0)
            ]
        };

        var errors = SparkyProfileValidator.Validate(profile, Languages, globalConcurrencyLimit: 2);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_invalid_intents_unbounded_limits_and_unknown_language_ids()
    {
        var profile = Draft() with
        {
            MaxAnswerCharacters = 2001,
            MaxConcurrentRequests = 3,
            RouteAliases =
            [
                new SparkyRouteAlias("regex .*", (SparkyIntent)999, Priority: 101, LanguageId: 99)
            ]
        };

        var errors = SparkyProfileValidator.Validate(profile, Languages, globalConcurrencyLimit: 2);

        Assert.Contains(errors, error => error.Contains("MaxAnswerCharacters", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("MaxConcurrentRequests", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("RouteAliases", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_regex_and_script_like_aliases_without_compiling_user_patterns()
    {
        var profile = Draft() with
        {
            RouteAliases =
            [
                new SparkyRouteAlias("(.*)", SparkyIntent.MechanicsLore),
                new SparkyRouteAlias("<script>alert(1)</script>", SparkyIntent.StructuredItem)
            ]
        };

        var errors = SparkyProfileValidator.Validate(profile, Languages, globalConcurrencyLimit: 2);

        Assert.Contains(errors, error => error.Contains("RouteAliases", StringComparison.Ordinal));
    }

    [Fact]
    public void Requires_metadata_retention_to_cover_content_retention()
    {
        var profile = Draft() with
        {
            ContentRetentionDays = 60,
            MetadataRetentionDays = 30
        };

        var errors = SparkyProfileValidator.Validate(profile, Languages, globalConcurrencyLimit: 2);

        Assert.Contains(errors, error => error.Contains("retention", StringComparison.OrdinalIgnoreCase));
    }

    private static SparkyProfileDraft Draft() => new(
        SchemaVersion: 1,
        ModelId: "qwen3-4b-instruct",
        MaxAnswerCharacters: 2000,
        MaxCompletionTokens: 700,
        InferenceTimeoutSeconds: 120,
        MaxConcurrentRequests: 1,
        CaptureContent: true,
        ContentRetentionDays: 30,
        MetadataRetentionDays: 90,
        PersonaStyle: "Practical and concise.",
        Retrieval: new SparkyRetrievalSettings(MaxPassages: 5, MaxCharacters: 4200, MinimumScore: 0.25),
        RouteAliases: [],
        BundleVersion: "legacy-1234567890abcdef");
}
