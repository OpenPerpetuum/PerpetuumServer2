using Perpetuum.Builders;
using Perpetuum.Zones;

namespace Perpetuum.Players
{
    public class ErrorPacketBuilder : IBuilder<Packet>
    {
        private readonly ErrorCodes error;

        public ErrorPacketBuilder(ErrorCodes error)
        {
            this.error = error;
        }

        public Packet Build()
        {
            return new Packet(ZoneCommand.Error) { Error = error };
        }
    }
}
