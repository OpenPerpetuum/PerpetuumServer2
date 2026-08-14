using Perpetuum.AdminTool.NewItem;

namespace Perpetuum.AdminTool.NewRobot;

public interface INewRobotBuildModel : INewItemBuildModel
{
    BasicPanelViewModel HeadPanel { get; }
    StatsPanelViewModel HeadStatsPanel { get; }
    OptionsVisualPanelViewModel HeadOptionsPanel { get; }
    BasicPanelViewModel ChassisPanel { get; }
    StatsPanelViewModel ChassisStatsPanel { get; }
    OptionsVisualPanelViewModel ChassisOptionsPanel { get; }
    BasicPanelViewModel LegPanel { get; }
    StatsPanelViewModel LegStatsPanel { get; }
    OptionsVisualPanelViewModel LegOptionsPanel { get; }
    BasicPanelViewModel InventoryPanel { get; }
    StatsPanelViewModel InventoryStatsPanel { get; }
    OptionsVisualPanelViewModel InventoryOptionsPanel { get; }
    RobotTemplatePanelViewModel TemplatePanelViewModel { get; }
    RobotTemplateRelationPanelViewModel TemplateRelationPanelViewModel { get; }
    BonusesPanelViewModel BonusesPanel { get; }
}
