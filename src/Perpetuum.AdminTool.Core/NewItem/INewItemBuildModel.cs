namespace Perpetuum.AdminTool.NewItem;

public interface INewItemBuildModel
{
    BasicPanelViewModel BasicPanel { get; }
    BasicPanelViewModel CalibrationPanel { get; }
    BasicPanelViewModel PrototypePanel { get; }
    StatsPanelViewModel StatsPanel { get; }
    PropertyModifiersPanelViewModel PropertyModifiersPanel { get; }
    ProductionPanelViewModel ProductionPanel { get; }
    ResearchPanelViewModel ResearchPanel { get; }
    OptionsVisualPanelViewModel OptionsVisualPanel { get; }
}
