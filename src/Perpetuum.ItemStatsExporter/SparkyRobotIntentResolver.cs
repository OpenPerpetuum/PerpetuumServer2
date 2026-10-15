using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyRobotIntentResolver
{
    private static readonly Regex StatsPattern = new(
        @"\b(?:stats?|statistics|attributes?|properties|values?|werte|eigenschaften|attribute|statistiken|estat[ií]sticas|atributos|propriedades|valores|характеристик\w*|статистик\w*|параметр\w*|свойств\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BonusPattern = new(
        @"\b(?:bonuses|bonus|boni|b[oô]nus|bónus|бонус\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ComponentPattern = new(
        @"\b(?:components?|parts?|head|chassis|legs|komponenten?|bestandteile?|bauteile?|teile|componentes?|partes|peças|pecas|cabeça|cabeca|chassi|pernas|компонент\w*|част\w*|голов\w*|шасс\w*|ног\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CompatibilityPattern = new(
        @"\b(?:fit|fits|fitting|compatible|compatibility|equip|equipped|install|installed|passen|passt|passend|kompatibel|einbauen|cabe|encaix\w*|serve|compat[ií]vel|equip\w*|instal\w*|подойд\w*|совместим\w*|установ\w*|помест\w*|встав\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RobotTermPattern = new(
        @"\b(?:robots?|robot models?|roboter|rob[oô]s?|робот\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FastestPattern = new(
        @"\b(?:fastest|quickest|top\s+speed|highest\s+(?:top\s+)?speed|maximum\s+speed|most\s+speed|schnellst\w*|h[oö]chst\w*\s+geschwindigkeit|mais\s+r[aá]pid\w*|maior\s+velocidade|сам\w*\s+быстр\w*|наибольш\w*\s+скорост\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ComparePattern = new(
        @"\b(?:compare|comparison|compared|difference\w*|versus|vs\.?|vergleich\w*|unterschied\w*|compar\w*|diferen[çc]\w*|сравн\w*|разниц\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LaserTermPattern = new(
        @"\b(?:lasers?|laserwaffen|laserwaffe|l[aá]ser\w*|лазер\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LaserCatalogPattern = new(
        @"\b(?:list|all|which|know|catalog(?:ue)?|liste|alle|welche|kennen|lista|todos|todas|quais|conhece|перечисл\w*|все|какие|знаешь)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LaserCatalogSubjectPattern = new(
        @"\b(?:lasers|laser\s+(?:modules|weapons)|laserwaffen|l[aá]seres|(?:welche|alle)\s+laser|(?:m[oó]dulos?|armas?)\s+(?:de|do|da)\s+l[aá]ser\w*|лазеры|лазер\w*\s+модул\w*|модул\w*\s+лазер\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RecipePattern = new(
        @"\b(?:recipes?|production|materials?|ingredients?|manufacturing|rezept\w*|herstell\w*|materialien|receita\w*|materiais|рецепт\w*|материал\w*|ингредиент\w*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static SparkyQuestionInterpretation CorrectStructuredItemQuestion(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyKnowledgeBundle bundle)
    {
        if (interpretation.Comparison is { IsComparison: true })
        {
            // A conversational comparison is routed by SparkyComparisonSubjectResolver.
            // Never reinterpret its omitted-subject follow-up as a global Fastest query.
            return interpretation.Intent == SparkyIntent.StructuredRobot
                ? interpretation with { RobotFocus = SparkyRobotQuestionFocus.CompareStats }
                : interpretation;
        }

        string searchText = string.Join(' ', new[] { question, interpretation.EnglishSearchQuery }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (LaserTermPattern.IsMatch(searchText) && LaserCatalogPattern.IsMatch(searchText) &&
            LaserCatalogSubjectPattern.IsMatch(searchText))
        {
            return interpretation with
            {
                Intent = SparkyIntent.StructuredModuleCatalog,
                RobotFocus = null,
                CatalogFocus = SparkyModuleCatalogFocus.Lasers
            };
        }

        if (ComparePattern.IsMatch(searchText))
        {
            IReadOnlyList<SparkyRobotRecord> mentionedRobots = SparkyRobotLookup.FindMentionedRobots(
                question,
                interpretation.EnglishSearchQuery,
                bundle,
                interpretation.LanguageId);
            if (mentionedRobots.Count >= 2)
            {
                if (BonusPattern.IsMatch(searchText) ||
                    ComponentPattern.IsMatch(searchText) ||
                    CompatibilityPattern.IsMatch(searchText) ||
                    RecipePattern.IsMatch(searchText))
                {
                    return interpretation;
                }

                return interpretation with
                {
                    Intent = SparkyIntent.StructuredRobot,
                    RobotFocus = SparkyRobotQuestionFocus.CompareStats,
                    CatalogFocus = null
                };
            }
        }

        if (RobotTermPattern.IsMatch(searchText) && FastestPattern.IsMatch(searchText))
        {
            IReadOnlyList<SparkyRobotRecord> mentionedRobots = SparkyRobotLookup.FindMentionedRobots(
                question,
                interpretation.EnglishSearchQuery,
                bundle,
                interpretation.LanguageId);
            SparkyRobotQuestionFocus speedFocus = mentionedRobots.Count switch
            {
                0 => SparkyRobotQuestionFocus.Fastest,
                1 => SparkyRobotQuestionFocus.Stats,
                _ => SparkyRobotQuestionFocus.CompareStats
            };
            return interpretation with
            {
                Intent = SparkyIntent.StructuredRobot,
                RobotFocus = speedFocus,
                CatalogFocus = null
            };
        }

        if (interpretation.Intent != SparkyIntent.StructuredItem ||
            !SparkyRobotLookup.ContainsRobotMention(
                question,
                interpretation.EnglishSearchQuery,
                bundle,
                interpretation.LanguageId))
        {
            return interpretation;
        }

        bool hasStats = StatsPattern.IsMatch(searchText);
        bool hasBonuses = BonusPattern.IsMatch(searchText);
        bool hasComponents = ComponentPattern.IsMatch(searchText);

        SparkyRobotQuestionFocus? focus = null;
        if (CompatibilityPattern.IsMatch(searchText))
        {
            focus = SparkyRobotQuestionFocus.ModuleCompatibility;
        }
        else if (hasBonuses && (hasStats || hasComponents) || hasStats && hasComponents)
        {
            focus = SparkyRobotQuestionFocus.General;
        }
        else if (hasBonuses)
        {
            focus = SparkyRobotQuestionFocus.ChassisBonus;
        }
        else if (hasComponents)
        {
            focus = SparkyRobotQuestionFocus.Components;
        }
        else if (hasStats)
        {
            focus = SparkyRobotQuestionFocus.Stats;
        }

        return focus is null
            ? interpretation
            : interpretation with
            {
                Intent = SparkyIntent.StructuredRobot,
                RobotFocus = focus,
                CatalogFocus = null
            };
    }
}
