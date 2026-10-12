using Perpetuum.Accounting.Characters;
using Perpetuum.Services.Sparky;

namespace Perpetuum.Services.EventServices.EventMessages;

internal sealed record SparkyResponseMessage(
    SparkySource Source,
    Character? Caller,
    string? OriginChannel,
    ulong DiscordChannelId,
    SparkyApiResult Result) : IEventMessage
{
    public EventType Type => EventType.SparkyResponse;
}
