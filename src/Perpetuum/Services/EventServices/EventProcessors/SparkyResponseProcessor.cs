using Perpetuum.Accounting.Characters;
using Perpetuum.Log;
using Perpetuum.Services.Channels;
using Perpetuum.Services.EventServices.EventMessages;
using Perpetuum.Services.Mail;
using Perpetuum.Services.Sessions;
using Perpetuum.Services.Sparky;

namespace Perpetuum.Services.EventServices.EventProcessors;

public sealed class SparkyResponseProcessor : EventProcessor
{
    private const int MessageLimit = 2000;
    private readonly IChannelManager _channelManager;
    private readonly ISessionManager _sessionManager;
    private readonly SparkyRequestCoordinator _coordinator;
    private readonly Func<EventListenerService> _eventListenerFactory;

    public SparkyResponseProcessor(
        IChannelManager channelManager,
        ISessionManager sessionManager,
        SparkyRequestCoordinator coordinator,
        Func<EventListenerService> eventListenerFactory)
    {
        _channelManager = channelManager;
        _sessionManager = sessionManager;
        _coordinator = coordinator;
        _eventListenerFactory = eventListenerFactory;
    }

    public override EventType Type => EventType.SparkyResponse;

    public override void HandleMessage(IEventMessage value)
    {
        if (value is not SparkyResponseMessage message)
        {
            return;
        }

        if (message.Source == SparkySource.Discord)
        {
            var discordText = Limit($"**Sparky, Syndicate Technician Chief**: {FormatResult(message.Result)}", _coordinator.MaxAnswerCharacters);
            if (!_eventListenerFactory().TryQueueSparkyDiscordReply(message.DiscordChannelId, discordText))
            {
                Logger.Error("[Sparky] Discord response queue is full; response was not sent.");
            }

            return;
        }

        var caller = message.Caller;
        var sender = _coordinator.SenderCharacter;
        if (caller is null || sender is null)
        {
            Logger.Error("[Sparky] In-game response could not be delivered because a character reference was unavailable.");
            return;
        }

        var body = Limit($"Syndicate Technician Chief Sparky:\n{FormatResult(message.Result)}", _coordinator.MaxAnswerCharacters);
        if (_sessionManager.IsOnline(caller) &&
            !string.IsNullOrWhiteSpace(message.OriginChannel) &&
            _channelManager.GameOnlyAnnouncement(message.OriginChannel, sender, body))
        {
            return;
        }

        try
        {
            var result = MailHandler.SendMail(sender, caller, "Sparky response", body, MailType.character, out _, out _);
            if (result != ErrorCodes.NoError)
            {
                Logger.Error($"[Sparky] Offline response mail failed ({result}).");
            }
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky] Offline response mail failed ({exception.GetType().Name}).");
        }
    }

    private static string FormatResult(SparkyApiResult result)
    {
        return result.Kind switch
        {
            SparkyResultKind.Answered => result.Answer ?? "I could not prepare a verified answer.",
            SparkyResultKind.NotFound => "I couldn't identify an item in the server's verified item data. Name the item, and I'll check its stats or recipe.",
            SparkyResultKind.Ambiguous => result.Candidates.Count == 0
                ? "I found more than one possible item. Please include its full translated name."
                : $"I found several possible items: {string.Join(", ", result.Candidates)}. Please include the full item name.",
            SparkyResultKind.Busy => result.Answer ?? "I'm handling several requests at the moment. Please try again shortly.",
            SparkyResultKind.Usage => "Ask me about an item, for example: Sparky, what are the stats for Standard medium HCL laser?",
            SparkyResultKind.QuestionTooLong => "Please shorten your question and ask again.",
            _ => "My local data link is unavailable right now. Please try again later."
        };
    }

    private static string Limit(string text, int configuredLimit)
    {
        var limit = Math.Clamp(configuredLimit, 1, MessageLimit);
        if (text.Length <= limit)
        {
            return text;
        }

        return limit == 1 ? "…" : text[..(limit - 1)] + "…";
    }
}
