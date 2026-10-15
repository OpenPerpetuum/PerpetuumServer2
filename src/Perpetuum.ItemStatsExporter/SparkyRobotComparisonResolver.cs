namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyRobotComparisonResolver : ISparkyComparisonDomainResolver
{
    public string Domain => "Robot";

    public SparkyComparisonResolution Resolve(
        SparkyComparisonRequest request,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        SparkyRobotRecord? first = bundle.Robots.FirstOrDefault(robot =>
            string.Equals(robot.DefinitionName, request.FirstDefinitionName, StringComparison.OrdinalIgnoreCase));
        SparkyRobotRecord? second = bundle.Robots.FirstOrDefault(robot =>
            string.Equals(robot.DefinitionName, request.SecondDefinitionName, StringComparison.OrdinalIgnoreCase));
        if (first is null || second is null ||
            !first.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRobotView? firstView) ||
            !second.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRobotView? secondView) ||
            string.IsNullOrWhiteSpace(firstView.DisplayName) || string.IsNullOrWhiteSpace(secondView.DisplayName))
        {
            return new SparkyComparisonResolution("unavailable", [], null, [], null, null, null,
                "subject_data_unavailable", []);
        }

        return SparkyComparisonCoordinator.Compare(
            Domain,
            firstView.DisplayName,
            secondView.DisplayName,
            firstView.AssembledStats,
            secondView.AssembledStats,
            request);
    }
}
