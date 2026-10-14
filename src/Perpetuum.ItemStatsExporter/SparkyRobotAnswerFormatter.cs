namespace Perpetuum.ItemStatsExporter;

internal static class SparkyRobotAnswerFormatter
{
    public static string? FormatLaserCatalog(SparkyKnowledgeBundle bundle, int languageId, int maxCharacters)
    {
        if (maxCharacters <= 0)
        {
            return string.Empty;
        }

        string[] names = bundle.RobotModules
            .Where(module => module.IsLaser == true && module.LocalizedViews.TryGetValue(languageId, out var view) &&
                             !string.IsNullOrWhiteSpace(view.DisplayName))
            .Select(module => module.LocalizedViews[languageId].DisplayName!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (names.Length == 0)
        {
            return null;
        }

        string intro = languageId switch
        {
            2 => "Im lokalen Katalog finde ich diese Lasermodule:",
            3 => "No catálogo local, encontrei estes módulos laser:",
            4 => "В локальном каталоге есть такие лазерные модули:",
            _ => "The local catalog lists these laser modules:"
        };

        for (int count = names.Length; count > 0; count--)
        {
            string suffix = count == names.Length
                ? string.Empty
                : languageId switch
                {
                    2 => $" und noch {names.Length - count}",
                    3 => $" e mais {names.Length - count}",
                    4 => $" и ещё {names.Length - count}",
                    _ => $" and {names.Length - count} more"
                };
            string answer = $"{intro} {string.Join(", ", names.Take(count))}{suffix}";
            if (answer.Length <= maxCharacters)
            {
                return answer;
            }
        }

        return Limit(intro, maxCharacters);
    }

    public static string FormatUnknownModuleCandidates(
        int languageId,
        IReadOnlyList<string> candidates,
        int maxCharacters)
    {
        if (maxCharacters <= 0 || candidates.Count == 0)
        {
            return string.Empty;
        }

        string names = string.Join(", ", candidates.Take(5));
        string answer = languageId switch
        {
            2 => $"Diesen Modulnamen finde ich nicht. Meintest du vielleicht: {names}?",
            3 => $"Não encontrei esse módulo com esse nome. Você quis dizer: {names}?",
            4 => $"Не нашёл модуль с таким названием. Возможно, вы имели в виду: {names}?",
            _ => $"I can't match that module name. Did you mean one of these: {names}?"
        };
        return Limit(answer, maxCharacters);
    }

    public static string GetCompatibilityScope(int languageId) => languageId switch
    {
        2 => "Das bestätigt nur die Slot-Eignung; freie Slots sowie die gesamte CPU- und Netzlast deines aktuellen Roboters kann ich nicht prüfen.",
        3 => "Isto confirma apenas a elegibilidade dos slots; não consigo verificar os slots livres nem a capacidade total de CPU e energia do seu robô atual.",
        4 => "Это подтверждает только допустимость установки в слот; свободные слоты и общий запас ЦП и энергосети вашего текущего робота я проверить не могу.",
        _ => "This confirms static slot eligibility only; I can't check your current free slots or total CPU/power-grid capacity."
    };

    public static string AppendCompatibilityScope(string answer, int languageId, int maxCharacters)
    {
        if (maxCharacters <= 0)
        {
            return string.Empty;
        }

        string scope = GetCompatibilityScope(languageId);
        if (answer.Contains(scope, StringComparison.OrdinalIgnoreCase))
        {
            return Limit(answer, maxCharacters);
        }

        string separator = Environment.NewLine;
        int answerBudget = maxCharacters - scope.Length - separator.Length;
        if (answerBudget <= 0)
        {
            return Limit(scope, maxCharacters);
        }

        string boundedAnswer = Limit(answer.Trim(), answerBudget);
        return string.IsNullOrWhiteSpace(boundedAnswer)
            ? Limit(scope, maxCharacters)
            : $"{boundedAnswer}{separator}{scope}";
    }

    private static string Limit(string value, int maximum)
    {
        if (value.Length <= maximum)
        {
            return value;
        }

        if (maximum == 1)
        {
            return "…";
        }

        int end = maximum - 1;
        int boundary = value.AsSpan(0, end).LastIndexOfAny(" \r\n\t".AsSpan());
        if (boundary > maximum / 2)
        {
            end = boundary;
        }

        return $"{value[..end].TrimEnd()}…";
    }
}
