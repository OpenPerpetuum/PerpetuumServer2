using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal enum SparkyConversationScope
{
    Game,
    SmallTalk,
    OutOfScope,
    Ambiguous
}

internal enum SparkyFollowUpAction
{
    None,
    Continue,
    SelectCandidate,
    Clarify
}

/// <summary>
/// Applies code-owned topic boundaries after model interpretation. It never grants evidence access.
/// </summary>
internal static class SparkyScopePolicy
{
    private static readonly Regex ExplicitRealWorldPattern = new(
        @"\b(?:real\s+world|irl|in\s+real\s+life|current\s+(?:president|government|election)|today(?:'s)?\s+(?:politics|president)|united\s+states|u\.s\.?|usa|ukraine|israel|palestine|germany|france|russia|china|european\s+union)\b|(?:реальн\w*\s+мир|в\s+реальной\s+жизни|сша|украин\w*|израил\w*|палестин\w*|германи\w*|франци\w*|росси\w*|кита\w*)|(?:realen\s+welt|wirklichen\s+leben|vereinigten\s+staaten|ukraine|israel|palästina|deutschland|frankreich|russland|china)|(?:mundo\s+real|vida\s+real|estados\s+unidos|ucrânia|israel|palestina|alemanha|frança|rússia|china)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PoliticsPattern = new(
        @"\b(?:politics?|political|election|president|prime\s+minister|parliament|congress|political\s+party|government|geopolitics?|war|conflict|sanctions?)\b|(?:политик\w*|выбор\w*|президент\w*|премьер-министр\w*|парламент\w*|правительств\w*|войн\w*|конфликт\w*|санкци\w*)|(?:politik\w*|wahl\w*|präsident\w*|bundestag|parlament\w*|regierung\w*|krieg\w*|konflikt\w*|sanktion\w*)|(?:polític\w*|eleiç\w*|president\w*|primeiro-ministro|parlament\w*|governo\w*|guerra\w*|conflito\w*|sanção\w*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveTopicPattern = new(
        @"\b(?:religion|religious|race|racial|ethnicity|ethnic|sexual\s+orientation|gender\s+identity|abortion|immigration|terrorism|extremism)\b|(?:религи\w*|расов\w*|этническ\w*|сексуальн\w*\s+ориентац\w*|гендерн\w*\s+идентичност\w*|аборт\w*|иммиграц\w*|терроризм\w*|экстремизм\w*)|(?:religion\w*|rass\w*|ethnisch\w*|sexuelle\s+orientierung|geschlechtsidentität|abtreib\w*|einwander\w*|terrorismus|extremismus)|(?:religião\w*|raça|étnic\w*|orientação\s+sexual|identidade\s+de\s+gênero|aborto\w*|imigraç\w*|terrorismo|extremismo)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GameContextPattern = new(
        @"\b(?:perpetuum|in[- ]game|game\s+world|syndicate|corporation|agent|robot|robotics|planet|extension|module|item|recipe|season)\b|(?:перпетуум|синдикат|корпораци\w*|агент\w*|робот\w*|планет\w*|модул\w*|предмет\w*|рецепт\w*|сезон\w*)|(?:syndikat|konzern|roboter|planet|modul|gegenstand|rezept|saison)|(?:sindicato|corporação|agente|robô|planeta|módulo|item|receita|temporada)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static SparkyConversationScope Classify(
        string question,
        SparkyIntent intent,
        SparkyConversationScope proposedScope = SparkyConversationScope.Ambiguous,
        string? contextualizedQuery = null)
    {
        string text = string.Join(' ', question, contextualizedQuery);
        bool gameContext = GameContextPattern.IsMatch(text);
        if (ExplicitRealWorldPattern.IsMatch(text) ||
            ((PoliticsPattern.IsMatch(text) || SensitiveTopicPattern.IsMatch(text)) && !gameContext))
        {
            return SparkyConversationScope.OutOfScope;
        }

        if (intent is SparkyIntent.StructuredItem or SparkyIntent.StructuredRobot or
            SparkyIntent.StructuredModuleCatalog or SparkyIntent.MechanicsLore or SparkyIntent.Combined)
        {
            return SparkyConversationScope.Game;
        }

        if (intent == SparkyIntent.SmallTalk)
        {
            return SparkyConversationScope.SmallTalk;
        }

        return proposedScope == SparkyConversationScope.Ambiguous
            ? SparkyConversationScope.OutOfScope
            : proposedScope;
    }
}
