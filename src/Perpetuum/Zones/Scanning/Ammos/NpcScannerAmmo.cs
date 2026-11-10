using Perpetuum.EntityFramework;
using Perpetuum.Items.Ammos;

namespace Perpetuum.Zones.Scanning.Ammos
{
    public sealed class NpcScannerAmmo : Ammo
    {
        public const string DefinitionName = "def_ammo_npc_scan";

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }
    }
}
