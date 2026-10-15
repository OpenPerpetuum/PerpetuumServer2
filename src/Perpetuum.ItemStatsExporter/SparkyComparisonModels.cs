namespace Perpetuum.ItemStatsExporter;

internal enum SparkyComparisonDirection
{
    Higher,
    Lower,
    Unspecified
}

/// <summary>
/// A bounded interpreter proposal. Subject and metric strings are hints only;
/// comparison resolvers must validate them against the active knowledge bundle.
/// </summary>
internal sealed record SparkyComparisonIntent(
    bool IsComparison,
    bool IsContextual,
    IReadOnlyList<string> SubjectReferences,
    string? MetricPhrase,
    SparkyComparisonDirection Direction,
    bool CompareAllSharedFields);

internal sealed record SparkyComparisonDiagnosticSubject(
    string DisplayName,
    string ReferenceSource);

internal sealed record SparkyComparisonDiagnostics(
    string? Domain,
    IReadOnlyList<SparkyComparisonDiagnosticSubject> Subjects,
    string? MetricPhrase,
    string ResultCategory,
    IReadOnlyList<SparkyComparisonFact> Facts);
