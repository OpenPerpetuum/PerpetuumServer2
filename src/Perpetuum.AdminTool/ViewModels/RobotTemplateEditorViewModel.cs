using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Templates;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class RobotTemplateEditorViewModel : ObservableObject
    {
        // Top-level keys we own. Anything else found in the input dictionary is preserved
        // verbatim so we don't accidentally drop server-side fields (e.g. cargo `items`).
        private static readonly HashSet<string> _ownedKeys = new(StringComparer.Ordinal)
        {
            "robot", "head", "chassis", "leg", "container",
            "headModules", "chassisModules", "legModules"
        };

        private readonly IReadOnlyList<RobotTemplateEditorEntity> _all;
        private readonly Dictionary<int, RobotTemplateEditorEntity> _byDef;

        // Keys we don't model — round-tripped on save.
        private readonly Dictionary<string, object> _passthrough = new(StringComparer.Ordinal);

        // Saved module data captured per part on initial load. When a part definition
        // changes, slots are rebuilt; the user's prior module/ammo picks are mapped onto
        // the new slot list by index where possible.
        private List<ModuleData> _initialHeadModules = new();
        private List<ModuleData> _initialChassisModules = new();
        private List<ModuleData> _initialLegModules = new();

        // CategoryFlags roots used to filter each part dropdown. Hierarchical match —
        // any descendant of the root is accepted.
        private const long CfRobots = 0x0000000000000001L;
        private const long CfRobotHead = 0x0000000000000150L;
        private const long CfRobotChassis = 0x0000000000000250L;
        private const long CfRobotLeg = 0x0000000000000350L;
        private const long CfRobotInventory = 0x0000000000030915L;

        public ObservableCollection<RobotTemplateEditorEntity> RobotPicks { get; } = new();
        public ObservableCollection<RobotTemplateEditorEntity> HeadPicks { get; } = new();
        public ObservableCollection<RobotTemplateEditorEntity> ChassisPicks { get; } = new();
        public ObservableCollection<RobotTemplateEditorEntity> LegPicks { get; } = new();
        public ObservableCollection<RobotTemplateEditorEntity> ContainerPicks { get; } = new();

        public ObservableCollection<RobotTemplateSlotViewModel> HeadSlots { get; } = new();
        public ObservableCollection<RobotTemplateSlotViewModel> ChassisSlots { get; } = new();
        public ObservableCollection<RobotTemplateSlotViewModel> LegSlots { get; } = new();

        [ObservableProperty] private int _robotDefinition;
        [ObservableProperty] private int _headDefinition;
        [ObservableProperty] private int _chassisDefinition;
        [ObservableProperty] private int _legDefinition;
        [ObservableProperty] private int _containerDefinition;

        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _initialGenxy = "";
        [ObservableProperty] private string _resultGenxy = "";

        public RobotTemplateEditorViewModel(
            IReadOnlyList<RobotTemplateEditorEntity> allEntities,
            string initialDescription)
        {
            _all = allEntities;
            _byDef = allEntities.ToDictionary(e => e.Definition);

            // Each part dropdown is filtered by Enabled + a category-tree match against
            // the relevant CategoryFlags root. Disabled rows still live in `_byDef` so
            // existing references resolve their display name; they just can't be picked.
            PopulatePicks(RobotPicks, CfRobots);
            PopulatePicks(HeadPicks, CfRobotHead);
            PopulatePicks(ChassisPicks, CfRobotChassis);
            PopulatePicks(LegPicks, CfRobotLeg);
            PopulatePicks(ContainerPicks, CfRobotInventory);

            InitialGenxy = initialDescription ?? "";

            LoadFromGenxy(InitialGenxy);
        }

        private void PopulatePicks(ObservableCollection<RobotTemplateEditorEntity> sink, long categoryRoot)
        {
            var mask = CategoryFlagsMask(categoryRoot);
            foreach (var e in _all)
            {
                if (!e.Enabled) continue;
                if (e.CategoryFlags == 0) continue;
                if ((e.CategoryFlags & mask) != categoryRoot) continue;
                sink.Add(e);
            }
        }

        // Mirror of Perpetuum.CategoryFlagsExtensions.GetCategoryFlagsMask on long.
        private static long CategoryFlagsMask(long target)
        {
            var mask = unchecked((long)0xFFFFFFFFFFFFFFFFUL);
            while (((ulong)target & (ulong)mask) > 0)
            {
                mask <<= 8;
            }
            return ~mask;
        }

        private void LoadFromGenxy(string genxy)
        {
            // Empty string → fresh template, all defs zero, all slot lists empty.
            var dict = string.IsNullOrEmpty(genxy)
                ? new Dictionary<string, object>()
                : GenxyConverter.Deserialize(genxy);

            RobotDefinition = ToInt(dict.GetValueOrDefault("robot"));
            HeadDefinition = ToInt(dict.GetValueOrDefault("head"));
            ChassisDefinition = ToInt(dict.GetValueOrDefault("chassis"));
            LegDefinition = ToInt(dict.GetValueOrDefault("leg"));
            ContainerDefinition = ToInt(dict.GetValueOrDefault("container"));

            _initialHeadModules = ReadModules(dict.GetValueOrDefault("headModules"));
            _initialChassisModules = ReadModules(dict.GetValueOrDefault("chassisModules"));
            _initialLegModules = ReadModules(dict.GetValueOrDefault("legModules"));

            // Round-trip everything else (e.g. `items`) back into the saved Genxy.
            foreach (var kv in dict)
            {
                if (_ownedKeys.Contains(kv.Key)) continue;
                _passthrough[kv.Key] = kv.Value;
            }

            RebuildSlots(HeadSlots, HeadDefinition, _initialHeadModules);
            RebuildSlots(ChassisSlots, ChassisDefinition, _initialChassisModules);
            RebuildSlots(LegSlots, LegDefinition, _initialLegModules);
        }

        partial void OnHeadDefinitionChanged(int value) =>
            RebuildSlots(HeadSlots, value, _initialHeadModules);

        partial void OnChassisDefinitionChanged(int value) =>
            RebuildSlots(ChassisSlots, value, _initialChassisModules);

        partial void OnLegDefinitionChanged(int value) =>
            RebuildSlots(LegSlots, value, _initialLegModules);

        private void RebuildSlots(
            ObservableCollection<RobotTemplateSlotViewModel> sink,
            int partDefinition,
            List<ModuleData> seedModules)
        {
            sink.Clear();
            if (partDefinition <= 0 || !_byDef.TryGetValue(partDefinition, out var part)) return;

            for (var i = 0; i < part.SlotFlags.Length; i++)
            {
                var slotIndex = i + 1; // 1-based
                var slot = new RobotTemplateSlotViewModel(_byDef, _all)
                {
                    SlotIndex = slotIndex,
                    SlotFlag = (uint)part.SlotFlags[i] // treat as unsigned bitmask
                };
                slot.PopulateModulePicks(_all);

                // Pre-fill from seed modules: pick the seeded module whose slot index
                // matches this slot. A user might have switched parts whose slot count
                // differs — in that case slots beyond the seed are simply empty.
                var seed = seedModules.FirstOrDefault(m => m.Slot == slotIndex);
                if (seed != null)
                {
                    slot.ModuleDefinition = seed.Definition;
                    slot.AmmoDefinition = seed.AmmoDefinition;
                    slot.AmmoQuantity = seed.AmmoQuantity;
                }

                sink.Add(slot);
            }
        }

        public bool TrySerialize(out string error)
        {
            error = "";
            if (RobotDefinition <= 0) { error = "Robot definition is required."; return false; }
            if (HeadDefinition <= 0) { error = "Head definition is required."; return false; }
            if (ChassisDefinition <= 0) { error = "Chassis definition is required."; return false; }
            if (LegDefinition <= 0) { error = "Leg definition is required."; return false; }
            if (ContainerDefinition <= 0) { error = "Container definition is required."; return false; }

            var output = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["robot"] = RobotDefinition,
                ["head"] = HeadDefinition,
                ["chassis"] = ChassisDefinition,
                ["leg"] = LegDefinition,
                ["container"] = ContainerDefinition,
                ["headModules"] = SerializeSlots(HeadSlots),
                ["chassisModules"] = SerializeSlots(ChassisSlots),
                ["legModules"] = SerializeSlots(LegSlots)
            };

            // Preserve unknown keys (cargo `items`, anything else).
            foreach (var kv in _passthrough)
            {
                if (output.ContainsKey(kv.Key)) continue;
                output[kv.Key] = kv.Value;
            }

            ResultGenxy = GenxyConverter.Serialize(output);
            return true;
        }

        private static Dictionary<string, object> SerializeSlots(IEnumerable<RobotTemplateSlotViewModel> slots)
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            var idx = 0;
            foreach (var slot in slots)
            {
                if (slot.ModuleDefinition <= 0) continue; // skip empty slots

                var inner = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["definition"] = slot.ModuleDefinition,
                    ["slot"] = slot.SlotIndex
                };

                if (slot.AmmoDefinition > 0)
                {
                    inner["ammoDefinition"] = slot.AmmoDefinition;
                    inner["ammoQuantity"] = slot.AmmoQuantity;
                }

                dict[$"m{idx++}"] = inner;
            }
            return dict;
        }

        private static List<ModuleData> ReadModules(object? raw)
        {
            var result = new List<ModuleData>();
            if (raw is not IDictionary<string, object> outer) return result;

            foreach (var kv in outer)
            {
                if (kv.Value is not IDictionary<string, object> m) continue;
                result.Add(new ModuleData
                {
                    Definition = ToInt(GetOrDefault(m, "definition")),
                    Slot = ToInt(GetOrDefault(m, "slot")),
                    AmmoDefinition = ToInt(GetOrDefault(m, "ammoDefinition")),
                    AmmoQuantity = ToInt(GetOrDefault(m, "ammoQuantity"))
                });
            }
            return result;
        }

        private static object? GetOrDefault(IDictionary<string, object> dict, string key)
            => dict.TryGetValue(key, out var v) ? v : null;

        private static int ToInt(object? v) => v switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            _ => 0
        };

        private class ModuleData
        {
            public int Definition { get; init; }
            public int Slot { get; init; }
            public int AmmoDefinition { get; init; }
            public int AmmoQuantity { get; init; }
        }
    }
}
