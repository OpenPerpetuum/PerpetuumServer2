namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyItemComparisonResolver : ISparkyComparisonDomainResolver
{
    public string Domain => "Item";

    public SparkyComparisonResolution Resolve(
        SparkyComparisonRequest request,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        WeaponItem? first = bundle.Items.FirstOrDefault(item =>
            string.Equals(item.DefinitionName, request.FirstDefinitionName, StringComparison.OrdinalIgnoreCase));
        WeaponItem? second = bundle.Items.FirstOrDefault(item =>
            string.Equals(item.DefinitionName, request.SecondDefinitionName, StringComparison.OrdinalIgnoreCase));
        if (first is null || second is null ||
            !first.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedItemView? firstView) ||
            !second.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedItemView? secondView))
        {
            return new SparkyComparisonResolution("unavailable", [], null, [], null, null, null,
                "subject_data_unavailable", []);
        }

        string? firstName = firstView.DisplayName;
        string? secondName = secondView.DisplayName;
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(secondName))
        {
            return new SparkyComparisonResolution("unavailable", [], null, [], null, null, null,
                "subject_translation_unavailable", []);
        }

        return SparkyComparisonCoordinator.Compare(
            Domain,
            firstName,
            secondName,
            firstView.AnswerView.Stats,
            secondView.AnswerView.Stats,
            request);
    }
}
