namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyApiRequest(string? Question, string? ContextItem = null);

internal sealed record SparkyApiResponse(
    string Status,
    string? Item = null,
    string? Answer = null,
    IReadOnlyList<string>? Candidates = null);

internal sealed record SparkyApiError(string Error);
