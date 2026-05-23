using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public record TodaysDailyObjectiveRow(
        string Name,
        SeasonActivityType ActivityType,
        long TargetValue,
        int CompletionsToday);
}
