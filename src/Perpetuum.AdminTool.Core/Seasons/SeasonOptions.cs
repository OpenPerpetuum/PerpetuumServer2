using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons;

public record ActivityTypeOption(SeasonActivityType Value, string Label);

public record ScoringModeOption(SeasonScoringMode Value, string Label);
