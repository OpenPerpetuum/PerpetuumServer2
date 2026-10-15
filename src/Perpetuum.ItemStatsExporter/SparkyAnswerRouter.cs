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
    string? FinishStatus = null,
    SparkyConversationUpdate? ContextUpdate = null,
    string? ParsedIntent = null,
    string? ParsedFocus = null,
    string? EffectiveIntent = null,
    string? EffectiveFocus = null,
    string? ResolverCategory = null,
    string? ResolverOutcome = null,
    int? ComparisonSubjectCount = null,
    string? ComparisonAttributeCategory = null,
    string? ComparisonResultCategory = null,
    SparkyComparisonDiagnostics? ComparisonDiagnostics = null);

internal sealed record SparkyEntityCoverage(
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing);

internal sealed class SparkyAnswerRouter
{
    private readonly SparkyServiceOptions options;
    private readonly SparkyRetrievalSettings retrieval;
    private readonly string? personaStyle;
    private readonly HttpClient? modelClient;

    public SparkyAnswerRouter(
        SparkyServiceOptions options,
        SparkyRetrievalSettings? retrieval = null,
        string? personaStyle = null,
        HttpClient? modelClient = null)
    {
        this.options = options;
        this.retrieval = retrieval ?? new SparkyRetrievalSettings(5, 4200, 0.25);
        this.personaStyle = personaStyle;
        this.modelClient = modelClient;
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
        CancellationToken cancellationToken,
        IReadOnlyList<SparkyConversationTurn>? contextTurns = null,
        string? contextSummary = null,
        SparkyPendingClarification? pendingClarification = null,
        SparkyCallerRank callerRank = SparkyCallerRank.Agent,
        SparkyLiveKnowledgeReadView? liveKnowledge = null)
    {
        SparkyAnswerResult result = await AnswerCoreAsync(
            question, interpretation, bundle, contextItem, contextLanguageId, cancellationToken,
            contextTurns, contextSummary, pendingClarification, callerRank, liveKnowledge).ConfigureAwait(false);
        SparkyQuestionInterpretation effective = GetEffectiveInterpretation(
            question, interpretation, bundle, contextTurns, pendingClarification);
        return result with
        {
            ParsedIntent = interpretation.Intent.ToString(),
            ParsedFocus = interpretation.RobotFocus?.ToString(),
            EffectiveIntent = result.EffectiveIntent ?? effective.Intent.ToString(),
            EffectiveFocus = result.EffectiveFocus ?? effective.RobotFocus?.ToString(),
            ResolverCategory = result.ResolverCategory ?? effective.Intent.ToString(),
            ResolverOutcome = result.ResolverOutcome ?? result.Status
        };
    }

