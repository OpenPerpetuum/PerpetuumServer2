using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.ViewModels
{
    /// <summary>
    /// One slot in a robot component. Holds the chosen module + (optional) ammo,
    /// plus the pre-filtered candidate lists driven by `SlotFlag` and the chosen
    /// module's `ammoCategoryFlags`.
    /// </summary>
    public partial class RobotTemplateSlotViewModel : ObservableObject
    {
        private readonly IReadOnlyDictionary<int, RobotTemplateEditorEntity> _byDef;

        public int SlotIndex { get; init; } // 1-based

        /// <summary>
        /// The slotFlags entry for this slot (a bitmask of `SlotFlags` enum values).
        /// Used to filter modules: a module fits if `(module.ModuleFlag &amp; SlotFlag) == module.ModuleFlag`.
        /// </summary>
        public long SlotFlag { get; init; }

        public string SlotFlagHex => "0x" + SlotFlag.ToString("X");

        [ObservableProperty] private int _moduleDefinition;
        [ObservableProperty] private int _ammoDefinition;
        [ObservableProperty] private int _ammoQuantity;
        [ObservableProperty] private bool _isAmmoable;

        public ObservableCollection<RobotTemplateEditorEntity> ModulePicks { get; } = new();
        public ObservableCollection<RobotTemplateEditorEntity> AmmoPicks { get; } = new();

        // Specialized slots only accept specialized modules.
        // SlotFlags.specialized = 11 → bit value 1 << 11 = 0x800.
        private const long SpecializedFlag = 1L << 11;

        public RobotTemplateSlotViewModel(
            IReadOnlyDictionary<int, RobotTemplateEditorEntity> byDef,
            IEnumerable<RobotTemplateEditorEntity> allEntities)
        {
            _byDef = byDef;

            // Filter rule mirrors Perpetuum/Robots/RobotComponent.IsValidSlotTo:
            //   (moduleFlag & slotFlag) == moduleFlag
            //   AND (slot is non-specialized OR module is specialized)
            // We don't apply it here yet because SlotFlag isn't set in the ctor —
            // the ItemsSource binding seed runs after the property setter, so we
            // populate ModulePicks from PopulateModulePicks() once after ctor.
        }

        public void PopulateModulePicks(IEnumerable<RobotTemplateEditorEntity> allEntities)
        {
            var slotIsSpecialized = (SlotFlag & SpecializedFlag) == SpecializedFlag;

            ModulePicks.Clear();
            foreach (var e in allEntities)
            {
                if (!e.Enabled) continue;
                if (e.ModuleFlag == 0) continue; // not a module
                if ((e.ModuleFlag & SlotFlag) != e.ModuleFlag) continue;
                if (slotIsSpecialized && (e.ModuleFlag & SpecializedFlag) != SpecializedFlag) continue;
                ModulePicks.Add(e);
            }
        }

        partial void OnModuleDefinitionChanged(int value)
        {
            RebuildAmmoPicks();
        }

        private void RebuildAmmoPicks()
        {
            AmmoPicks.Clear();
            IsAmmoable = false;

            if (ModuleDefinition <= 0 ||
                !_byDef.TryGetValue(ModuleDefinition, out var module) ||
                !module.IsAmmoable)
            {
                if (AmmoDefinition != 0) AmmoDefinition = 0;
                if (AmmoQuantity != 0) AmmoQuantity = 0;
                return;
            }

            IsAmmoable = true;

            // Hierarchical filter: include candidates whose categoryflags is the
            // module's options[ammoType] or any descendant in the categoryflags tree.
            // Mirrors Perpetuum.CategoryFlagsExtensions.IsCategory:
            //     mask = bytes up through highest non-zero byte of `target`
            //     accept if (source & mask) == target
            // (We re-implement the math here instead of calling into the server's
            // extension — its static ctor binds to the Db facade we don't initialize.)
            var target = module.AmmoType;
            if (target == 0) return;

            var mask = CategoryFlagsMask(target);
            foreach (var e in _byDef.Values.OrderBy(e => e.Name, System.StringComparer.OrdinalIgnoreCase))
            {
                if (!e.Enabled) continue;
                if (e.CategoryFlags == 0) continue;
                if ((e.CategoryFlags & mask) != target) continue;
                AmmoPicks.Add(e);
            }

            // If the previously selected ammo no longer fits, clear it.
            if (AmmoDefinition != 0 && !AmmoPicks.Any(a => a.Definition == AmmoDefinition))
            {
                AmmoDefinition = 0;
                AmmoQuantity = 0;
            }
        }

        // Mirror of Perpetuum.CategoryFlagsExtensions.GetCategoryFlagsMask, adapted
        // to operate on long. Keeps shifting an all-ones mask up by 8 bits until the
        // target's bits all sit below it, then returns the inverted mask — i.e. an
        // all-ones value covering the bytes from the lowest up through the highest
        // non-zero byte of `target`.
        private static long CategoryFlagsMask(long target)
        {
            var mask = unchecked((long)0xFFFFFFFFFFFFFFFFUL);
            while (((ulong)target & (ulong)mask) > 0)
            {
                mask <<= 8;
            }
            return ~mask;
        }
    }
}
