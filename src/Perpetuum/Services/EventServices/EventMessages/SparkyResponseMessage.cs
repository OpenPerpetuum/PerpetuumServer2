using Perpetuum.Accounting.Characters;
using Perpetuum.Services.Sparky;

namespace Perpetuum.Services.EventServices.EventMessages;

internal sealed record SparkyResponseMessage(
    long RequestId,
    Guid TraceId,
    SparkySource Source,
    Character? Caller,
    string? OriginChannel,
    ulong DiscordChannelId,
    ulong CallerId,
    SparkyApiResult Result) : IEventMessage
{
    public EventType Type => EventType.SparkyResponse;
}
