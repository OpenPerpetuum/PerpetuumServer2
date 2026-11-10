using System;
using System.Threading;
using Perpetuum.EntityFramework;
using Perpetuum.Zones.NpcSystem;
using Perpetuum.Zones.Scanning.Ammos;

namespace Perpetuum.Zones.Scanning.Scanners
{
    public partial class Scanner : IEntityVisitor<NpcScannerAmmo>
    {
        private static int _npcScanStrokeId;

        public void Visit(NpcScannerAmmo ammo)
        {
            var scanRange = ammo.GetScanRange(_player.Character);
            if (scanRange <= 0)
            {
                // Recheck at firing time for charges loaded before the skill requirement changed.
                throw new PerpetuumException(ErrorCodes.ExtensionLevelMismatchTerrain);
            }

            var origin = _player.CurrentPosition;
            Npc closest = null;
            var closestDistance = scanRange * scanRange;

            foreach (var unit in _zone.Units)
            {
                if (unit is not Npc npc || npc.States.Dead)
                {
                    continue;
                }

                var position = npc.CurrentPosition;
                var dx = position.X - origin.X;
                var dy = position.Y - origin.Y;
                var distance = dx * dx + dy * dy;
                if (distance < closestDistance || (distance == closestDistance && (closest == null || npc.Eid < closest.Eid)))
                {
                    closest = npc;
                    closestDistance = distance;
                }
            }

            if (closest == null)
            {
                return;
            }

            const byte radarPing = 0;
            const byte strokePoint = 1;
            const byte strokeEnd = 2;

            var target = closest.CurrentPosition;
            // The radar joins consecutive points with the same sender and stroke ID.
            var strokeId = unchecked((byte)Interlocked.Increment(ref _npcScanStrokeId));
            SendNpcScanDoodle(strokePoint, origin, strokeId, 0);
            SendNpcScanDoodle(strokeEnd, target, strokeId, 1);
            SendNpcScanDoodle(radarPing, target, 0, 0);
        }

        private void SendNpcScanDoodle(byte type, Position position, byte strokeId, ushort pointIndex)
        {
            // The client decodes GangDoodle coordinates as unsigned fixed-point values with five fractional bits.
            const int radarUnitsPerTile = 32;
            using var packet = new Packet(ZoneCommand.GangDoodle);
            packet.AppendLong(_player.Eid);
            packet.AppendByte(type);
            packet.AppendUShort((ushort)Math.Clamp(position.X * radarUnitsPerTile, 0, ushort.MaxValue));
            packet.AppendUShort((ushort)Math.Clamp(position.Y * radarUnitsPerTile, 0, ushort.MaxValue));
            packet.AppendByte(strokeId);
            packet.AppendUShort(pointIndex);
            _player.Session.SendPacket(packet);
        }
    }
}
