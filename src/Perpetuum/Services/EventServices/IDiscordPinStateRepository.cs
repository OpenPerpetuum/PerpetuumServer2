using Perpetuum.Services.EventServices.EventMessages;

namespace Perpetuum.Services.EventServices
{
    public interface IDiscordPinStateRepository
    {
        (ulong channelId, ulong messageId)? Get(PinSlot slot);
        void Upsert(PinSlot slot, ulong channelId, ulong messageId);
    }
}
