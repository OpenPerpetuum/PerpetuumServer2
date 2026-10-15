using System.Globalization;
using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyProfileValidator
{
    private static readonly Regex ModelIdPattern = new("\\A[A-Za-z0-9][A-Za-z0-9._:/-]{0,127}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex BundleVersionPattern = new("\\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedSourceCategories = new(StringComparer.Ordinal)
    {
        "mechanics",
        "lore"
    };

    public static IReadOnlyList<string> Validate(
        SparkyProfileDraft? profile,
        IReadOnlySet<int> supportedLanguageIds,
        int globalConcurrencyLimit)
    {
        var errors = new List<string>();
        if (profile is null)
        {
            return ["Profile draft is required."];
        }

        if (profile.SchemaVersion != 2)
            errors.Add("SchemaVersion must be 2.");
        if (string.IsNullOrWhiteSpace(profile.ModelId) || !ModelIdPattern.IsMatch(profile.ModelId))
            errors.Add("ModelId contains unsupported characters or exceeds 128 characters.");
        if (profile.MaxAnswerCharacters is < 80 or > 2000)
            errors.Add("MaxAnswerCharacters must be between 80 and 2000.");
        if (profile.MaxCompletionTokens is < 32 or > 4096)
            errors.Add("MaxCompletionTokens must be between 32 and 4096.");
        if (profile.InferenceTimeoutSeconds is < 5 or > 300)
            errors.Add("InferenceTimeoutSeconds must be between 5 and 300.");
        if (profile.ConversationHistoryTurns is < 2 or > 16 || profile.ConversationHistoryTurns % 2 != 0)
            errors.Add("ConversationHistoryTurns must be an even number between 2 and 16.");
        if (profile.ConversationHistoryCharacters is < 256 or > 8000)
            errors.Add("ConversationHistoryCharacters must be between 256 and 8000.");
        if (!double.IsFinite(profile.AnswerTemperature) || profile.AnswerTemperature is < 0 or > 2)
            errors.Add("AnswerTemperature must be between 0 and 2.");
        if (!double.IsFinite(profile.AnswerTopP) || profile.AnswerTopP is < 0.01 or > 1)
            errors.Add("AnswerTopP must be between 0.01 and 1.");
        if (profile.LiveKnowledge is { CurrentSnapshotMaxAgeMinutes: < 5 or > 1440 })
            errors.Add("LiveKnowledge.CurrentSnapshotMaxAgeMinutes must be between 5 and 1440.");
        if (globalConcurrencyLimit < 1 || profile.MaxConcurrentRequests is < 1 || profile.MaxConcurrentRequests > globalConcurrencyLimit)
            errors.Add($"MaxConcurrentRequests must be between 1 and the code-owned global limit of {Math.Max(1, globalConcurrencyLimit)}.");
        if (profile.ContentRetentionDays is < 1 or > 3650 || profile.MetadataRetentionDays is < 1 or > 3650 ||
            profile.MetadataRetentionDays < profile.ContentRetentionDays)
            errors.Add("Profile retention must be between 1 and 3650 days, with metadata retention at least as long as content retention.");
        if (string.IsNullOrWhiteSpace(profile.PersonaStyle) || profile.PersonaStyle.Length > 500 ||
            profile.PersonaStyle.Any(char.IsControl) || profile.PersonaStyle.Contains('<') || profile.PersonaStyle.Contains('>'))
            errors.Add("PersonaStyle must be plain text of at most 500 characters.");
        if (!BundleVersionPattern.IsMatch(profile.BundleVersion ?? string.Empty))
            errors.Add("BundleVersion must be a version identifier, not a path.");

        if (profile.Retrieval is null || profile.Retrieval.MaxPassages is < 1 or > 8 ||
            profile.Retrieval.MaxCharacters is < 200 or > 8000 ||
            !double.IsFinite(profile.Retrieval.MinimumScore) || profile.Retrieval.MinimumScore is < 0.1 or > 0.9)
        {
            errors.Add("Retrieval settings exceed supported candidate, character, or score bounds.");
        }
        else if (profile.Retrieval.SourceCategories is { } categories &&
                 (categories.Count is < 1 or > 2 || categories.Any(category => !AllowedSourceCategories.Contains(category)) ||
                  categories.Distinct(StringComparer.Ordinal).Count() != categories.Count))
        {
            errors.Add("Retrieval.SourceCategories may contain only unique mechanics and lore categories.");
        }

        if (profile.RouteAliases is null || profile.RouteAliases.Count > 64)
        {
            errors.Add("RouteAliases may contain at most 64 literal route rules.");
        }
        else
        {
            var uniqueAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var alias in profile.RouteAliases)
            {
                if (alias is null || !IsLiteralPhrase(alias.Phrase) ||
                    !Enum.IsDefined(alias.Intent) || alias.Intent == SparkyIntent.Unsupported ||
                    alias.Priority is < -100 or > 100 ||
                    alias.LanguageId is { } languageId && !supportedLanguageIds.Contains(languageId) ||
                    alias.ExcludedPhrases is { Count: > 8 } ||
                    alias.ExcludedPhrases?.Any(excluded => !IsLiteralPhrase(excluded)) == true)
                {
                    errors.Add("RouteAliases contain an invalid phrase, intent, priority, language, or exclusion.");
                    break;
                }

                string key = $"{alias.LanguageId?.ToString(CultureInfo.InvariantCulture) ?? "*"}:{NormalizePhrase(alias.Phrase)}";
                if (!uniqueAliases.Add(key))
                {
                    errors.Add("RouteAliases cannot repeat a phrase for the same language.");
                    break;
                }
            }
        }

        return errors;
    }

    internal static bool IsLiteralPhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > 80 || phrase.Any(char.IsControl))
        {
            return false;
        }

        int words = 0;
        bool inWord = false;
        foreach (char character in phrase.Normalize())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord)
                {
                    words++;
                    inWord = true;
                }
                continue;
            }

            if (char.IsWhiteSpace(character) || character is '\'' or '’' or '-' or '_')
            {
                inWord = false;
                continue;
            }

            return false;
        }

        return words is >= 1 and <= 8;
    }

    internal static string NormalizePhrase(string phrase) =>
        string.Join(' ', phrase.Normalize().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}
