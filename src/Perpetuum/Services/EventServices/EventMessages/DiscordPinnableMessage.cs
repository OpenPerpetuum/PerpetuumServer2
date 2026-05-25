namespace Perpetuum.Services.EventServices.EventMessages
{
    public class DiscordPinnableMessage : IEventMessage
    {
        public EventType Type => EventType.PerpetuumToDiscord;
        public ulong DiscordChannelId { get; }
        public string Nick { get; }
        public string Message { get; }
        public PinSlot PinSlot { get; }

        public DiscordPinnableMessage(ulong discordChannelId, string nick, string message, PinSlot pinSlot)
        {
            DiscordChannelId = discordChannelId;
            Nick = nick;
            Message = message;
            PinSlot = pinSlot;
        }
    }
}
