using Perpetuum.Builders;
using Perpetuum.Common;
using Perpetuum.Zones;
using System.Linq;

namespace Perpetuum.Robots
{
    public class RobotInfoPacketBuilder : IBuilder<Packet>
    {
        private readonly Robot _robot;
        private readonly ZoneCommand _command;

        public RobotInfoPacketBuilder(Robot robot, ZoneCommand command = ZoneCommand.RobotInfo)
        {
            _robot = robot;
            _command = command;
        }

        public Packet Build()
        {
            var infoPacket = new Packet(_command);
            infoPacket.AppendLong(_robot.Eid);
            AppendRobotProperties(infoPacket);

            return infoPacket;
        }

        private void AppendRobotProperties(Packet infoPacket)
        {
            var properties = _robot.Properties.Where(p => p.HasValue).ToArray();

            infoPacket.AppendInt(properties.Length);

            foreach (var property in properties)
            {
                infoPacket.AppendProperty(property);
            }
        }
    }
}
