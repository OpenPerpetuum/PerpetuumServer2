using Perpetuum.Builders;

namespace Perpetuum.Zones.Locking.Locks
{
    public class TerrainLockParametersPacketBuilder : IBuilder<Packet>
    {
        private readonly TerrainLock _terrainLock;

        public TerrainLockParametersPacketBuilder(TerrainLock terrainLock)
        {
            _terrainLock = terrainLock;
        }

        public Packet Build()
        {
            var packet = new Packet(ZoneCommand.TerrainLockParameters);

            packet.AppendLong(_terrainLock.Id);
            packet.AppendByte((byte)_terrainLock.TerraformType);
            packet.AppendByte((byte)_terrainLock.TerraformDirection);
            packet.AppendByte((byte)_terrainLock.Radius);
            packet.AppendByte((byte)_terrainLock.Falloff);

            return packet;
        }
    }
}
