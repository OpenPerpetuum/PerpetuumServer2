using System.Text.RegularExpressions;
using System.Globalization;
using Perpetuum.ExportedTypes;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyRobotLookup
{
    private const int MinimumModuleSuggestionTokenOverlap = 2;
    private static readonly Regex WordPattern = new("[\\p{L}\\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FollowUpPattern = new(
        @"\b(?:what|how)\s+about\b|\b(?:it|its|they|them|their|that|this|those|these|one|same)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ModuleQueryStopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "can", "could", "does", "do", "fit", "fits", "for", "how", "i", "if", "in", "into", "is", "it", "its", "may", "module", "modules", "on", "or", "robot", "the", "this", "to", "use", "will", "would",
        "ein", "eine", "einer", "eines", "für", "kann", "können", "modul", "module", "passt", "roboter", "und", "ist", "auf", "der", "die", "das", "zu",
        "de", "do", "da", "em", "na", "no", "para", "pode", "podem", "módulo", "modulo", "robô", "robo", "um", "uma",
        "ли", "может", "модуль", "модули", "робот", "робота", "роботе", "и", "в", "для", "этот", "эта", "это", "на"
    };

    public static SparkyRobotLookupResult Resolve(
        string question,
        string? englishSearchQuery,
        SparkyKnowledgeBundle bundle,
        int languageId,
        string? contextItem,
        SparkyRobotQuestionFocus focus)
    {
        if (!Enum.IsDefined(focus) || !bundle.Languages.Any(language => language.LanguageId == languageId))
        {
            return NotFound();
        }

        if (focus == SparkyRobotQuestionFocus.Fastest)
        {
            return ResolveFastest(bundle, languageId);
        }

        string[] queries = new[] { question, englishSearchQuery }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Normalize(value!))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        SparkyEntityResolutionResult? tieredRobotResolution = null;
        List<RobotMatch> robotMatches;
        if (HasTierAwareCatalog(bundle) && focus != SparkyRobotQuestionFocus.CompareStats)
        {
            tieredRobotResolution = ResolveTierAwareRobots(question, englishSearchQuery, bundle, languageId);
            robotMatches = FindTieredRobotMatches(tieredRobotResolution, queries, bundle, languageId);
        }
        else
        {
            robotMatches = FindRobotMatches(queries, bundle, languageId);
        }

        if (focus == SparkyRobotQuestionFocus.CompareStats)
        {
            return ResolveComparison(robotMatches, languageId, queries.FirstOrDefault() ?? string.Empty);
        }

        SparkyEntityResolutionResult? tieredModuleResolution = null;
        List<ModuleMatch> moduleMatches;
        if (focus != SparkyRobotQuestionFocus.ModuleCompatibility)
        {
            moduleMatches = [];
        }
        else if (HasTierAwareCatalog(bundle))
        {
            tieredModuleResolution = ResolveTierAwareModules(question, englishSearchQuery, bundle, languageId, robotMatches);
            moduleMatches = FindTieredModuleMatches(tieredModuleResolution, queries, bundle, languageId);
        }
        else
        {
            moduleMatches = FindModuleMatches(queries, bundle, languageId);
        }

        if (tieredRobotResolution?.Status is "ambiguous" or "conflicting_tiers")
        {
            IEnumerable<string?> names = tieredRobotResolution.CandidateDefinitionNames.Count > 0
                ? tieredRobotResolution.CandidateDefinitionNames.Select(definition =>
                    bundle.Robots.FirstOrDefault(robot => string.Equals(robot.DefinitionName, definition, StringComparison.OrdinalIgnoreCase)))
                    .Where(robot => robot is not null)
                    .Select(robot => PublicRobotName(robot!, languageId))
                : robotMatches.Select(match => PublicRobotName(match.Record, languageId));
            return AmbiguousTiered(names);
        }

        if (tieredModuleResolution?.Status is "ambiguous" or "conflicting_tiers")
        {
            IEnumerable<string?> names = tieredModuleResolution.CandidateDefinitionNames.Count > 0
                ? tieredModuleResolution.CandidateDefinitionNames.Select(definition =>
                    bundle.RobotModules.FirstOrDefault(module => string.Equals(module.DefinitionName, definition, StringComparison.OrdinalIgnoreCase)))
                    .Where(module => module is not null)
                    .Select(module => PublicModuleName(module!, languageId))
                : [];
            return AmbiguousTiered(names);
        }

        if (robotMatches.Count > 1)
        {
            return Ambiguous(robotMatches.Select(match => PublicRobotName(match.Record, languageId)));
        }

        if (moduleMatches.Count > 1)
        {
            return Ambiguous(moduleMatches.Select(match => PublicModuleName(match.Record, languageId)));
        }

        RobotMatch? selectedRobot = SelectLongest(robotMatches);
        ModuleMatch? selectedModule = SelectLongest(moduleMatches);
        if (selectedRobot is null && IsFollowUp(question, englishSearchQuery) &&
            (tieredRobotResolution is null || !tieredRobotResolution.HasNameQuery) &&
            TryResolveContextRobot(contextItem, bundle, languageId, out SparkyRobotRecord? contextRobot))
        {
            selectedRobot = new RobotMatch(contextRobot!, new HashSet<string>(StringComparer.Ordinal));
        }

        if (selectedRobot is null)
        {
            return NotFound();
        }

        var directDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selectedRobot.Record.DefinitionName };
        if (selectedModule is not null)
        {
            directDefinitions.Add(selectedModule.Record.DefinitionName);
        }

        var matchedAliases = new HashSet<string>(selectedRobot.Aliases, StringComparer.Ordinal);
        if (selectedModule is not null)
        {
            matchedAliases.UnionWith(selectedModule.Aliases);
        }

        SparkyAliasAmbiguity? ambiguity = bundle.SearchIndex.AmbiguousAliases
            .Where(entry => entry.LanguageId == languageId || entry.LanguageId == 0)
            .Where(entry => matchedAliases.Contains(entry.Alias) && entry.Definitions.Any(directDefinitions.Contains))
            .OrderByDescending(entry => WordCount(entry.Alias))
            .ThenBy(entry => entry.Alias, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ambiguity is not null)
        {
            string[] ambiguousCandidates = GetPublicCandidateNames(bundle, ambiguity.Definitions, languageId);
            return ambiguousCandidates.Length > 1
                ? new SparkyRobotLookupResult("ambiguous", null, ambiguousCandidates)
                : NotFound();
        }

        if (focus == SparkyRobotQuestionFocus.ModuleCompatibility && selectedModule is null)
        {
            string[] suggestions = FindModuleSuggestions(queries, selectedRobot, bundle, languageId);
            return suggestions.Length > 0
                ? new SparkyRobotLookupResult("module_not_found", null, suggestions)
                : NotFound();
        }

        if (focus == SparkyRobotQuestionFocus.ModuleCompatibility &&
            string.IsNullOrWhiteSpace(PublicModuleName(selectedModule!.Record, languageId)))
        {
            return NotFound();
        }

        SparkyLocalizedRobotView? localizedRobot = GetRobotView(selectedRobot.Record, languageId);
        string? robotName = localizedRobot?.DisplayName;
        if (localizedRobot is null || string.IsNullOrWhiteSpace(robotName))
        {
            return NotFound();
        }

        SparkyRobotAnswerFacts facts = BuildFacts(
            queries,
            selectedRobot.Record,
            localizedRobot,
            robotName,
            selectedModule?.Record,
            languageId,
            focus);
        return new SparkyRobotLookupResult("found", facts, []);
    }

    public static bool ContainsRobotMention(
        string question,
        string? englishSearchQuery,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        if (!bundle.Languages.Any(language => language.LanguageId == languageId))
        {
            return false;
        }

        return FindMentionedRobots(question, englishSearchQuery, bundle, languageId).Count > 0;
    }

    public static IReadOnlyList<SparkyRobotRecord> FindMentionedRobots(
        string question,
        string? englishSearchQuery,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        string[] queries = new[] { question, englishSearchQuery }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Normalize(value!))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<RobotMatch> nameMatches = FindRobotMatches(queries, bundle, languageId);
        if (!HasTierAwareCatalog(bundle) || nameMatches.Count == 0)
        {
            return nameMatches.Select(match => match.Record).ToArray();
        }

        SparkyEntityResolutionResult resolution = ResolveTierAwareRobots(
            question, englishSearchQuery, bundle, languageId);
        return resolution.Status switch
        {
            "found" when resolution.DefinitionName is not null => bundle.Robots
                .Where(robot => string.Equals(robot.DefinitionName, resolution.DefinitionName, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            "ambiguous" => resolution.CandidateDefinitionNames
                .Select(definition => bundle.Robots.FirstOrDefault(robot =>
                    string.Equals(robot.DefinitionName, definition, StringComparison.OrdinalIgnoreCase)))
                .Where(robot => robot is not null)
                .Cast<SparkyRobotRecord>()
                .ToArray(),
            "conflicting_tiers" => nameMatches.Select(match => match.Record).ToArray(),
            _ => []
        };
    }

    private static SparkyRobotLookupResult ResolveFastest(SparkyKnowledgeBundle bundle, int languageId)
    {
        var ranked = bundle.Robots
            .Select(robot =>
            {
                SparkyLocalizedRobotView? canonicalView = GetRobotView(robot, 0);
                AnswerStat? speed = canonicalView?.AssembledStats.FirstOrDefault(stat => stat.FieldId == (int)AggregateField.speed_max);
                return (Robot: robot, CanonicalView: canonicalView, Speed: speed,
                    NumericSpeed: speed is not null && decimal.TryParse(speed.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
                        ? (decimal?)value
                        : null);
            })
            .Where(entry => entry.NumericSpeed is not null)
            .OrderByDescending(entry => entry.NumericSpeed)
            .ThenBy(entry => entry.CanonicalView?.DisplayName ?? entry.Robot.DefinitionName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ranked.Length == 0)
        {
            return NotFound();
        }

        decimal fastestSpeed = ranked[0].NumericSpeed!.Value;
        var fastest = new List<SparkyRobotSpeedFact>();
        foreach (var entry in ranked.TakeWhile(entry => entry.NumericSpeed == fastestSpeed))
        {
            SparkyLocalizedRobotView? localizedView = GetRobotView(entry.Robot, languageId);
            AnswerStat? localizedSpeed = localizedView?.AssembledStats
                .FirstOrDefault(stat => stat.FieldId == (int)AggregateField.speed_max);
            if (string.IsNullOrWhiteSpace(localizedView?.DisplayName) || localizedSpeed is null)
            {
                return NotFound();
            }

            fastest.Add(new SparkyRobotSpeedFact(
                localizedView.DisplayName,
                entry.Speed!.Value,
                localizedSpeed.Unit));
        }
        var facts = new SparkyRobotAnswerFacts(
            string.Join(" and ", fastest.Select(robot => robot.RobotName)),
            SparkyRobotQuestionFocus.Fastest,
            [], [], [], null,
            SpeedRanking: fastest);
        return new SparkyRobotLookupResult("found", facts, []);
    }

    private static SparkyRobotLookupResult ResolveComparison(
        IReadOnlyList<RobotMatch> robotMatches,
        int languageId,
        string normalizedQuestion)
    {
        if (robotMatches.Count < 2)
        {
            return NotFound();
        }

        if (robotMatches.Count > 2)
        {
            return Ambiguous(robotMatches.Select(match => PublicRobotName(match.Record, languageId)));
        }

        var ordered = robotMatches
            .OrderBy(match => FindFirstMentionIndex(normalizedQuestion, match.Aliases))
            .ThenBy(match => PublicRobotName(match.Record, languageId), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string? firstName = PublicRobotName(ordered[0].Record, languageId);
        string? secondName = PublicRobotName(ordered[1].Record, languageId);
        SparkyLocalizedRobotView? firstView = GetRobotView(ordered[0].Record, languageId);
        SparkyLocalizedRobotView? secondView = GetRobotView(ordered[1].Record, languageId);
        if (firstName is null || secondName is null || firstView is null || secondView is null)
        {
            return NotFound();
        }

        var secondStats = secondView.AssembledStats
            .Where(stat => decimal.TryParse(stat.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .GroupBy(stat => stat.FieldId)
            .ToDictionary(group => group.Key, group => group.First());
        var comparisons = new List<SparkyRobotStatComparison>();
        foreach (AnswerStat firstStat in firstView.AssembledStats.OrderBy(stat => stat.FieldId))
        {
            if (!decimal.TryParse(firstStat.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal firstValue) ||
                !secondStats.TryGetValue(firstStat.FieldId, out AnswerStat? secondStat) ||
                !string.Equals(firstStat.Unit, secondStat.Unit, StringComparison.OrdinalIgnoreCase) ||
                !decimal.TryParse(secondStat.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal secondValue))
            {
                continue;
            }

            comparisons.Add(new SparkyRobotStatComparison(
                firstStat.Label,
                firstStat.Value,
                secondStat.Value,
                (secondValue - firstValue).ToString("G", CultureInfo.InvariantCulture),
                firstStat.Unit));
        }

        if (comparisons.Count == 0)
        {
            return NotFound();
        }

        var facts = new SparkyRobotAnswerFacts(
            $"{firstName} and {secondName}",
            SparkyRobotQuestionFocus.CompareStats,
            [], [], [], null,
            Comparisons: [new SparkyRobotStatsComparison(firstName, secondName, comparisons)]);
        return new SparkyRobotLookupResult("found", facts, []);
    }

    private static int FindFirstMentionIndex(string normalizedQuestion, IEnumerable<string> aliases)
    {
        string paddedQuestion = $" {normalizedQuestion} ";
        return aliases.Select(alias => paddedQuestion.IndexOf($" {alias} ", StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
    }

    private static string[] FindModuleSuggestions(
        IReadOnlyList<string> queries,
        RobotMatch robot,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        var queryTokenSequences = queries
            .Select(query => RemoveRobotAliases(query, robot.Aliases)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !ModuleQueryStopWords.Contains(token))
                .ToArray())
            .Where(tokens => tokens.Length > 0)
            .ToArray();
        if (queryTokenSequences.Length == 0)
        {
            return [];
        }

        var scored = bundle.RobotModules
            .Select(module =>
            {
                string? displayName = PublicModuleName(module, languageId);
                if (displayName is null)
                {
                    return (DisplayName: (string?)null, Score: new ModuleCandidateScore(0, 0, 0));
                }

                var aliases = new HashSet<string>(StringComparer.Ordinal);
                AddAlias(displayName, aliases);
                AddAlias(GetModuleView(module, 0)?.DisplayName, aliases);
                ModuleCandidateScore score = aliases
                    .Select(alias => alias.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .ToArray())
                    .SelectMany(aliasTokens => queryTokenSequences.Select(queryTokens => ScoreModuleCandidate(queryTokens, aliasTokens)))
                    .OrderByDescending(candidate => candidate.Overlap)
                    .ThenByDescending(candidate => candidate.OrderedPrefixLength)
                    .ThenBy(candidate => candidate.ExtraCandidateTokens)
                    .FirstOrDefault();
                return (DisplayName: displayName, Score: score);
            })
            .Where(candidate => candidate.DisplayName is not null)
            .ToArray();

        var eligible = scored
            .Where(candidate => candidate.Score.Overlap >= MinimumModuleSuggestionTokenOverlap)
            .ToArray();
        if (eligible.Length == 0)
        {
            return [];
        }

        ModuleCandidateScore bestScore = eligible
            .OrderByDescending(candidate => candidate.Score.Overlap)
            .ThenByDescending(candidate => candidate.Score.OrderedPrefixLength)
            .ThenBy(candidate => candidate.Score.ExtraCandidateTokens)
            .First()
            .Score;
        return eligible
            .Where(candidate => candidate.Score == bestScore)
            .Select(candidate => candidate.DisplayName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static ModuleCandidateScore ScoreModuleCandidate(IReadOnlyList<string> queryTokens, IReadOnlyList<string> aliasTokens)
    {
        var queryTokenSet = queryTokens.ToHashSet(StringComparer.Ordinal);
        var aliasTokenSet = aliasTokens.ToHashSet(StringComparer.Ordinal);
        int overlap = queryTokenSet.Count(aliasTokenSet.Contains);
        if (overlap < MinimumModuleSuggestionTokenOverlap)
        {
            return new ModuleCandidateScore(0, 0, 0);
        }

        int commonPrefixLength = 0;
        int prefixLimit = Math.Min(queryTokens.Count, aliasTokens.Count);
        while (commonPrefixLength < prefixLimit &&
               string.Equals(queryTokens[commonPrefixLength], aliasTokens[commonPrefixLength], StringComparison.Ordinal))
        {
            commonPrefixLength++;
        }

        int extraCandidateTokens = Math.Max(0, aliasTokenSet.Count - overlap);
        return new ModuleCandidateScore(overlap, commonPrefixLength, extraCandidateTokens);
    }

    private static string RemoveRobotAliases(string query, IReadOnlyCollection<string> aliases)
    {
        string[] tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var removed = new bool[tokens.Length];
        foreach (string[] aliasTokens in aliases
                     .Select(alias => alias.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                     .Where(parts => parts.Length > 0))
        {
            for (int start = 0; start <= tokens.Length - aliasTokens.Length; start++)
            {
                bool matches = true;
                for (int offset = 0; offset < aliasTokens.Length; offset++)
                {
                    if (!string.Equals(tokens[start + offset], aliasTokens[offset], StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    for (int offset = 0; offset < aliasTokens.Length; offset++)
                    {
                        removed[start + offset] = true;
                    }
                }
            }
        }

        return string.Join(' ', tokens.Where((_, index) => !removed[index]));
    }

    private static SparkyRobotAnswerFacts BuildFacts(
        IReadOnlyList<string> queries,
        SparkyRobotRecord robot,
        SparkyLocalizedRobotView view,
        string robotName,
        SparkyRobotModuleRecord? module,
        int languageId,
        SparkyRobotQuestionFocus focus)
    {
        IReadOnlyList<SparkyRobotStatFact> assembledStats = focus is SparkyRobotQuestionFocus.Stats or
            SparkyRobotQuestionFocus.Components or SparkyRobotQuestionFocus.General
            ? ToStatFacts(view.AssembledStats)
            : [];
        IReadOnlyList<SparkyRobotComponentFact> components = [];
        IReadOnlyList<SparkyLocalizedChassisBonus> bonuses = [];
        SparkyRobotModuleFact? moduleFact = null;

        if (focus is SparkyRobotQuestionFocus.Components or SparkyRobotQuestionFocus.General)
        {
            components = view.Components
                .Select(component => new SparkyRobotComponentFact(
                    component.DisplayName,
                    focus == SparkyRobotQuestionFocus.Components ? ToStatFacts(component.Stats) : [],
                    component.SlotCount,
                    []))
                .ToArray();
        }

        if (focus is SparkyRobotQuestionFocus.ChassisBonus or SparkyRobotQuestionFocus.General)
        {
            bonuses = SelectBonuses(queries, view.ChassisBonuses);
        }

        if (focus == SparkyRobotQuestionFocus.ModuleCompatibility && module is not null)
        {
            module.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRobotModuleView? localizedModule);
            if (localizedModule?.DisplayName is null)
            {
                return new SparkyRobotAnswerFacts(robotName, focus, [], [], [], null);
            }

            SparkyRobotCompatibility[] matchingRelations = robot.Compatibility
                .Where(relation => string.Equals(relation.ModuleDefinitionName, module.DefinitionName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            components = matchingRelations
                .Select(relation => view.Components.FirstOrDefault(component =>
                    string.Equals(component.DefinitionName, relation.ComponentDefinitionName, StringComparison.OrdinalIgnoreCase)))
                .Where(component => component is not null)
                .Select(component =>
                {
                    SparkyRobotCompatibility relation = matchingRelations.First(candidate =>
                        string.Equals(candidate.ComponentDefinitionName, component!.DefinitionName, StringComparison.OrdinalIgnoreCase));
                    return new SparkyRobotComponentFact(
                        component!.DisplayName,
                        [],
                        component.SlotCount,
                        relation.EligibleSlots);
                })
                .ToArray();
            moduleFact = new SparkyRobotModuleFact(
                localizedModule.DisplayName,
                ToStatFacts(localizedModule.CpuAndPowerGridStats),
                localizedModule.UniqueCategoryLimit,
                components.Any(component => component.EligibleSlots.Count > 0),
                CurrentLoadoutFitEvaluated: false,
                SparkyRobotAnswerFormatter.GetCompatibilityScope(languageId));
        }

        return new SparkyRobotAnswerFacts(
            robotName,
            focus,
            assembledStats,
            components,
            bonuses,
            moduleFact);
    }

    private static IReadOnlyList<SparkyLocalizedChassisBonus> SelectBonuses(
        IReadOnlyList<string> queries,
        IReadOnlyList<SparkyLocalizedChassisBonus> bonuses)
    {
        if (bonuses.Count < 2)
        {
            return bonuses;
        }

        string[] mentionedBonusAliases = bonuses
            .Where(bonus => queries.Any(query =>
                ContainsPhrase(query, Normalize(bonus.ExtensionName)) ||
                ContainsPhrase(query, Normalize(bonus.PropertyName))))
            .SelectMany(bonus => new[] { Normalize(bonus.ExtensionName), Normalize(bonus.PropertyName) })
            .Where(alias => alias.Length > 0)
            .ToArray();
        if (mentionedBonusAliases.Length == 0)
        {
            return bonuses;
        }

        return bonuses.Where(bonus => mentionedBonusAliases.Contains(Normalize(bonus.ExtensionName), StringComparer.Ordinal) ||
                                     mentionedBonusAliases.Contains(Normalize(bonus.PropertyName), StringComparer.Ordinal))
            .ToArray();
    }

    private static IReadOnlyList<SparkyRobotStatFact> ToStatFacts(IEnumerable<AnswerStat> stats)
    {
        return stats.Select(stat => new SparkyRobotStatFact(stat.Label, stat.Value, stat.Unit)).ToArray();
    }

    private static List<RobotMatch> FindRobotMatches(
        IReadOnlyList<string> queries,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        var matches = new List<RobotMatch>();
        foreach (SparkyRobotRecord robot in bundle.Robots)
        {
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            AddAlias(GetRobotView(robot, languageId)?.DisplayName, aliases);
            AddAlias(GetRobotView(robot, 0)?.DisplayName, aliases);
            string[] matched = aliases.Where(alias => queries.Any(query => ContainsPhrase(query, alias))).ToArray();
            if (matched.Length > 0)
            {
                matches.Add(new RobotMatch(robot, matched.ToHashSet(StringComparer.Ordinal)));
            }
        }

        return KeepLongest(matches, match => match.Aliases, queries);
    }

    private static bool HasTierAwareCatalog(SparkyKnowledgeBundle bundle)
    {
        if (bundle.SchemaVersion < 4)
        {
            return false;
        }

        if (bundle.EntityResolutionRules is null ||
            bundle.Robots.Any(robot => robot.TierMetadata is null) ||
            bundle.RobotModules.Any(module => module.TierMetadata is null))
        {
            throw new InvalidDataException("Schema 4 robot and module catalogs require tier metadata and entity-resolution rules.");
        }

        return true;
    }

    private static SparkyEntityResolutionResult ResolveTierAwareRobots(
        string question,
        string? englishSearchQuery,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        SparkyEntityCandidate[] candidates = bundle.Robots
            .Where(robot => robot.TierMetadata is not null)
            .Select(robot => new SparkyEntityCandidate
            {
                DefinitionName = robot.DefinitionName,
                EntityKind = SparkyEntityKind.Robot,
                TierMetadata = robot.TierMetadata!,
                PublicAliases = GetRobotPublicAliases(robot, languageId),
                IsCalibrationTemplate = false
            })
            .Where(candidate => candidate.PublicAliases.Count > 0)
            .ToArray();

        return SparkyTierAwareEntityResolver.Resolve(
            question,
            englishSearchQuery,
            SparkyEntityKind.Robot,
            candidates,
            bundle.EntityResolutionRules!,
            languageId,
            matchAliasWithinQuestion: true);
    }

    private static SparkyEntityResolutionResult ResolveTierAwareModules(
        string question,
        string? englishSearchQuery,
        SparkyKnowledgeBundle bundle,
        int languageId,
        IReadOnlyList<RobotMatch> robotMatches)
    {
        SparkyEntityCandidate[] candidates = bundle.RobotModules
            .Where(module => module.TierMetadata is not null)
            .Select(module => new SparkyEntityCandidate
            {
                DefinitionName = module.DefinitionName,
                EntityKind = SparkyEntityKind.Item,
                TierMetadata = module.TierMetadata!,
                PublicAliases = GetModulePublicAliases(module, languageId),
                IsCalibrationTemplate = module.DefinitionName.EndsWith(
                    bundle.EntityResolutionRules!.CalibrationTemplate.Suffix,
                    StringComparison.OrdinalIgnoreCase)
            })
            .Where(candidate => candidate.PublicAliases.Count > 0)
            .ToArray();

        return SparkyTierAwareEntityResolver.Resolve(
            RemoveRobotMentionForModuleQuery(question, robotMatches, bundle.EntityResolutionRules!, languageId),
            englishSearchQuery is null
                ? null
                : RemoveRobotMentionForModuleQuery(englishSearchQuery, robotMatches, bundle.EntityResolutionRules!, languageId),
            SparkyEntityKind.Item,
            candidates,
            bundle.EntityResolutionRules!,
            languageId,
            matchAliasWithinQuestion: true);
    }

    private static string RemoveRobotMentionForModuleQuery(
        string question,
        IReadOnlyList<RobotMatch> robotMatches,
        SparkyEntityResolutionRules rules,
        int languageId)
    {
        var robotPhrases = new List<string>();
        foreach (RobotMatch match in robotMatches)
        {
            string[] robotNames = GetRobotPublicAliases(match.Record, languageId);
            robotPhrases.AddRange(robotNames);

            SparkyEntityTierMetadata? tier = match.Record.TierMetadata;
            SparkyEntityTierRule? tierRule = tier is null
                ? null
                : rules.FindTierRule(SparkyEntityKind.Robot, tier.TierType, tier.TierLevel);
            if (tierRule is null)
            {
                continue;
            }

            foreach (string tierAlias in rules.GetAliases(tierRule, languageId))
            {
                foreach (string robotName in robotNames)
                {
                    robotPhrases.Add($"{tierAlias} {robotName}");
                    robotPhrases.Add($"{robotName} {tierAlias}");
                }
            }
        }

        return RemoveRobotAliases(Normalize(question), robotPhrases.Select(Normalize).ToArray());
    }

    private static List<RobotMatch> FindTieredRobotMatches(
        SparkyEntityResolutionResult resolution,
        IReadOnlyList<string> queries,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        return resolution.CandidateDefinitionNames
            .Select(definition => bundle.Robots.FirstOrDefault(robot =>
                string.Equals(robot.DefinitionName, definition, StringComparison.OrdinalIgnoreCase)))
            .Where(robot => robot is not null)
            .Cast<SparkyRobotRecord>()
            .Select(robot => new RobotMatch(robot, GetRobotPublicAliases(robot, languageId)
                .Where(alias => queries.Any(query => ContainsPhrase(query, Normalize(alias))))
                .Select(Normalize)
                .ToHashSet(StringComparer.Ordinal)))
            .ToList();
    }

    private static List<ModuleMatch> FindTieredModuleMatches(
        SparkyEntityResolutionResult resolution,
        IReadOnlyList<string> queries,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        return resolution.CandidateDefinitionNames
            .Select(definition => bundle.RobotModules.FirstOrDefault(module =>
                string.Equals(module.DefinitionName, definition, StringComparison.OrdinalIgnoreCase)))
            .Where(module => module is not null)
            .Cast<SparkyRobotModuleRecord>()
            .Select(module => new ModuleMatch(module, GetModulePublicAliases(module, languageId)
                .Where(alias => queries.Any(query => ContainsPhrase(query, Normalize(alias))))
                .Select(Normalize)
                .ToHashSet(StringComparer.Ordinal)))
            .ToList();
    }

    private static string[] GetRobotPublicAliases(SparkyRobotRecord robot, int languageId) =>
        new[] { GetRobotView(robot, languageId)?.DisplayName, GetRobotView(robot, 0)?.DisplayName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Where(name => SafePublicName(name, robot.DefinitionName) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string[] GetModulePublicAliases(SparkyRobotModuleRecord module, int languageId) =>
        new[] { GetModuleView(module, languageId)?.DisplayName, GetModuleView(module, 0)?.DisplayName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Where(name => SafePublicName(name, module.DefinitionName) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static List<ModuleMatch> FindModuleMatches(
        IReadOnlyList<string> queries,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        var matches = new List<ModuleMatch>();
        foreach (SparkyRobotModuleRecord module in bundle.RobotModules)
        {
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            AddAlias(GetModuleView(module, languageId)?.DisplayName, aliases);
            AddAlias(GetModuleView(module, 0)?.DisplayName, aliases);
            string[] matched = aliases.Where(alias => queries.Any(query => ContainsPhrase(query, alias))).ToArray();
            if (matched.Length > 0)
            {
                matches.Add(new ModuleMatch(module, matched.ToHashSet(StringComparer.Ordinal)));
            }
        }

        return KeepLongest(matches, match => match.Aliases, queries);
    }

    private static List<TMatch> KeepLongest<TMatch>(
        List<TMatch> matches,
        Func<TMatch, IReadOnlyCollection<string>> aliases,
        IReadOnlyList<string> queries)
    {
        if (matches.Count < 2)
        {
            return matches;
        }

        return matches.Where(match => HasIndependentMention(match, matches, aliases, queries)).ToList();
    }

    private static bool HasIndependentMention<TMatch>(
        TMatch match,
        IReadOnlyList<TMatch> matches,
        Func<TMatch, IReadOnlyCollection<string>> aliases,
        IReadOnlyList<string> queries)
    {
        foreach (string alias in aliases(match))
        {
            foreach (string query in queries)
            {
                foreach ((int start, int length) in FindPhraseSpans(query, alias))
                {
                    bool coveredByMoreSpecificAlias = matches
                        .Where(candidate => !EqualityComparer<TMatch>.Default.Equals(candidate, match))
                        .SelectMany(aliases)
                        .Where(candidateAlias => WordCount(candidateAlias) > WordCount(alias))
                        .SelectMany(candidateAlias => FindPhraseSpans(query, candidateAlias))
                        .Any(span => start >= span.Start && start + length <= span.Start + span.Length);
                    if (!coveredByMoreSpecificAlias)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static IEnumerable<(int Start, int Length)> FindPhraseSpans(string normalizedText, string normalizedPhrase)
    {
        if (normalizedPhrase.Length == 0)
        {
            yield break;
        }

        string paddedText = $" {normalizedText} ";
        string needle = $" {normalizedPhrase} ";
        int searchFrom = 0;
        while (searchFrom < paddedText.Length)
        {
            int index = paddedText.IndexOf(needle, searchFrom, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            yield return (index + 1, normalizedPhrase.Length);
            searchFrom = index + 1;
        }
    }

    private static TMatch? SelectLongest<TMatch>(IReadOnlyList<TMatch> matches) where TMatch : class
    {
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool TryResolveContextRobot(
        string? contextItem,
        SparkyKnowledgeBundle bundle,
        int languageId,
        out SparkyRobotRecord? robot)
    {
        robot = null;
        if (string.IsNullOrWhiteSpace(contextItem) || contextItem.Length > 256)
        {
            return false;
        }

        string normalized = Normalize(contextItem);
        SparkyRobotRecord[] matches = bundle.Robots
            .Where(candidate => candidate.LocalizedViews.Values
                .Select(view => view.DisplayName)
                .Any(name => !string.IsNullOrWhiteSpace(name) && string.Equals(Normalize(name), normalized, StringComparison.Ordinal)))
            .ToArray();
        if (matches.Length != 1 || PublicRobotName(matches[0], languageId) is null)
        {
            return false;
        }

        if (bundle.SearchIndex.AmbiguousAliases.Any(entry =>
                (entry.LanguageId == languageId || entry.LanguageId == 0) &&
                string.Equals(entry.Alias, normalized, StringComparison.Ordinal) &&
                entry.Definitions.Count > 1))
        {
            return false;
        }

        robot = matches[0];
        return true;
    }

    private static bool IsFollowUp(string question, string? englishSearchQuery)
    {
        return (question.Length <= 160 && FollowUpPattern.IsMatch(question)) ||
               (!string.IsNullOrWhiteSpace(englishSearchQuery) &&
                englishSearchQuery.Length <= 160 && FollowUpPattern.IsMatch(englishSearchQuery));
    }

    private static string[] GetPublicCandidateNames(
        SparkyKnowledgeBundle bundle,
        IEnumerable<string> definitions,
        int languageId)
    {
        return definitions.Select(definition => GetPublicCandidateName(bundle, definition, languageId))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetPublicCandidateName(SparkyKnowledgeBundle bundle, string definition, int languageId)
    {
        SparkyRobotRecord? robot = bundle.Robots.FirstOrDefault(record =>
            string.Equals(record.DefinitionName, definition, StringComparison.OrdinalIgnoreCase));
        if (robot is not null)
        {
            return PublicRobotName(robot, languageId);
        }

        SparkyRobotModuleRecord? module = bundle.RobotModules.FirstOrDefault(record =>
            string.Equals(record.DefinitionName, definition, StringComparison.OrdinalIgnoreCase));
        if (module is not null)
        {
            return PublicModuleName(module, languageId);
        }

        WeaponItem? item = bundle.Items.FirstOrDefault(record =>
            string.Equals(record.DefinitionName, definition, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            item.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedItemView? localized);
            return SafePublicName(localized?.HasLocalizedDisplayName == true ? localized.DisplayName : item.DisplayName, item.DefinitionName);
        }

        ProductionRecipe? recipe = bundle.ProductionRecipes.FirstOrDefault(record =>
            string.Equals(record.DefinitionName, definition, StringComparison.OrdinalIgnoreCase));
        if (recipe is not null)
        {
            recipe.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRecipeView? localized);
            return SafePublicName(localized?.DisplayName ?? recipe.DisplayName, recipe.DefinitionName);
        }

        return null;
    }

    private static SparkyRobotLookupResult Ambiguous(IEnumerable<string?> names)
    {
        string[] candidates = names.Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        return candidates.Length > 1
            ? new SparkyRobotLookupResult("ambiguous", null, candidates)
            : NotFound();
    }

    private static SparkyRobotLookupResult AmbiguousTiered(IEnumerable<string?> names)
    {
        string[] candidates = names.Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        return new SparkyRobotLookupResult("ambiguous", null, candidates);
    }

    private static SparkyRobotLookupResult NotFound() => new("not_found", null, []);

    private static string? PublicRobotName(SparkyRobotRecord robot, int languageId) =>
        SafePublicName(GetRobotView(robot, languageId)?.DisplayName, robot.DefinitionName);
    private static string? PublicModuleName(SparkyRobotModuleRecord module, int languageId) =>
        SafePublicName(GetModuleView(module, languageId)?.DisplayName, module.DefinitionName);
    private static SparkyLocalizedRobotView? GetRobotView(SparkyRobotRecord robot, int languageId) =>
        robot.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRobotView? view) ? view : null;
    private static SparkyLocalizedRobotModuleView? GetModuleView(SparkyRobotModuleRecord module, int languageId) =>
        module.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRobotModuleView? view) ? view : null;

    private static void AddAlias(string? name, ISet<string> aliases)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            string alias = Normalize(name);
            if (alias.Length > 0)
            {
                aliases.Add(alias);
            }
        }
    }

    private static string? SafePublicName(string? displayName, string definitionName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        string trimmed = displayName.Trim();
        return string.Equals(trimmed, definitionName, StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(trimmed, @"\A(?:def_[a-z0-9_]+|ext_[a-z0-9_]+|[0-9]+)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? null
            : trimmed;
    }

    private static bool ContainsPhrase(string normalizedText, string normalizedPhrase)
    {
        return normalizedPhrase.Length > 0 &&
               (normalizedText == normalizedPhrase ||
                normalizedText.StartsWith(normalizedPhrase + " ", StringComparison.Ordinal) ||
                normalizedText.EndsWith(" " + normalizedPhrase, StringComparison.Ordinal) ||
                normalizedText.Contains(" " + normalizedPhrase + " ", StringComparison.Ordinal));
    }

    private static string Normalize(string value) => string.Join(' ', WordPattern.Matches(value)
        .Select(match => match.Value.ToLowerInvariant()));

    private static int WordCount(string value) => WordPattern.Matches(value).Count;

    private sealed record RobotMatch(SparkyRobotRecord Record, HashSet<string> Aliases);
    private sealed record ModuleMatch(SparkyRobotModuleRecord Record, HashSet<string> Aliases);
    private readonly record struct ModuleCandidateScore(int Overlap, int OrderedPrefixLength, int ExtraCandidateTokens);
}
