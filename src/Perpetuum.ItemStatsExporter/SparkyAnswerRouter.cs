using System.Text.RegularExpressions;
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyAnswerResult(
    string Status,
    string? Answer,
    string? Item,
    int? LanguageId,
    IReadOnlyList<string>? Candidates = null);

internal sealed class SparkyAnswerRouter(SparkyServiceOptions options)
{
    private static readonly Regex NumberPattern = new(@"\b\d+(?:\.\d+)?\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<SparkyAnswerResult> AnswerAsync(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyKnowledgeBundle bundle,
        string? contextItem,
        int? contextLanguageId,
        CancellationToken cancellationToken)
    {
        int languageId = interpretation.LanguageId;
        var language = bundle.Languages.FirstOrDefault(candidate => candidate.LanguageId == languageId)
            ?? bundle.Languages.First(candidate => candidate.LanguageId == 0);
        languageId = language.LanguageId;

        if (interpretation.Intent == SparkyIntent.Unsupported)
        {
            return Template("answered", languageId, SparkyResponseTemplateKey.UnsupportedEvidence);
        }

        if (interpretation.Intent == SparkyIntent.SmallTalk)
        {
            var templateKey = interpretation.SmallTalkKind switch
            {
                SparkySmallTalkKind.Greeting => SparkyResponseTemplateKey.Greeting,
                SparkySmallTalkKind.Acknowledgement => SparkyResponseTemplateKey.Acknowledgement,
                SparkySmallTalkKind.Thanks => SparkyResponseTemplateKey.Thanks,
                SparkySmallTalkKind.Banter => SparkyResponseTemplateKey.Banter,
                _ => SparkyResponseTemplateKey.Greeting
            };
            return Template("answered", languageId, templateKey);
        }

        ItemStatsLookupAnswer? item = null;
        if (interpretation.Intent is SparkyIntent.StructuredItem or SparkyIntent.Combined)
        {
            var lookup = ItemStatsLookup.Resolve(
                question,
                bundle,
                languageId,
                contextItem,
                contextLanguageId,
                interpretation.EnglishSearchQuery);
            if (lookup.Status == "ambiguous")
            {
                string[] candidates = lookup.Candidates
                    .Select(candidate => candidate.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToArray();
                string answer = SparkyResponseTemplates.Format(
                    languageId,
                    SparkyResponseTemplateKey.AmbiguousItem,
                    string.Join(", ", candidates));
                return new SparkyAnswerResult("ambiguous", answer, null, languageId, candidates);
            }

            if (lookup.Status != "found" || lookup.Item is null)
            {
                return Template("not_found", languageId, SparkyResponseTemplateKey.MissingTerm);
            }

            item = lookup.Item;
        }

        if (interpretation.Intent == SparkyIntent.StructuredItem && item is not null)
        {
            var answer = await LocalModelAnswerer.AnswerLocalizedAsync(
                options.ModelBaseUrl,
                options.ModelId,
                question,
                item,
                languageId,
                language.DisplayName,
                options.MaxAnswerCharacters,
                options.MaxCompletionTokens,
                options.InferenceTimeout,
                apiToken: options.ModelApiToken,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new SparkyAnswerResult("answered", answer.Answer, item.ContextDisplayName, answer.LanguageId);
        }

        string searchQuery = interpretation.EnglishSearchQuery ?? string.Empty;
        var passages = new SparkyKnowledgeRetriever(bundle).Search(searchQuery);
        if (passages.Count == 0 || HasConflictingNumericClaims(passages))
        {
            return Template("answered", languageId, SparkyResponseTemplateKey.UnsupportedEvidence, item?.ContextDisplayName);
        }

        object evidence = item is null
            ? new
            {
                passages = passages.Select(ToModelPassage).ToArray()
            }
            : new
            {
                itemFacts = new
                {
                    item.DisplayName,
                    item.Stats,
                    item.AmmoVariants,
                    item.Recipe,
                    item.RecipeAvailable,
                    item.RecipeTranslationsComplete,
                    item.FullMaterials,
                    item.FullMaterialsAvailable
                },
                passages = passages.Select(ToModelPassage).ToArray()
            };

        var groundedAnswer = await LocalModelAnswerer.AnswerGroundedAsync(
            options.ModelBaseUrl,
            options.ModelId,
            question,
            languageId,
            language.DisplayName,
            evidence,
            options.MaxAnswerCharacters,
            options.MaxCompletionTokens,
            options.InferenceTimeout,
            apiToken: options.ModelApiToken,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new SparkyAnswerResult("answered", groundedAnswer.Answer, item?.ContextDisplayName, groundedAnswer.LanguageId);
    }

    private static object ToModelPassage(SparkyKnowledgePassage passage) => new
    {
        category = passage.SourceCategory,
        text = passage.Text
    };

    private static bool HasConflictingNumericClaims(IReadOnlyList<SparkyKnowledgePassage> passages)
    {
        for (int first = 0; first < passages.Count; first++)
        {
            var firstNumbers = NumberPattern.Matches(passages[first].Text)
                .Select(match => match.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (firstNumbers.Count == 0)
            {
                continue;
            }

            for (int second = first + 1; second < passages.Count; second++)
            {
                if (string.Equals(passages[first].SourceId, passages[second].SourceId, StringComparison.Ordinal))
                {
                    continue;
                }

                var secondNumbers = NumberPattern.Matches(passages[second].Text)
                    .Select(match => match.Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (secondNumbers.Count > 0 && !firstNumbers.Overlaps(secondNumbers))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static SparkyAnswerResult Template(
        string status,
        int languageId,
        SparkyResponseTemplateKey key,
        string? item = null)
    {
        return new SparkyAnswerResult(status, SparkyResponseTemplates.Get(languageId, key), item, languageId);
    }
}
