// Stub — replaced in Task 11
using Perpetuum.AdminTool.NewItem;
namespace Perpetuum.AdminTool.ViewModels;

public class NewItemDialogViewModel
{
    public BasicPanelViewModel BasicPanel { get; } = new(BasicPanelMode.Main, []);
    public BasicPanelViewModel CalibrationPanel { get; } = new(BasicPanelMode.CalibrationTemplate, []);
    public BasicPanelViewModel PrototypePanel { get; } = new(BasicPanelMode.Prototype, []);
    public StatsPanelViewModel StatsPanel { get; } = new();
    public PropertyModifiersPanelViewModel PropertyModifiersPanel { get; } = new();
    public ProductionPanelViewModel ProductionPanel { get; } = new();
    public ResearchPanelViewModel ResearchPanel { get; } = new();
    public OptionsVisualPanelViewModel OptionsVisualPanel { get; } = new();
    public bool IsCraftable => BasicPanel.IsCraftable;
    public bool HasPrototype => BasicPanel.HasPrototype;
}
