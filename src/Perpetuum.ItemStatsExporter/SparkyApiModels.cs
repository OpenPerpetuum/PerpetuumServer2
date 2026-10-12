namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyApiRequest(string? Question, string? ContextItem = null, int? ContextLanguageId = null);

internal sealed record SparkyApiResponse(
    string Status,
    string? Item = null,
    string? Answer = null,
    IReadOnlyList<string>? Candidates = null,
    int? LanguageId = null);

internal sealed record SparkyApiError(string Error);

internal sealed record SparkyLocalizedAnswer(int LanguageId, string Answer);