    private async Task<SparkyAnswerResult> AnswerCoreAsync(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyKnowledgeBundle bundle,
        string? contextItem,
        int? contextLanguageId,
        CancellationToken cancellationToken,
        IReadOnlyList<SparkyConversationTurn>? contextTurns = null,
        string? contextSummary = null,
        SparkyPendingClarification? pendingClarification = null,
        SparkyCallerRank callerRank = SparkyCallerRank.Agent,
        SparkyLiveKnowledgeReadView? liveKnowledge = null)
    {
        Task<SparkyAnswerResult> ClarifyAsync(
            string clarifyQuestion,
            int clarifyLanguageId,
            string clarifyLanguageName,
            SparkyClarificationEvidence clarifyEvidence,
            string clarifyStatus,
            SparkyResponseTemplateKey fallbackKey,
            string? clarifyItem,
            CancellationToken token) => ClarifyCoreAsync(
                clarifyQuestion,
                clarifyLanguageId,
                clarifyLanguageName,
                clarifyEvidence,
                clarifyStatus,
                fallbackKey,
                clarifyItem,
                token,
                contextTurns,
                contextSummary,
                pendingClarification,
                callerRank);

        if (pendingClarification is not null && interpretation.FollowUpAction == SparkyFollowUpAction.Clarify)
        {
            int clarificationLanguageId = bundle.Languages.Any(language => language.LanguageId == interpretation.LanguageId)
                ? interpretation.LanguageId
                : bundle.Languages.FirstOrDefault(language => language.LanguageId == 0)?.LanguageId ?? 0;
            var clarificationLanguage = bundle.Languages.FirstOrDefault(language => language.LanguageId == clarificationLanguageId)
                ?? bundle.Languages[0];
            return await ClarifyAsync(
                question,
                clarificationLanguageId,
                clarificationLanguage.DisplayName,
                new SparkyClarificationEvidence(
                    "The player has not selected one of the exact choices already offered.",
                    null,
                    pendingClarification.CandidateNames,
                    []),
                "ambiguous",
                SparkyResponseTemplateKey.AmbiguousItem,
                contextItem,
                cancellationToken).ConfigureAwait(false);
        }

        if (pendingClarification is not null && interpretation.SelectedCandidate is { } selectedCandidate)
        {
            string requestedType = pendingClarification.RequestedAnswerType ?? interpretation.RequestedAnswerType ?? "General";
            if (requestedType != "Compatibility")
            {
                contextItem = selectedCandidate;
            }
            interpretation = interpretation with
            {
                EnglishSearchQuery = $"{pendingClarification.OriginalQuestion} {selectedCandidate} {requestedType}",
                MentionedEntities = (interpretation.MentionedEntities ?? [])
                    .Append(selectedCandidate)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToArray(),
                Scope = SparkyConversationScope.Game
            };
            if (requestedType is "Stats" or "Recipe" or "FullRecipe" or "Ammo")
            {
                interpretation = interpretation with { Intent = SparkyIntent.StructuredItem };
            }
        }

        int languageId = interpretation.LanguageId;
        var language = bundle.Languages.FirstOrDefault(candidate => candidate.LanguageId == languageId)
            ?? bundle.Languages.First(candidate => candidate.LanguageId == 0);
        languageId = language.LanguageId;

        Task<SparkyLocalizedAnswer> ComposeAsync(
            object? evidence,
            SparkyAnswerOutcome outcome = SparkyAnswerOutcome.FactualAnswer,
            IReadOnlyList<string>? requiredExactStrings = null,
            string? fallback = null) => ComposeWithFallbackAsync(
                question,
                languageId,
                language.DisplayName,
                evidence,
                outcome,
                contextTurns,
                contextSummary,
                pendingClarification,
                callerRank,
                requiredExactStrings,
                fallback ?? SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.UnsupportedEvidence),
                cancellationToken);

