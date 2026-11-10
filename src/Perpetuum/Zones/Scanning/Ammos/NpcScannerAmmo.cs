using System;
using System.Linq;
using Perpetuum.Accounting.Characters;
using Perpetuum.EntityFramework;
using Perpetuum.Items.Ammos;
using Perpetuum.Services.ExtensionService;

namespace Perpetuum.Zones.Scanning.Ammos
{
    public sealed class NpcScannerAmmo : Ammo
    {
        public const string DefinitionName = "def_ammo_npc_scan";
        private const string ExtensionName = "ext_npc_scanning";
        private const int TilesPerKilometer = 100;
        private readonly ExtensionInfo _extension;

        public NpcScannerAmmo(IExtensionReader extensionReader)
        {
            _extension = extensionReader.GetExtensionByName(ExtensionName);
        }

        public double GetScanRange(Character character)
        {
            if (_extension == null)
            {
                return 0;
            }

            var requiredLevel = ED.EnablerExtensions.Keys.FirstOrDefault(extension => extension.id == _extension.id).level;
            var level = Math.Clamp(character.GetExtensionLevel(_extension.id), 0, 10);
            return level >= Math.Max(1, requiredLevel) ? level * _extension.bonus * TilesPerKilometer : 0;
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }
    }
}
