using System.Text.Json;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyEvaluationValidator
{
    public const int MaximumTurns = 12;
    public const int MaximumTurnCharacters = 2000;
    public const int MaximumSequenceCharacters = 8000;
    public const int MaximumExpectationEntries = 64;
    public const int MaximumRequestBytes = 64 * 1024;

    public static bool TryValidate(SparkyEvaluationExampleRequest request, out string error)
    {
        error = "invalid_evaluation";
        if (request is null || request.RequestId == Guid.Empty || request.Note?.Length > 500 ||
            request.Note?.Any(char.IsControl) == true)
            return false;

        IReadOnlyList<SparkyEvaluationTurn>? turns = request.Turns;
        if (turns is { Count: 0 or > MaximumTurns })
            return false;
        if (!SparkyTraceEventValidator.IsValidEvaluationExpectation(request.ExpectedIntent, request.ExpectedProperties))
            return false;

        int textLength = 0;
        int expectationEntries = 0;
        foreach (var turn in turns ?? [])
        {
            if (turn is null || string.IsNullOrWhiteSpace(turn.Question) ||
                turn.Question.Length > MaximumTurnCharacters || !SparkyTraceEventValidator.IsContentTextValid(turn.Question, MaximumTurnCharacters) ||
                turn.ExpectedScope is not null &&
                    (!Enum.TryParse<SparkyConversationScope>(turn.ExpectedScope, ignoreCase: false, out var scope) || !Enum.IsDefined(scope)) ||
                turn.ExpectedIntent is not null &&
                    (!Enum.TryParse<SparkyIntent>(turn.ExpectedIntent, ignoreCase: false, out var intent) || !Enum.IsDefined(intent)) ||
                turn.ExpectedEntity?.Length > 120 || !ValidText(turn.ExpectedEntity, 120) ||
                !ValidStringList(turn.ExpectedSourceIds, 32, 128, identifierOnly: true) ||
                !ValidStringList(turn.RequiredFacts, 16, 200) ||
                !ValidStringList(turn.ForbiddenClaims, 16, 200) ||
                !SparkyTraceEventValidator.IsValidEvaluationExpectation(turn.ExpectedIntent, turn.ExpectedProperties ?? []))
                return false;

            textLength += turn.Question.Length;
            expectationEntries += (turn.ExpectedScope is null ? 0 : 1) +
                                  (turn.ExpectedIntent is null ? 0 : 1) +
                                  (turn.ExpectedEntity is null ? 0 : 1) +
                                  (turn.ExpectedSourceIds?.Count ?? 0) +
                                  (turn.RequiredFacts?.Count ?? 0) +
                                  (turn.ForbiddenClaims?.Count ?? 0) +
                                  (turn.ExpectedProperties?.Count ?? 0);
            if (textLength > MaximumSequenceCharacters || expectationEntries > MaximumExpectationEntries)
                return false;
        }

        try
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(request);
            try
            {
                if (json.Length > MaximumRequestBytes)
                    return false;
            }
            finally
            {
                Array.Clear(json, 0, json.Length);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static IReadOnlyList<SparkyEvaluationTurn> FromLegacy(
        string question,
        string? expectedIntent,
        IReadOnlyList<string>? expectedProperties) =>
        [new SparkyEvaluationTurn(question, ExpectedIntent: expectedIntent, ExpectedProperties: expectedProperties ?? [])];

    private static bool ValidStringList(
        IReadOnlyList<string>? values,
        int maximumCount,
        int maximumCharacters,
        bool identifierOnly = false)
    {
        if (values is null)
            return true;
        if (values.Count > maximumCount)
            return false;
        foreach (string value in values)
        {
            if (!ValidText(value, maximumCharacters))
                return false;
            if (identifierOnly && value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
                return false;
        }
        return true;
    }

    private static bool ValidText(string? value, int maximumCharacters) =>
        value is null || !string.IsNullOrWhiteSpace(value) && value.Length <= maximumCharacters &&
        !value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
}
