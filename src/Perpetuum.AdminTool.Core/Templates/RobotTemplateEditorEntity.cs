using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Templates
{
    /// <summary>
    /// Lookup record used by the structured robot-template editor. Carries the
    /// entitydefaults columns plus three values parsed from `options`:
    /// `moduleFlag` (filter modules into slots), `ammoCategoryFlags` (filter ammo
    /// into modules), and `slotFlags` (per-part slot list).
    /// </summary>
    public class RobotTemplateEditorEntity
    {
        public int Definition { get; init; }
        public string Name { get; init; } = "";
        public long CategoryFlags { get; init; }
        public long AttributeFlags { get; init; }

        /// <summary>Mirrors entitydefaults.enabled. Disabled rows are hidden from selectors.</summary>
        public bool Enabled { get; init; }

        /// <summary>options[moduleFlag] — int, may be 0 if absent.</summary>
        public long ModuleFlag { get; init; }

        /// <summary>
        /// options[ammoType] — a CategoryFlags value (stored as Genxy long, e.g. `L3120a`)
        /// rooting the subtree of ammo that fits the module. May be 0 if absent.
        /// </summary>
        public long AmmoType { get; init; }

        /// <summary>options[ammoCapacity] — int, may be 0.</summary>
        public int AmmoCapacity { get; init; }

        /// <summary>options[slotFlags] — int[], empty if not a robot component.</summary>
        public int[] SlotFlags { get; init; } = System.Array.Empty<int>();

        public string Display => $"{Definition} — {Name}";

        // Server-side `ActiveModule.IsAmmoable` checks options at runtime, but the
        // authoritative flag for "this module needs ammo" on the data side is the
        // `ammo_required` attribute flag (bit 18). Use that here so modules that
        // declare ammo support but don't expose it via options are still ammo-able
        // in the editor.
        public bool IsAmmoable =>
            new EntityAttributeFlags((ulong)AttributeFlags).HasFlag(Perpetuum.ExportedTypes.AttributeFlags.ammo_required);
    }
}
