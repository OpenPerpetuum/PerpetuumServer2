using System.Globalization;
using System.Text.RegularExpressions;
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyRecipeAnswerFormatter
{
    private static readonly Regex RecipeOrMaterialsPattern = new(
        @"\b(?:recipes?|rezept\w*|receitas?|рецепт\p{L}*|materials|ingredients?|components?|materialien|rohstoffe|komпонент(?:e|en)|materiais|ingredientes|component(?:e?s?)|материалы|материалов|компонент(?:ы|ов)?|сырь[её])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FullMaterialsPattern = new(
        @"\b(?:full|complete|total|all|raw|vollst[aä]ndig\w*|komplett\w*|gesamt\w*|alle[nrms]?|complet\w*|integral\w*|tod[oa]s?|полн\w*|все|всё|общ\w*|сырь[её])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryFormat(
        string question,
        string? englishSearchQuery,
        ItemStatsLookupAnswer item,
        int languageId,
        out string answer)
    {
        answer = string.Empty;
        if (!ContainsRecipeRequest(question, englishSearchQuery))
        {
            return false;
        }

        bool wantsFullMaterials = ContainsFullMaterialsRequest(question, englishSearchQuery);
        if (wantsFullMaterials &&
            item.FullMaterialsAvailable &&
            item.FullMaterials is { Materials.Count: > 0 } fullMaterials)
        {
            answer = FormatList(
                item.DisplayName,
                fullMaterials.OutputQuantity,
                fullMaterials.Materials,
                languageId,
                fullMaterials: true);
            return true;
        }

        if (item.RecipeTranslationsComplete && item.Recipe is { Components.Count: > 0 } recipe)
        {
            string header = wantsFullMaterials
                ? GetFullMaterialsUnavailableHeader(languageId, item.DisplayName, recipe.OutputQuantity)
                : GetDirectRecipeHeader(languageId, item.DisplayName, recipe.OutputQuantity);
            answer = FormatList(header, recipe.Components, languageId);
            return true;
        }

        answer = SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.UnsupportedEvidence);
        return true;
    }

    internal static bool ContainsRecipeRequest(string question, string? englishSearchQuery)
    {
        return RecipeOrMaterialsPattern.IsMatch(question) ||
               !string.IsNullOrWhiteSpace(englishSearchQuery) && RecipeOrMaterialsPattern.IsMatch(englishSearchQuery);
    }

    internal static bool ContainsFullMaterialsRequest(string question, string? englishSearchQuery)
    {
        return FullMaterialsPattern.IsMatch(question) ||
               !string.IsNullOrWhiteSpace(englishSearchQuery) && FullMaterialsPattern.IsMatch(englishSearchQuery);
    }

    public static IReadOnlyList<string> GetRequiredExactStrings(
        string question,
        string? englishSearchQuery,
        ItemStatsLookupAnswer item)
    {
        var required = new List<string> { item.DisplayName };
        if (ContainsRecipeRequest(question, englishSearchQuery))
        {
            bool useFullMaterials = ContainsFullMaterialsRequest(question, englishSearchQuery) &&
                                    item.FullMaterialsAvailable &&
                                    item.FullMaterials is { Materials.Count: > 0 };
            int outputQuantity = useFullMaterials
                ? item.FullMaterials!.OutputQuantity
                : item.Recipe?.OutputQuantity ?? 0;
            var components = useFullMaterials
                ? item.FullMaterials!.Materials
                : item.Recipe?.Components ?? [];
            if (outputQuantity > 0)
            {
                AddUnique(required, FormatNumber(outputQuantity, item.LanguageId));
            }
            foreach (var component in components)
            {
                AddUnique(required, $"{FormatNumber(component.Amount, item.LanguageId)} {component.DisplayName}");
            }

            return required;
        }

        bool requestsStats = Regex.IsMatch(
            string.Join(' ', question, englishSearchQuery),
            @"\b(?:stats?|statistics|attributes|properties|accuracy|damage|range|optimal|falloff|cycle\s*time|cpu|power\s*grid|powergrid|core\s+usage|ammo|ammunition|statistiken|eigenschaften|werte|schaden|reichweite|genauigkeit|dano|alcance|precis[aã]o|характеристик\w*|статистик\w*|урон\w*|дальност\w*|точност\w*)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (requestsStats)
        {
            foreach (var stat in item.Stats)
            {
                AddUnique(required, $"{stat.Label}: {stat.Value} {stat.Unit}".Trim());
            }
        }

        bool requestsAmmo = Regex.IsMatch(
            string.Join(' ', question, englishSearchQuery),
            @"\b(?:ammo|ammunition|ammo\s+types?|munitions?|munition|muni[cç][aã]o|боеприпас\w*|аммуниц\w*)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (requestsAmmo)
        {
            foreach (var ammo in item.AmmoVariants)
            {
                AddUnique(required, ammo.DisplayName);
                foreach (var damage in ammo.Damage)
                {
                    AddUnique(required, $"{damage.Type}: {damage.Value}");
                }
            }
        }

        return required;
    }

    private static void AddUnique(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.Ordinal))
        {
            values.Add(value);
        }
    }

    private static string FormatNumber(long value, int languageId)
    {
        var culture = languageId switch
        {
            2 => CultureInfo.GetCultureInfo("de-DE"),
            3 => CultureInfo.GetCultureInfo("pt-BR"),
            4 => CultureInfo.GetCultureInfo("ru-RU"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };
        return value.ToString("N0", culture);
    }

    private static string FormatList(
        string displayName,
        int outputQuantity,
        IReadOnlyList<ProductionRecipeAnswerComponent> components,
        int languageId,
        bool fullMaterials)
    {
        string header = fullMaterials
            ? GetFullMaterialsHeader(languageId, displayName, outputQuantity)
            : GetDirectRecipeHeader(languageId, displayName, outputQuantity);
        return FormatList(header, components, languageId);
    }

    private static string FormatList(
        string header,
        IReadOnlyList<ProductionRecipeAnswerComponent> components,
        int languageId)
    {
        var culture = languageId switch
        {
            2 => CultureInfo.GetCultureInfo("de-DE"),
            3 => CultureInfo.GetCultureInfo("pt-BR"),
            4 => CultureInfo.GetCultureInfo("ru-RU"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };
        return header + "\n" + string.Join("\n", components.Select(component =>
            $"- {component.Amount.ToString("N0", culture)} {component.DisplayName}"));
    }

    private static string GetDirectRecipeHeader(int languageId, string itemName, int outputQuantity)
    {
        return languageId switch
        {
            2 => $"{itemName} — direktes Rezept (pro Produktionszyklus; Ausstoß: {outputQuantity}):",
            3 => $"Receita direta: {itemName} (por ciclo de produção; produção: {outputQuantity}):",
            4 => $"Прямой рецепт: «{itemName}» (за производственный цикл; выпуск: {outputQuantity}):",
            _ => $"Direct recipe for {itemName} (per production cycle; output: {outputQuantity}):"
        };
    }

    private static string GetFullMaterialsHeader(int languageId, string itemName, int outputQuantity)
    {
        return languageId switch
        {
            2 => $"{itemName} — vollständige Rohstoffe (pro Produktionszyklus; Ausstoß: {outputQuantity}):",
            3 => $"Materiais completos: {itemName} (por ciclo de produção; produção: {outputQuantity}):",
            4 => $"Полный список материалов: «{itemName}» (за производственный цикл; выпуск: {outputQuantity}):",
            _ => $"Full materials for {itemName} (per production cycle; output: {outputQuantity}):"
        };
    }

    private static string GetFullMaterialsUnavailableHeader(int languageId, string itemName, int outputQuantity)
    {
        return languageId switch
        {
            2 => $"Die vollständige Materialliste ist in meinen lokalen Daten nicht verfügbar. Direktes Rezept: {itemName} (pro Produktionszyklus; Ausstoß: {outputQuantity}):",
            3 => $"A lista completa de materiais não está disponível nos meus dados locais. Receita direta: {itemName} (por ciclo de produção; produção: {outputQuantity}):",
            4 => $"Полный список материалов недоступен в моих локальных данных. Прямой рецепт: «{itemName}» (за производственный цикл; выпуск: {outputQuantity}):",
            _ => $"The full materials list isn't available in my local data. Direct recipe for {itemName} (per production cycle; output: {outputQuantity}):"
        };
    }
}
