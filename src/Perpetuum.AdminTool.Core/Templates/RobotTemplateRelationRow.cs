using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Templates
{
    public partial class RobotTemplateRelationRow : ObservableObject
    {
        public bool IsNew { get; set; }
        public RobotTemplateRelationSnapshot Original { get; private set; }

        // Resolved labels (not persisted) — populated from lookup tables for display.
        [ObservableProperty] private string _definitionName = "";
        [ObservableProperty] private string _templateName = "";

        // PK column. Editable; the diff layer keys baselines by Original.Definition so a user
        // can change this value and the resulting UPDATE writes WHERE definition = old, SET definition = new.
        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _templateId;
        [ObservableProperty] private int _itemScoreSum;
        [ObservableProperty] private int _raceId;
        [ObservableProperty] private int? _missionLevel;
        [ObservableProperty] private int? _missionLevelOverride;
        [ObservableProperty] private int? _killEp;
        [ObservableProperty] private string? _note;

        public RobotTemplateRelationRow(RobotTemplateRelationSnapshot snapshot)
        {
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(RobotTemplateRelationSnapshot snapshot)
        {
            Original = snapshot;
            Definition = snapshot.Definition;
            TemplateId = snapshot.TemplateId;
            ItemScoreSum = snapshot.ItemScoreSum;
            RaceId = snapshot.RaceId;
            MissionLevel = snapshot.MissionLevel;
            MissionLevelOverride = snapshot.MissionLevelOverride;
            KillEp = snapshot.KillEp;
            Note = snapshot.Note;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new RobotTemplateRelationSnapshot
            {
                Definition = Definition,
                TemplateId = TemplateId,
                ItemScoreSum = ItemScoreSum,
                RaceId = RaceId,
                MissionLevel = MissionLevel,
                MissionLevelOverride = MissionLevelOverride,
                KillEp = KillEp,
                Note = Note
            };
        }

        public static RobotTemplateRelationRow CreateNew(
            int definition, int templateId, int itemScoreSum, int raceId,
            int? missionLevel, int? missionLevelOverride, int? killEp, string? note)
        {
            var snap = new RobotTemplateRelationSnapshot
            {
                Definition = definition,
                TemplateId = templateId,
                ItemScoreSum = itemScoreSum,
                RaceId = raceId,
                MissionLevel = missionLevel,
                MissionLevelOverride = missionLevelOverride,
                KillEp = killEp,
                Note = note
            };
            return new RobotTemplateRelationRow(snap) { IsNew = true };
        }
    }

    public class RobotTemplateRelationSnapshot
    {
        public int Definition { get; init; }
        public int TemplateId { get; init; }
        public int ItemScoreSum { get; init; }
        public int RaceId { get; init; }
        public int? MissionLevel { get; init; }
        public int? MissionLevelOverride { get; init; }
        public int? KillEp { get; init; }
        public string? Note { get; init; }
    }
}
