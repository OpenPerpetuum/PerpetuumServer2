using System.Text.RegularExpressions;
using System.Text.Json;
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyAnswerResult(
    string Status,
    string? Answer,
    string? Item,
    int? LanguageId,
    IReadOnlyList<string>? Candidates = null,
    string? EnglishSearchQuery = null,
    IReadOnlyList<string>? SelectedSourceIds = null,
    string? FinishStatus = null);

internal sealed record SparkyEntityCoverage(
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing);

internal sealed class SparkyAnswerRouter
{
    private readonly SparkyServiceOptions options;
    private readonly SparkyRetrievalSettings retrieval;
    private readonly string? personaStyle;

    public SparkyAnswerRouter(
        SparkyServiceOptions options,
        SparkyRetrievalSettings? retrieval = null,
        string? personaStyle = null)
    {
        this.options = options;
        this.retrieval = retrieval ?? new SparkyRetrievalSettings(5, 4200, 0.25);
        this.personaStyle = personaStyle;
    }

    private static readonly Regex NumberPattern = new(@"\b\d+(?:\.\d+)?\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NumericQuestionPattern = new(
        @"\b(?:how\s+(?:many|much|old|long|far|often|big|fast)|when\s+(?:did|was|were|is|are)|(?:year|date|time|number|amount|quantity|percentage|percent|age|population|size|value|cost|duration|range|stats?)\b|\d+(?:\.\d+)?\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EvidenceWordPattern = new(@"[\p{L}\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PersonalCheckInPattern = new(
        @"^\s*(?:(?:hey|hi|hello|and|so|then|okay|ok)[\s,]+)*(?:how\s+(?:are|have)\s+you\b|how(?:['’]s| is)\s+it\s+going\b|how\s+are\s+things\b|are\s+you\s+(?:okay|well|doing)\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PlainGreetingPattern = new(
        @"^\s*(?:hey|hi|hello|greetings|good\s+(?:morning|afternoon|evening))(?:\s+there)?\s*[!.?]*\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EntityDefinitionQuestionPattern = new(
        @"^\s*(?:(?:and|but|so|then)\s+)?(?:what|who)\s+(?:is|are|was|were)\b|\b(?:tell me|explain|describe)\s+(?:what|who)\b|\bdefine\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ExplicitItemDataPattern = new(
        @"\b(?:stats?|statistics|accuracy|damage|range|optimal|falloff|cycle\s*time|cpu|power\s*grid|powergrid|core\s+usage|ammo|ammunition|recipe|materials?|components?|production\s+cost|werte|eigenschaften|schaden|reichweite|genauigkeit|rezept|materialien|statistiken|dano|alcance|precis[aã]o|receita|materiais|характеристик\w*|статистик\w*|урон\w*|дальност\w*|точност\w*|рецепт\w*|материал\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<SparkyAnswerResult> AnswerAsync(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyKnowledgeBundle bundle,
        string? contextItem,
        int? contextLanguageId,
        CancellationToken cancellationToken)
    {
        interpretation = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(question, interpretation, bundle);
        int languageId = interpretation.LanguageId;
        var language = bundle.Languages.FirstOrDefault(candidate => candidate.LanguageId == languageId)
            ?? bundle.Languages.First(candidate => candidate.LanguageId == 0);
        languageId = language.LanguageId;

        bool hasMentionedEntities = interpretation.MentionedEntities is { Count: > 0 };
        if (interpretation.Intent == SparkyIntent.Unsupported && !hasMentionedEntities)
        {
            return await ClarifyAsync(
                question,
                languageId,
                language.DisplayName,
                new SparkyClarificationEvidence("The request was not recognized as a supported Perpetuum topic.", null, [], []),
                "answered",
                SparkyResponseTemplateKey.UnsupportedEvidence,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        if (interpretation.Intent == SparkyIntent.StructuredModuleCatalog &&
            interpretation.CatalogFocus == SparkyModuleCatalogFocus.Lasers)
        {
            string? catalogAnswer = SparkyRobotAnswerFormatter.FormatLaserCatalog(
                bundle, languageId, options.MaxAnswerCharacters);
            return catalogAnswer is null
                ? await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("The local dictionary has no translated laser-module names in this language; ask the player to provide English module names.", null, [], []),
                    "not_found",
                    SparkyResponseTemplateKey.MissingTerm,
                    null,
                    cancellationToken).ConfigureAwait(false)
                : new SparkyAnswerResult("answered", catalogAnswer, null, languageId);
        }

        if (interpretation.Intent == SparkyIntent.SmallTalk && !hasMentionedEntities)
        {
            bool isPlainGreeting = PlainGreetingPattern.IsMatch(question);
            if (PersonalCheckInPattern.IsMatch(question) ||
                interpretation.SmallTalkKind == SparkySmallTalkKind.Banter && !isPlainGreeting)
            {
                var banter = await LocalModelAnswerer.AnswerBanterAsync(
                    options.ModelBaseUrl,
                    options.ModelId,
                    question,
                    languageId,
                    language.DisplayName,
                    options.MaxAnswerCharacters,
                    options.MaxCompletionTokens,
                    options.InferenceTimeout,
                    apiToken: options.ModelApiToken,
                    cancellationToken: cancellationToken,
                    personaStyle: personaStyle).ConfigureAwait(false);
                return new SparkyAnswerResult("answered", banter.Answer, null, banter.LanguageId, FinishStatus: banter.FinishStatus);
            }

            var templateKey = isPlainGreeting
                ? SparkyResponseTemplateKey.Greeting
                : interpretation.SmallTalkKind switch
            {
                SparkySmallTalkKind.Greeting => SparkyResponseTemplateKey.Greeting,
                SparkySmallTalkKind.Acknowledgement => SparkyResponseTemplateKey.Acknowledgement,
                SparkySmallTalkKind.Thanks => SparkyResponseTemplateKey.Thanks,
                _ => SparkyResponseTemplateKey.Greeting
            };
            return Template("answered", languageId, templateKey);
        }

        if (interpretation.Intent == SparkyIntent.StructuredRobot)
        {
            if (interpretation.RobotFocus is not { } robotFocus)
            {
                return Template("answered", languageId, SparkyResponseTemplateKey.UnsupportedEvidence);
            }

            SparkyRobotLookupResult robotLookup = SparkyRobotLookup.Resolve(
                question,
                interpretation.EnglishSearchQuery,
                bundle,
                languageId,
                contextItem,
                robotFocus);
            if (robotLookup.Status == "ambiguous")
            {
                return await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("More than one robot name matched the request.", null, robotLookup.Candidates, []),
                    "ambiguous",
                    SparkyResponseTemplateKey.AmbiguousItem,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (robotLookup.Status == "module_not_found")
            {
                string answer = SparkyRobotAnswerFormatter.FormatUnknownModuleCandidates(
                    languageId,
                    robotLookup.Candidates,
                    options.MaxAnswerCharacters);
                return new SparkyAnswerResult("not_found", answer, null, languageId, robotLookup.Candidates);
            }

            if (robotLookup.Status != "found" || robotLookup.Facts is null)
            {
                return await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("The requested robot facts were not present in the local bundle.", null, [], []),
                    "not_found",
                    SparkyResponseTemplateKey.MissingTerm,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            var robotAnswer = await LocalModelAnswerer.AnswerGroundedAsync(
                options.ModelBaseUrl,
                options.ModelId,
                question,
                languageId,
                language.DisplayName,
                robotLookup.Facts,
                options.MaxAnswerCharacters,
                options.MaxCompletionTokens,
                options.InferenceTimeout,
                apiToken: options.ModelApiToken,
                cancellationToken: cancellationToken,
                personaStyle: personaStyle).ConfigureAwait(false);
            string robotText = robotFocus == SparkyRobotQuestionFocus.ModuleCompatibility
                ? SparkyRobotAnswerFormatter.AppendCompatibilityScope(
                    robotAnswer.Answer,
                    robotAnswer.LanguageId,
                    options.MaxAnswerCharacters)
                : robotAnswer.Answer;
            return new SparkyAnswerResult(
                "answered",
                robotText,
                robotLookup.Facts.RobotName,
                robotAnswer.LanguageId,
                EnglishSearchQuery: interpretation.EnglishSearchQuery,
                SelectedSourceIds: []);
        }

        ItemStatsLookupAnswer? item = null;
        bool itemLookupMissed = false;
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
                return await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("More than one item name matched the request.", null, candidates, []),
                    "ambiguous",
                    SparkyResponseTemplateKey.AmbiguousItem,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (lookup.Status != "found" || lookup.Item is null)
            {
                if (lookup.Status != "not_found" || lookup.Item is not null)
                {
                    return await ClarifyAsync(
                        question,
                        languageId,
                        language.DisplayName,
                        new SparkyClarificationEvidence("The item lookup did not return one exact item record.", null, [], []),
                        "not_found",
                        SparkyResponseTemplateKey.MissingTerm,
                        null,
                        cancellationToken).ConfigureAwait(false);
                }

                if (interpretation.Intent == SparkyIntent.StructuredItem &&
                    ExplicitItemDataPattern.IsMatch(string.Join(' ', question, interpretation.EnglishSearchQuery)))
                {
                    var clarification = await ClarifyAsync(
                        question,
                        languageId,
                        language.DisplayName,
                        new SparkyClarificationEvidence(
                            "The exact item was not found in the verified item catalog; no item statistics or recipe can be supplied.",
                            question,
                            [],
                            []),
                        "not_found",
                        SparkyResponseTemplateKey.MissingTerm,
                        null,
                        cancellationToken).ConfigureAwait(false);
                    return WithKnowledgeDiagnostics(
                        clarification,
                        interpretation.EnglishSearchQuery ?? string.Empty,
                        []);
                }

                // The interpreter can mistake a lore name for an item. Try the bounded
                // knowledge index before asking the player to use an English item name.
                itemLookupMissed = true;
            }
            else
            {
                item = lookup.Item;
            }
        }

        if (interpretation.Intent == SparkyIntent.StructuredItem && item is not null)
        {
            if (SparkyRecipeAnswerFormatter.TryFormat(
                    question,
                    interpretation.EnglishSearchQuery,
                    item,
                    languageId,
                    out string recipeAnswer))
            {
                return new SparkyAnswerResult("answered", recipeAnswer, item.ContextDisplayName, languageId);
            }

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
                cancellationToken: cancellationToken,
                personaStyle: personaStyle).ConfigureAwait(false);
            return new SparkyAnswerResult("answered", answer.Answer, item.ContextDisplayName, answer.LanguageId, FinishStatus: answer.FinishStatus);
        }

        string searchQuery = interpretation.EnglishSearchQuery ??
                             (hasMentionedEntities ? string.Join(" ", interpretation.MentionedEntities!) : string.Empty);
        bool prioritizeEntityDefinitions =
            (interpretation.Intent is SparkyIntent.MechanicsLore or SparkyIntent.Combined) &&
            hasMentionedEntities &&
            (EntityDefinitionQuestionPattern.IsMatch(question) || EntityDefinitionQuestionPattern.IsMatch(searchQuery));
        var passages = new SparkyKnowledgeRetriever(bundle).Search(
            question,
            searchQuery,
            mentionedEntities: interpretation.MentionedEntities,
            prioritizeEntityDefinitions: prioritizeEntityDefinitions,
            minimumRelevanceScore: retrieval.MinimumScore,
            sourceCategories: retrieval.SourceCategories,
            maxPassages: retrieval.MaxPassages,
            maxCharacters: retrieval.MaxCharacters);
        string[] selectedSourceIds = passages
            .Select(passage => passage.SourceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (passages.Count == 0)
        {
            var clarification = await ClarifyAsync(
                question,
                languageId,
                language.DisplayName,
                new SparkyClarificationEvidence(
                    "No relevant local knowledge passages were retrieved for the question.",
                    item?.ContextDisplayName,
                    [],
                    interpretation.MentionedEntities ?? []),
                itemLookupMissed ? "not_found" : "answered",
                itemLookupMissed ? SparkyResponseTemplateKey.MissingTerm : SparkyResponseTemplateKey.UnsupportedEvidence,
                item?.ContextDisplayName,
                cancellationToken).ConfigureAwait(false);
            return WithKnowledgeDiagnostics(clarification, searchQuery, selectedSourceIds);
        }

        var entityCoverage = GetEntityCoverage(
            interpretation.MentionedEntities,
            passages,
            item?.ContextDisplayName,
            prioritizeEntityDefinitions);
        if (entityCoverage.Missing.Count > 0 &&
            (prioritizeEntityDefinitions || entityCoverage.Present.Count == 0))
        {
            var clarification = await ClarifyAsync(
                question,
                languageId,
                language.DisplayName,
                new SparkyClarificationEvidence(
                    "The retrieved sources do not verify the named subjects requested.",
                    item?.ContextDisplayName,
                    [],
                    entityCoverage.Missing),
                "answered",
                SparkyResponseTemplateKey.UnsupportedEvidence,
                item?.ContextDisplayName,
                cancellationToken).ConfigureAwait(false);
            return WithKnowledgeDiagnostics(clarification, searchQuery, selectedSourceIds);
        }

        if ((NumericQuestionPattern.IsMatch(question) || NumericQuestionPattern.IsMatch(searchQuery)) &&
            HasConflictingNumericClaims(passages))
        {
            var clarification = await ClarifyAsync(
                question,
                languageId,
                language.DisplayName,
                new SparkyClarificationEvidence(
                    "The retrieved local sources contain conflicting numeric claims for the requested fact.",
                    item?.ContextDisplayName,
                    [],
                    interpretation.MentionedEntities ?? []),
                "answered",
                SparkyResponseTemplateKey.UnsupportedEvidence,
                item?.ContextDisplayName,
                cancellationToken).ConfigureAwait(false);
            return WithKnowledgeDiagnostics(clarification, searchQuery, selectedSourceIds);
        }

        object evidence = item is null
            ? new
            {
                passages = passages.Select(ToModelPassage).ToArray(),
                entityCoverage
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
                passages = passages.Select(ToModelPassage).ToArray(),
                entityCoverage
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
            cancellationToken: cancellationToken,
            personaStyle: personaStyle).ConfigureAwait(false);
        return new SparkyAnswerResult(
            "answered",
            groundedAnswer.Answer,
            item?.ContextDisplayName,
            groundedAnswer.LanguageId,
            EnglishSearchQuery: searchQuery,
            SelectedSourceIds: selectedSourceIds);
    }

    private static SparkyEntityCoverage GetEntityCoverage(
        IReadOnlyList<string>? mentionedEntities,
        IReadOnlyList<SparkyKnowledgePassage> passages,
        string? itemDisplayName,
        bool requireDefinition)
    {
        var present = new List<string>();
        var missing = new List<string>();
        foreach (string entity in mentionedEntities ?? [])
        {
            bool found = requireDefinition
                ? passages.Any(passage => SparkyKnowledgeRetriever.ContainsEntityDefinition(passage.Text, entity))
                : passages.Any(passage => ContainsEntityPhrase(passage.Text, entity)) ||
                  ContainsEntityPhrase(itemDisplayName ?? string.Empty, entity);
            (found ? present : missing).Add(entity);
        }

        return new SparkyEntityCoverage(present, missing);
    }

    private static bool ContainsEntityPhrase(string text, string entity)
    {
        string[] entityTokens = EvidenceWordPattern.Matches(entity)
            .Select(match => match.Value.ToLowerInvariant())
            .ToArray();
        if (entityTokens.Length == 0)
        {
            return false;
        }

        string[] textTokens = EvidenceWordPattern.Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .ToArray();
        for (int start = 0; start <= textTokens.Length - entityTokens.Length; start++)
        {
            bool matches = true;
            for (int offset = 0; offset < entityTokens.Length; offset++)
            {
                if (!string.Equals(textTokens[start + offset], entityTokens[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static SparkyAnswerResult WithKnowledgeDiagnostics(
        SparkyAnswerResult answer,
        string englishSearchQuery,
        IReadOnlyList<string> selectedSourceIds)
    {
        return answer with
        {
            EnglishSearchQuery = englishSearchQuery,
            SelectedSourceIds = selectedSourceIds
        };
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

    private async Task<SparkyAnswerResult> ClarifyAsync(
        string question,
        int languageId,
        string languageName,
        SparkyClarificationEvidence evidence,
        string status,
        SparkyResponseTemplateKey fallbackKey,
        string? item,
        CancellationToken cancellationToken)
    {
        try
        {
            SparkyLocalizedAnswer answer = await LocalModelAnswerer.AnswerGroundedAsync(
                options.ModelBaseUrl,
                options.ModelId,
                question,
                languageId,
                languageName,
                evidence,
                options.MaxAnswerCharacters,
                options.MaxCompletionTokens,
                options.InferenceTimeout,
                apiToken: options.ModelApiToken,
                cancellationToken: cancellationToken,
                personaStyle: personaStyle).ConfigureAwait(false);
            return new SparkyAnswerResult(
                status,
                answer.Answer,
                item,
                answer.LanguageId,
                Candidates: evidence.CandidateNames.Count > 0 ? evidence.CandidateNames : null,
                FinishStatus: answer.FinishStatus);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ClarificationFallback(status, languageId, fallbackKey, evidence, item);
        }
        catch (HttpRequestException)
        {
            return ClarificationFallback(status, languageId, fallbackKey, evidence, item);
        }
        catch (InvalidDataException)
        {
            return ClarificationFallback(status, languageId, fallbackKey, evidence, item);
        }
        catch (JsonException)
        {
            return ClarificationFallback(status, languageId, fallbackKey, evidence, item);
        }
    }

    private static SparkyAnswerResult ClarificationFallback(
        string status,
        int languageId,
        SparkyResponseTemplateKey fallbackKey,
        SparkyClarificationEvidence evidence,
        string? item)
    {
        string fallback = fallbackKey == SparkyResponseTemplateKey.AmbiguousItem
            ? SparkyResponseTemplates.Format(languageId, fallbackKey, string.Join(", ", evidence.CandidateNames))
            : SparkyResponseTemplates.Get(languageId, fallbackKey);
        return new SparkyAnswerResult(
            status,
            fallback,
            item,
            languageId,
            Candidates: evidence.CandidateNames.Count > 0 ? evidence.CandidateNames : null);
    }
}
