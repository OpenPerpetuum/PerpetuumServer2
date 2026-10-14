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
            string discordAnswer = FormatResult(message.Result);
            var discordText = Limit($"Sparky, Syndicate Technician Chief: <@{message.CallerId}>, {discordAnswer}", _coordinator.MaxAnswerCharacters);
            _coordinator.LogDiagnosticAnswer(message.RequestId, message.Source, message.Result.Kind, discordAnswer);
            if (!_eventListenerFactory().TryQueueSparkyDiscordReply(message.RequestId, message.DiscordChannelId, message.CallerId, discordText))
            {
                Logger.Error($"[Sparky] Discord response queue is full for request #{message.RequestId:D6}; response was not sent.");
            }
            else
            {
                Logger.Info($"[Sparky] Discord response #{message.RequestId:D6} queued for delivery.");
            }

            return;
        }

        var caller = message.Caller;
        var sender = _coordinator.SenderCharacter;
        if (caller is null || sender is null)
        {
            Logger.Error($"[Sparky] In-game response #{message.RequestId:D6} could not be delivered because a character reference was unavailable.");
            return;
        }

        string answer = FormatResult(message.Result);
        if (_sessionManager.IsOnline(caller) && !string.IsNullOrWhiteSpace(message.OriginChannel))
        {
            var chatBody = Limit(
                $"Syndicate Technician Chief Sparky: {caller.Nick}, {answer}",
                _coordinator.MaxAnswerCharacters);
            if (_channelManager.GameOnlyAnnouncement(message.OriginChannel, sender, chatBody))
            {
                _coordinator.LogDiagnosticAnswer(message.RequestId, message.Source, message.Result.Kind, chatBody);
                Logger.Info($"[Sparky] Game response #{message.RequestId:D6} posted to its source channel.");
                return;
            }
        }

        var body = Limit($"Syndicate Technician Chief Sparky:\n{answer}", _coordinator.MaxAnswerCharacters);
        _coordinator.LogDiagnosticAnswer(message.RequestId, message.Source, message.Result.Kind, body);

        try
        {
            var result = MailHandler.SendMail(sender, caller, "Sparky response", body, MailType.character, out _, out _);
            if (result != ErrorCodes.NoError)
            {
                Logger.Error($"[Sparky] Offline response mail failed for request #{message.RequestId:D6} ({result}).");
            }
            else
            {
                Logger.Info($"[Sparky] Offline response for request #{message.RequestId:D6} mailed.");
            }
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky] Offline response mail failed for request #{message.RequestId:D6} ({exception.GetType().Name}).");
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
