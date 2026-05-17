using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewRobot;

public record RobotTemplateRelationData(
    double ItemScoreSum,
    int RaceId,
    int MissionLevel,
    int MissionLevelOverride,
    int KillEp,
    string? Note);

public partial class RobotTemplateRelationPanelViewModel : ObservableObject
{
    [ObservableProperty] private double _itemScoreSum;
    [ObservableProperty] private int _raceId;
    [ObservableProperty] private int _missionLevel;
    [ObservableProperty] private int _missionLevelOverride;
    [ObservableProperty] private int _killEp;
    [ObservableProperty] private string _note = "";

    public void LoadFromClone(RobotTemplateRelationData data)
    {
        ItemScoreSum = data.ItemScoreSum;
        RaceId = data.RaceId;
        MissionLevel = data.MissionLevel;
        MissionLevelOverride = data.MissionLevelOverride;
        KillEp = data.KillEp;
        Note = data.Note ?? "";
    }
}
