using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.NewRobot;

namespace Perpetuum.AdminTool.ViewModels;

// Stub — full implementation in Task 4
public partial class NewRobotDialogViewModel
{
    public BasicPanelViewModel BasicPanel { get; } = null!;
    public BasicPanelViewModel CalibrationPanel { get; } = null!;
    public BasicPanelViewModel PrototypePanel { get; } = null!;
    public StatsPanelViewModel StatsPanel { get; } = null!;
    public PropertyModifiersPanelViewModel PropertyModifiersPanel { get; } = null!;
    public ProductionPanelViewModel ProductionPanel { get; } = null!;
    public ResearchPanelViewModel ResearchPanel { get; } = null!;
    public OptionsVisualPanelViewModel OptionsVisualPanel { get; } = null!;
    public BasicPanelViewModel HeadPanel { get; } = null!;
    public StatsPanelViewModel HeadStatsPanel { get; } = null!;
    public BasicPanelViewModel ChassisPanel { get; } = null!;
    public StatsPanelViewModel ChassisStatsPanel { get; } = null!;
    public BasicPanelViewModel LegPanel { get; } = null!;
    public StatsPanelViewModel LegStatsPanel { get; } = null!;
    public BasicPanelViewModel InventoryPanel { get; } = null!;
    public StatsPanelViewModel InventoryStatsPanel { get; } = null!;
    public RobotTemplatePanelViewModel TemplatePanelViewModel { get; } = null!;
    public RobotTemplateRelationPanelViewModel TemplateRelationPanelViewModel { get; } = null!;
}
