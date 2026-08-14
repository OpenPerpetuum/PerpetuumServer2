using Perpetuum.AdminTool.Templates;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Core.Tests.Templates;

public sealed class RobotTemplateEditorViewModelTests
{
    [Fact]
    public void PartSlots_FilterModulesAndCompatibleAmmo()
    {
        const long ammoCategory = 0x3120a;
        RobotTemplateEditorEntity head = Entity(2, "head", 0x150, slotFlags: [1]);
        RobotTemplateEditorEntity module = Entity(
            10,
            "weapon",
            categoryFlags: 0x99,
            moduleFlag: 1,
            ammoType: ammoCategory,
            attributeFlags: 1L << 18);
        RobotTemplateEditorEntity ammo = Entity(20, "ammo", ammoCategory);
        var editor = new RobotTemplateEditorViewModel(
        [
            Entity(1, "robot", 0x1),
            head,
            Entity(3, "chassis", 0x250),
            Entity(4, "leg", 0x350),
            Entity(5, "container", 0x30915),
            module,
            ammo
        ], string.Empty);

        editor.HeadDefinition = head.Definition;
        RobotTemplateSlotViewModel slot = Assert.Single(editor.HeadSlots);
        Assert.Contains(slot.ModulePicks, item => item.Definition == module.Definition);

        slot.ModuleDefinition = module.Definition;

        Assert.True(slot.IsAmmoable);
        Assert.Contains(slot.AmmoPicks, item => item.Definition == ammo.Definition);
    }

    [Fact]
    public void Serialize_RequiresAllPartsAndProducesGenxy()
    {
        var editor = new RobotTemplateEditorViewModel(
        [
            Entity(1, "robot", 0x1),
            Entity(2, "head", 0x150),
            Entity(3, "chassis", 0x250),
            Entity(4, "leg", 0x350),
            Entity(5, "container", 0x30915)
        ], string.Empty)
        {
            RobotDefinition = 1,
            HeadDefinition = 2,
            ChassisDefinition = 3,
            LegDefinition = 4,
            ContainerDefinition = 5
        };

        bool success = editor.TrySerialize(out string error);

        Assert.True(success, error);
        Assert.Contains("#robot=i1", editor.ResultGenxy);
        Assert.Contains("#container=i5", editor.ResultGenxy);
    }

    private static RobotTemplateEditorEntity Entity(
        int definition,
        string name,
        long categoryFlags,
        long moduleFlag = 0,
        long ammoType = 0,
        long attributeFlags = 0,
        int[]? slotFlags = null)
    {
        return new RobotTemplateEditorEntity
        {
            Definition = definition,
            Name = name,
            CategoryFlags = categoryFlags,
            ModuleFlag = moduleFlag,
            AmmoType = ammoType,
            AttributeFlags = attributeFlags,
            SlotFlags = slotFlags ?? [],
            Enabled = true
        };
    }
}
