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
        int languageId = result.LanguageId ?? 0;
        return result.Kind switch
        {
            SparkyResultKind.Answered => result.Answer ?? "I could not prepare a verified answer.",
            SparkyResultKind.NotFound => result.Answer ?? SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.MissingTerm),
            SparkyResultKind.Ambiguous => result.Candidates.Count == 0
                ? result.Answer ?? SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.AmbiguousItem)
                : result.Answer ?? SparkyResponseTemplates.Format(
                    languageId,
                    SparkyResponseTemplateKey.AmbiguousItem,
                    string.Join(", ", result.Candidates)),
            SparkyResultKind.Busy => result.Answer ?? SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.Busy),
            SparkyResultKind.Usage => SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.Usage),
            SparkyResultKind.QuestionTooLong => SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.QuestionTooLong),
            _ => SparkyResponseTemplates.Get(languageId, SparkyResponseTemplateKey.Unavailable)
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