        if (interpretation.Scope is SparkyConversationScope.OutOfScope or SparkyConversationScope.Ambiguous)
        {
            bool outOfScope = interpretation.Scope == SparkyConversationScope.OutOfScope;
            var response = await ComposeAsync(
                new
                {
                    reason = outOfScope
                        ? "The request is outside Perpetuum game assistance. Do not answer it using general knowledge."
                        : "The request is ambiguous and cannot be safely resolved from the current turn or its dialogue context."
                },
                outOfScope ? SparkyAnswerOutcome.Refusal : SparkyAnswerOutcome.Clarification,
                fallback: SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.UnsupportedEvidence)).ConfigureAwait(false);
            return new SparkyAnswerResult("answered", response.Answer, null, response.LanguageId, FinishStatus: response.FinishStatus);
        }

        if (interpretation.Comparison is { IsComparison: true } comparisonIntent)
        {
            SparkyComparisonSubjectResolution subjectResolution = SparkyComparisonSubjectResolver.Resolve(
                comparisonIntent,
                question,
                interpretation.EnglishSearchQuery,
                contextTurns ?? [],
                bundle,
                languageId);
            string? comparisonDomain = subjectResolution.Domain ??
                (subjectResolution.Subjects.Select(subject => subject.Domain).Distinct(StringComparer.Ordinal).Count() == 1
                    ? subjectResolution.Subjects.FirstOrDefault()?.Domain
                    : null);
            var comparisonDiagnosticsSubjects = subjectResolution.Subjects
                .Select(subject => new SparkyComparisonDiagnosticSubject(subject.DisplayName, subject.ReferenceSource))
                .ToArray();

            if (subjectResolution.Status != "resolved" || subjectResolution.Domain is null || subjectResolution.Subjects.Count != 2)
            {
                string reason = subjectResolution.Status switch
                {
                    "ambiguous" => "More than two plausible recent entities could be compared.",
                    "cross_domain" => "The requested subjects belong to different structured game domains.",
                    "insufficient_subjects" => "Only one supported comparison subject could be resolved.",
                    _ => "No supported comparison subjects could be resolved from the current turn or dialogue context."
                };
                SparkyAnswerResult clarification = await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence(reason, comparisonIntent.MetricPhrase,
                        subjectResolution.CandidateNames, []),
                    subjectResolution.Status,
                    subjectResolution.CandidateNames.Count > 0
                        ? SparkyResponseTemplateKey.AmbiguousItem
                        : SparkyResponseTemplateKey.MissingTerm,
                    null,
                    cancellationToken).ConfigureAwait(false);
                return clarification with
                {
                    EffectiveIntent = comparisonDomain == "Robot" ? SparkyIntent.StructuredRobot.ToString() : null,
                    EffectiveFocus = comparisonDomain == "Robot" ? SparkyRobotQuestionFocus.CompareStats.ToString() : null,
                    ResolverCategory = "comparison_subjects",
                    ResolverOutcome = subjectResolution.Status,
                    ComparisonSubjectCount = subjectResolution.Subjects.Count,
                    ComparisonAttributeCategory = CategorizeComparisonMetric(comparisonIntent.MetricPhrase),
                    ComparisonResultCategory = subjectResolution.Status,
                    ComparisonDiagnostics = new SparkyComparisonDiagnostics(
                        comparisonDomain, comparisonDiagnosticsSubjects, comparisonIntent.MetricPhrase,
                        subjectResolution.Status, [])
                };
            }

            var comparisonRequest = new SparkyComparisonRequest(
                subjectResolution.Domain,
                subjectResolution.Subjects[0].DefinitionName,
                subjectResolution.Subjects[1].DefinitionName,
                comparisonIntent.MetricPhrase,
                comparisonIntent.Direction,
                comparisonIntent.CompareAllSharedFields);
            SparkyComparisonResolution comparisonResolution = SparkyComparisonCoordinator.Resolve(
                comparisonRequest,
                bundle,
                languageId,
                [new SparkyItemComparisonResolver(), new SparkyRobotComparisonResolver()]);
            SparkyComparisonDiagnostics diagnostics = new(
                subjectResolution.Domain,
                comparisonDiagnosticsSubjects,
                comparisonIntent.MetricPhrase,
                comparisonResolution.DiagnosticCategory,
                comparisonResolution.Facts);

            if (comparisonResolution.Status != "resolved")
            {
                string reason = comparisonResolution.Status switch
                {
                    "needs_criterion" => "The player asks which subject is better but has not named a measurable criterion.",
                    "ambiguous_attribute" => "The requested attribute matches more than one structured field.",
                    _ => "The requested attribute or one of its values is unavailable in local records."
                };
                string fallback = comparisonResolution.Status == "needs_criterion"
                    ? SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.UnsupportedEvidence)
                    : SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.MissingTerm);
                SparkyLocalizedAnswer clarification = await ComposeAsync(
                    new { reason, subjects = comparisonResolution.LocalizedSubjectNames, requestedMetric = comparisonIntent.MetricPhrase },
                    SparkyAnswerOutcome.Clarification,
                    requiredExactStrings: comparisonResolution.LocalizedSubjectNames,
                    fallback: fallback).ConfigureAwait(false);
                return new SparkyAnswerResult(
                    comparisonResolution.Status,
                    clarification.Answer,
                    null,
                    clarification.LanguageId,
                    FinishStatus: clarification.FinishStatus,
                    EffectiveIntent: subjectResolution.Domain == "Robot" ? SparkyIntent.StructuredRobot.ToString() : SparkyIntent.StructuredItem.ToString(),
                    EffectiveFocus: subjectResolution.Domain == "Robot" ? SparkyRobotQuestionFocus.CompareStats.ToString() : null,
                    ResolverCategory: "comparison_facts",
                    ResolverOutcome: comparisonResolution.Status,
                    ComparisonSubjectCount: 2,
                    ComparisonAttributeCategory: CategorizeComparisonMetric(comparisonIntent.MetricPhrase),
                    ComparisonResultCategory: comparisonResolution.DiagnosticCategory,
                    ComparisonDiagnostics: diagnostics);
            }

            IReadOnlyList<string> requiredComparisonStrings = GetRequiredComparisonStrings(comparisonResolution);
            string comparisonFallback = FormatComparisonFallback(comparisonResolution);
            SparkyLocalizedAnswer comparisonAnswer = await ComposeAsync(
                new
                {
                    comparisonType = comparisonIntent.CompareAllSharedFields ? "all shared numeric fields" : "one structured field",
                    domain = subjectResolution.Domain,
                    subjects = comparisonResolution.LocalizedSubjectNames,
                    requestedAttribute = comparisonIntent.MetricPhrase,
                    comparisons = comparisonResolution.Facts,
                    calculatedDifference = comparisonResolution.Difference,
                    leadingSubject = comparisonResolution.LeadingSubjectName
                },
                requiredExactStrings: requiredComparisonStrings,
                fallback: comparisonFallback).ConfigureAwait(false);
            return new SparkyAnswerResult(
                "answered",
                comparisonAnswer.Answer,
                null,
                comparisonAnswer.LanguageId,
                EnglishSearchQuery: interpretation.EnglishSearchQuery,
                SelectedSourceIds: [],
                FinishStatus: comparisonAnswer.FinishStatus,
                EffectiveIntent: subjectResolution.Domain == "Robot" ? SparkyIntent.StructuredRobot.ToString() : SparkyIntent.StructuredItem.ToString(),
                EffectiveFocus: subjectResolution.Domain == "Robot" ? SparkyRobotQuestionFocus.CompareStats.ToString() : null,
                ResolverCategory: "comparison_facts",
                ResolverOutcome: "resolved",
                ComparisonSubjectCount: 2,
                ComparisonAttributeCategory: CategorizeComparisonMetric(comparisonIntent.MetricPhrase),
                ComparisonResultCategory: comparisonResolution.DiagnosticCategory,
                ComparisonDiagnostics: diagnostics);
        }

        interpretation = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(question, interpretation, bundle);

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
            if (catalogAnswer is null)
            {
                return await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("The local dictionary has no translated laser-module names in this language; ask the player to provide English module names.", null, [], []),
                    "not_found",
                    SparkyResponseTemplateKey.MissingTerm,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            var laserNames = bundle.RobotModules
                .Where(module => module.IsLaser == true && module.LocalizedViews.TryGetValue(languageId, out var view) &&
                                 !string.IsNullOrWhiteSpace(view.DisplayName))
                .Select(module => module.LocalizedViews[languageId].DisplayName!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var catalogGeneration = await ComposeAsync(
                new { catalogKind = "translated laser modules", moduleNames = laserNames },
                requiredExactStrings: laserNames,
                fallback: catalogAnswer).ConfigureAwait(false);
            return new SparkyAnswerResult("answered", catalogGeneration.Answer, null, catalogGeneration.LanguageId,
                FinishStatus: catalogGeneration.FinishStatus);
        }

        if (interpretation.Intent == SparkyIntent.SmallTalk && !hasMentionedEntities)
        {
            bool isPlainGreeting = PlainGreetingPattern.IsMatch(question);
            if (PersonalCheckInPattern.IsMatch(question) ||
                interpretation.SmallTalkKind == SparkySmallTalkKind.Banter && !isPlainGreeting)
            {
                var banter = await ComposeAsync(
                    new { kind = "small talk", category = interpretation.SmallTalkKind?.ToString() },
                    SparkyAnswerOutcome.SmallTalk,
                    fallback: SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.Greeting)).ConfigureAwait(false);
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
            var smallTalk = await ComposeAsync(
                new { kind = "small talk", category = templateKey.ToString() },
                SparkyAnswerOutcome.SmallTalk,
                fallback: SparkyResponseTemplates.Get(languageId, templateKey)).ConfigureAwait(false);
            return new SparkyAnswerResult("answered", smallTalk.Answer, null, smallTalk.LanguageId, FinishStatus: smallTalk.FinishStatus);
        }

        if (interpretation.Intent == SparkyIntent.StructuredRobot)
        {
            if (interpretation.RobotFocus is not { } robotFocus)
            {
                return await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("The robot question could not be assigned a supported focus.", null, [], []),
                    "answered",
                    SparkyResponseTemplateKey.UnsupportedEvidence,
                    null,
                    cancellationToken).ConfigureAwait(false);
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
                return await ClarifyAsync(
                    question,
                    languageId,
                    language.DisplayName,
                    new SparkyClarificationEvidence("The requested module name did not exactly match a translated module; these are nearby catalog candidates.", null, robotLookup.Candidates, []),
                    "not_found",
                    SparkyResponseTemplateKey.MissingTerm,
                    null,
                    cancellationToken).ConfigureAwait(false);
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

            IReadOnlyList<string> robotRequirements = GetRequiredRobotStrings(robotLookup.Facts);
            var robotAnswer = await ComposeAsync(
                robotLookup.Facts,
                requiredExactStrings: robotRequirements,
                fallback: SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.UnsupportedEvidence)).ConfigureAwait(false);
            return new SparkyAnswerResult(
                "answered",
                robotAnswer.Answer,
                robotLookup.Facts.RobotName,
                robotAnswer.LanguageId,
                EnglishSearchQuery: interpretation.EnglishSearchQuery,
                SelectedSourceIds: [],
                FinishStatus: robotAnswer.FinishStatus);
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
            string fallbackAnswer = SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.UnsupportedEvidence);
            if (SparkyRecipeAnswerFormatter.TryFormat(
                    question,
                    interpretation.EnglishSearchQuery,
                    item,
                    languageId,
                    out string recipeAnswer))
            {
                fallbackAnswer = recipeAnswer;
            }

            var answer = await ComposeAsync(
                LocalModelAnswerer.ToModelItemFacts(item),
                requiredExactStrings: SparkyRecipeAnswerFormatter.GetRequiredExactStrings(
                    question,
                    interpretation.EnglishSearchQuery,
                    item),
                fallback: fallbackAnswer).ConfigureAwait(false);
            return new SparkyAnswerResult("answered", answer.Answer, item.ContextDisplayName, answer.LanguageId, FinishStatus: answer.FinishStatus);
        }

        string searchQuery = interpretation.EnglishSearchQuery ??
                             (hasMentionedEntities ? string.Join(" ", interpretation.MentionedEntities!) : string.Empty);
        if (SparkySeasonStandingResolver.TryResolve(question, searchQuery, liveKnowledge, out var seasonFacts))
        {
            var seasonAnswer = await ComposeAsync(
                seasonFacts,
                requiredExactStrings: SparkySeasonStandingResolver.GetRequiredExactStrings(seasonFacts)).ConfigureAwait(false);
            var seasonResult = new SparkyAnswerResult(
                "answered",
                seasonAnswer.Answer,
                null,
                seasonAnswer.LanguageId,
                FinishStatus: seasonAnswer.FinishStatus);
            IReadOnlyList<string> seasonSourceIds = seasonFacts.SourceId is { Length: > 0 } sourceId
                ? [sourceId]
                : [];
            return WithKnowledgeDiagnostics(seasonResult, searchQuery, seasonSourceIds);
        }

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
            maxCharacters: retrieval.MaxCharacters,
            liveKnowledge: liveKnowledge);
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

        var groundedAnswer = await ComposeAsync(evidence).ConfigureAwait(false);
        return new SparkyAnswerResult(
            "answered",
            groundedAnswer.Answer,
            item?.ContextDisplayName,
            groundedAnswer.LanguageId,
            EnglishSearchQuery: searchQuery,
            SelectedSourceIds: selectedSourceIds,
            FinishStatus: groundedAnswer.FinishStatus);
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

    private async Task<SparkyAnswerResult> ClarifyCoreAsync(
        string question,
        int languageId,
        string languageName,
        SparkyClarificationEvidence evidence,
        string status,
        SparkyResponseTemplateKey fallbackKey,
        string? item,
        CancellationToken cancellationToken,
        IReadOnlyList<SparkyConversationTurn>? contextTurns,
        string? contextSummary,
        SparkyPendingClarification? pendingClarification,
        SparkyCallerRank callerRank)
    {
        string[] candidates = evidence.CandidateNames.Take(SparkyApiRequest.MaximumCandidates).ToArray();
        string fallback = evidence.CandidateNames.Count > 0 && evidence.Reason.Contains("module", StringComparison.OrdinalIgnoreCase)
            ? SparkyRobotAnswerFormatter.FormatUnknownModuleCandidates(languageId, candidates, options.MaxAnswerCharacters)
            : GetClarificationFallback(languageId, fallbackKey, evidence with { CandidateNames = candidates });
        var answer = await ComposeWithFallbackAsync(
            question,
            languageId,
            languageName,
            evidence with { CandidateNames = candidates },
            SparkyAnswerOutcome.Clarification,
            contextTurns,
            contextSummary,
            pendingClarification,
            callerRank,
            candidates,
            fallback,
            cancellationToken).ConfigureAwait(false);
        return new SparkyAnswerResult(
            status,
            answer.Answer,
            item,
            answer.LanguageId,
            Candidates: candidates.Length > 0 ? candidates : null,
            FinishStatus: answer.FinishStatus);
    }

    private static SparkyQuestionInterpretation GetEffectiveInterpretation(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyKnowledgeBundle bundle,
        IReadOnlyList<SparkyConversationTurn>? contextTurns,
        SparkyPendingClarification? pendingClarification)
    {
        SparkyQuestionInterpretation prepared = interpretation;
        if (pendingClarification is not null && prepared.SelectedCandidate is { } selectedCandidate)
        {
            string requestedType = pendingClarification.RequestedAnswerType ?? prepared.RequestedAnswerType ?? "General";
            prepared = prepared with
            {
                EnglishSearchQuery = $"{pendingClarification.OriginalQuestion} {selectedCandidate} {requestedType}",
                MentionedEntities = (prepared.MentionedEntities ?? [])
                    .Append(selectedCandidate)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToArray(),
                Scope = SparkyConversationScope.Game,
                Intent = requestedType is "Stats" or "Recipe" or "FullRecipe" or "Ammo"
                    ? SparkyIntent.StructuredItem
                    : prepared.Intent
            };
        }

        if (prepared.Comparison is { IsComparison: true } comparison)
        {
            SparkyComparisonSubjectResolution resolution = SparkyComparisonSubjectResolver.Resolve(
                comparison, question, prepared.EnglishSearchQuery, contextTurns ?? [], bundle, prepared.LanguageId);
            string? domain = resolution.Domain ??
                (resolution.Subjects.Select(subject => subject.Domain).Distinct(StringComparer.Ordinal).Count() == 1
                    ? resolution.Subjects.FirstOrDefault()?.Domain
                    : null);
            return domain switch
            {
                "Robot" => prepared with { Intent = SparkyIntent.StructuredRobot, RobotFocus = SparkyRobotQuestionFocus.CompareStats },
                "Item" => prepared with { Intent = SparkyIntent.StructuredItem, RobotFocus = null },
                _ when prepared.Intent == SparkyIntent.StructuredRobot => prepared with { RobotFocus = SparkyRobotQuestionFocus.CompareStats },
                _ => prepared
            };
        }

        return SparkyRobotIntentResolver.CorrectStructuredItemQuestion(question, prepared, bundle);
    }

    private static IReadOnlyList<string> GetRequiredComparisonStrings(SparkyComparisonResolution resolution)
    {
        var required = new List<string>();
        foreach (SparkyComparisonFact fact in resolution.Facts)
        {
            Add(fact.AttributeLabel);
            foreach (string value in fact.Values)
            {
                Add(string.IsNullOrWhiteSpace(fact.Unit) ? value : $"{value} {fact.Unit}");
            }

            Add(string.IsNullOrWhiteSpace(fact.Unit)
                ? fact.Difference
                : $"{fact.Difference} {fact.Unit}");
            Add(fact.LeadingSubjectName);
        }

        foreach (string subject in resolution.LocalizedSubjectNames)
            Add(subject);
        return required.Take(72).ToArray();

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !required.Contains(value, StringComparer.Ordinal))
                required.Add(value);
        }
    }

    private static string FormatComparisonFallback(SparkyComparisonResolution resolution)
    {
        if (resolution.Facts.Count == 1)
        {
            SparkyComparisonFact fact = resolution.Facts[0];
            string first = FormatComparisonValue(fact.Values[0], fact.Unit);
            string second = FormatComparisonValue(fact.Values[1], fact.Unit);
            string difference = FormatComparisonValue(fact.Difference, fact.Unit);
            return $"Records show {resolution.LocalizedSubjectNames[0]} at {first} and {resolution.LocalizedSubjectNames[1]} at {second} for {fact.AttributeLabel}; the difference is {difference}.";
        }

        return string.Join("\n", resolution.Facts.Take(12).Select(fact =>
            $"{fact.AttributeLabel}: {resolution.LocalizedSubjectNames[0]} {FormatComparisonValue(fact.Values[0], fact.Unit)}; " +
            $"{resolution.LocalizedSubjectNames[1]} {FormatComparisonValue(fact.Values[1], fact.Unit)} (difference {FormatComparisonValue(fact.Difference, fact.Unit)})"));
    }

    private static string FormatComparisonValue(string value, string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? value : $"{value} {unit}";

    private static string CategorizeComparisonMetric(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return "all_shared_fields_or_unspecified";

        string normalized = string.Join(' ', Regex.Matches(phrase.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value));
        if (normalized.Contains("speed", StringComparison.Ordinal) ||
            normalized is "fast" or "faster" or "speedier")
        {
            return "speed";
        }

        if (normalized.Contains("cpu", StringComparison.Ordinal)) return "cpu_usage";
        if (normalized.Contains("armor", StringComparison.Ordinal)) return "armor";
        if (normalized.Contains("range", StringComparison.Ordinal)) return "range";
        return "named_attribute";
    }

    private async Task<SparkyLocalizedAnswer> ComposeWithFallbackAsync(
        string question,
        int languageId,
        string languageName,
        object? evidence,
        SparkyAnswerOutcome outcome,
        IReadOnlyList<SparkyConversationTurn>? contextTurns,
        string? contextSummary,
        SparkyPendingClarification? pendingClarification,
        SparkyCallerRank callerRank,
        IReadOnlyList<string>? requiredExactStrings,
        string fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LocalModelAnswerer.AnswerGroundedAsync(
                options.ModelBaseUrl,
                options.ModelId,
                question,
                languageId,
                languageName,
                evidence ?? new { },
                options.MaxAnswerCharacters,
                options.MaxCompletionTokens,
                options.InferenceTimeout,
                apiToken: options.ModelApiToken,
                cancellationToken: cancellationToken,
                personaStyle: personaStyle,
                outcome: outcome,
                contextTurns: contextTurns,
                contextSummary: contextSummary,
                pendingClarification: pendingClarification,
                callerRank: callerRank,
                requiredExactStrings: requiredExactStrings,
                modelClient: modelClient,
                answerTemperature: options.AnswerTemperature,
                answerTopP: options.AnswerTopP).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SparkyLocalizedAnswer(languageId, fallback, "fallback");
        }
        catch (HttpRequestException)
        {
            return new SparkyLocalizedAnswer(languageId, fallback, "fallback");
        }
        catch (InvalidDataException)
        {
            return new SparkyLocalizedAnswer(languageId, fallback, "fallback");
        }
        catch (JsonException)
        {
            return new SparkyLocalizedAnswer(languageId, fallback, "fallback");
        }
    }

    private static string GetClarificationFallback(
        int languageId,
        SparkyResponseTemplateKey fallbackKey,
        SparkyClarificationEvidence evidence) =>
        fallbackKey == SparkyResponseTemplateKey.AmbiguousItem
            ? SparkyResponseTemplates.Format(languageId, fallbackKey, string.Join(", ", evidence.CandidateNames.Take(SparkyApiRequest.MaximumCandidates)))
            : SparkyResponseTemplates.Get(languageId, fallbackKey);

    private static IReadOnlyList<string> GetRequiredRobotStrings(SparkyRobotAnswerFacts facts)
    {
        var required = new List<string> { facts.RobotName };
        static void Add(List<string> values, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.Ordinal))
            {
                values.Add(value);
            }
        }

        static void AddStat(List<string> values, string label, string value, string? unit)
        {
            Add(values, $"{label}: {value} {unit}".Trim());
        }

        switch (facts.Focus)
        {
            case SparkyRobotQuestionFocus.Stats:
                foreach (var stat in facts.AssembledStats)
                {
                    AddStat(required, stat.Label, stat.Value, stat.Unit);
                }
                break;
            case SparkyRobotQuestionFocus.Components:
                foreach (var component in facts.Components)
                {
                    Add(required, component.DisplayName);
                    Add(required, component.SlotCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                break;
            case SparkyRobotQuestionFocus.ChassisBonus:
                foreach (var bonus in facts.ChassisBonuses)
                {
                    Add(required, bonus.ExtensionName);
                    Add(required, bonus.PropertyName);
                    Add(required, bonus.Value);
                    Add(required, bonus.Unit);
                }
                break;
            case SparkyRobotQuestionFocus.ModuleCompatibility when facts.Module is { } module:
                Add(required, module.DisplayName);
                foreach (int slot in facts.Components.SelectMany(component => component.EligibleSlots).Distinct())
                {
                    Add(required, slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                Add(required, module.CompatibilityScope);
                break;
            case SparkyRobotQuestionFocus.Fastest:
                foreach (var speed in facts.SpeedRanking ?? [])
                {
                    Add(required, speed.RobotName);
                    Add(required, speed.Speed);
                    Add(required, speed.Unit);
                }
                break;
            case SparkyRobotQuestionFocus.CompareStats:
                foreach (var comparison in facts.Comparisons ?? [])
                {
                    Add(required, comparison.FirstRobotName);
                    Add(required, comparison.SecondRobotName);
                    foreach (var stat in comparison.Stats)
                    {
                        AddStat(required, stat.Label, stat.FirstValue, stat.Unit);
                        Add(required, stat.SecondValue);
                        Add(required, stat.Difference);
                    }
                }
                break;
        }

        return required;
    }
}
