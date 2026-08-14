using System.Collections.Generic;

namespace Perpetuum.AdminTool.NewItem;

public class CloneExtendedData
{
    public IReadOnlyList<(int ComponentDef, int Amount)> Components { get; init; } = [];
    public (int ResearchLevel, int? CalibrationProgram, bool Enabled)? ResearchLevel { get; init; }
    public IReadOnlyList<(int ParentDef, int GroupId, int X, int Y, int? EnablerExtId)> TechTree { get; init; } = [];
    public IReadOnlyList<(int PointTypeId, int Amount)> ResearchCosts { get; init; } = [];
    public IReadOnlyList<(int ExtensionId, int Level)> EnablerExtensions { get; init; } = [];
    public IReadOnlyDictionary<string, string?> DefinitionConfig { get; init; } = new Dictionary<string, string?>();
}
