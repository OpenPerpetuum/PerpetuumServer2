using Discord;
using Discord.WebSocket;
using Perpetuum.Log;
using Perpetuum.Services.EventServices.EventMessages;
using Perpetuum.Services.EventServices.EventProcessors;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Perpetuum.Services.Sparky;

namespace Perpetuum.Services.EventServices
{
    /// <summary>
    /// Listener Process that processes queue of EventMessages and notifies observers
    /// </summary>
    public class EventListenerService : Process
    {
        private readonly object _lock = new object();
        private readonly IDictionary<EventType, IList<IEventProcessor>> _observers;
        private readonly ConcurrentQueue<IEventMessage> _queue;
        private readonly DiscordSocketClient _client;

        //private readonly DiscordSocketClient _client;
        private readonly GlobalConfiguration _globalConfiguration;
        private readonly IDiscordPinStateRepository _pinStateRepository;
        private readonly Func<SparkyRequestCoordinator> _sparkyCoordinatorFactory;
        private readonly SparkyQuestionCaptureService _questionCapture;
        private Channel<SparkyDiscordReply>? _sparkyDiscordQueue;
        private CancellationTokenSource? _sparkyDiscordShutdown;
        private Task? _sparkyDiscordWorker;

        public EventListenerService(
            GlobalConfiguration globalConfiguration,
            IDiscordPinStateRepository pinStateRepository,
            Func<SparkyRequestCoordinator> sparkyCoordinatorFactory,
            SparkyQuestionCaptureService questionCapture)
        {
            _observers = new Dictionary<EventType, IList<IEventProcessor>>();
            _queue = new ConcurrentQueue<IEventMessage>();

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            });

            _globalConfiguration = globalConfiguration;
            _pinStateRepository = pinStateRepository;
            _sparkyCoordinatorFactory = sparkyCoordinatorFactory;
            _questionCapture = questionCapture;
        }

        /// <summary>
        /// Send a message where the type of EventMessage determines which listener is notified
        /// </summary>
        /// <param name="message">EventMessage of the type</param>
        public void PublishMessage(IEventMessage message)
        {
            if (message is DiscordIntegrationMessage discordMessage &&
                discordMessage.Type == EventType.PerpetuumToDiscord)
            {
                if (_client.GetChannel(discordMessage.ChannelDiscordId) is IMessageChannel discordChannel)
                {
                    string messageToSend = $"**<{discordMessage.Nick}>**: {discordMessage.Message}";
                    discordChannel.SendMessageAsync(
                        messageToSend,
                        allowedMentions: new AllowedMentions { AllowedTypes = AllowedMentionTypes.Users });
                }
            }
            else if (message is DiscordPinnableMessage pinnableMessage)
            {
                if (_client.GetChannel(pinnableMessage.DiscordChannelId) is IMessageChannel discordChannel)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            string messageToSend = $"**<{pinnableMessage.Nick}>**: {pinnableMessage.Message}";
                            var sent = await discordChannel.SendMessageAsync(
                                messageToSend,
                                allowedMentions: new AllowedMentions { AllowedTypes = AllowedMentionTypes.Users });

                            var existing = _pinStateRepository.Get(pinnableMessage.PinSlot);
                            if (existing.HasValue)
                            {
                                try
                                {
                                    var unpinChannel = _client.GetChannel(existing.Value.channelId) as IMessageChannel
                                                       ?? discordChannel;
                                    var oldMsg = await unpinChannel.GetMessageAsync(existing.Value.messageId);
                                    if (oldMsg is IUserMessage oldUserMsg)
                                        await oldUserMsg.UnpinAsync();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(ex.Message);
                                }
                            }

                            try { await sent.PinAsync(); }
                            catch (Exception ex)
                            {
                                Logger.Error(ex.Message);
                            }

                            _pinStateRepository.Upsert(
                                pinnableMessage.PinSlot,
                                pinnableMessage.DiscordChannelId,
                                sent.Id);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message);
                        }
                    });
                }
            }
            else
            {
                _queue.Enqueue(message);
            }
        }

        internal bool TryQueueSparkyDiscordReply(long requestId, Guid traceId, ulong channelId, ulong callerId, string message)
        {
            return _sparkyDiscordQueue?.Writer.TryWrite(new SparkyDiscordReply(requestId, traceId, channelId, callerId, message)) == true;
        }

        public void NotifyListeners(IEventMessage message)
        {
            lock (_lock)
            {
                if (_observers.TryGetValue(message.Type, out IList<IEventProcessor> list))
                {
                    foreach (IEventProcessor obs in list)
                    {
                        obs.OnNext(message);
                    }
                }
            }
        }

        /// <summary>
        /// Listeners are subscribed and instantiated in the Bootstrapper
        /// </summary>
        /// <param name="observer">Listener</param>
        public void AttachListener(IEventProcessor observer)
        {
            lock (_lock)
            {
                if (!_observers.ContainsKey(observer.Type))
                {
                    _observers.Add(observer.Type, new List<IEventProcessor>() { observer });
                }
                else
                {
                    _observers[observer.Type].Add(observer);
                }
            }
        }

        public override void Update(TimeSpan time)
        {
            TimeKeeper timer = new TimeKeeper(TimeSpan.FromSeconds(1));
            while (!_queue.IsEmpty && !timer.Expired)
            {
                if (_queue.TryDequeue(out IEventMessage message))
                {
                    NotifyListeners(message);
                }
            }
        }

        public override void Stop()
        {
            _sparkyDiscordQueue?.Writer.TryComplete();
            _sparkyDiscordShutdown?.Cancel();
            if (_sparkyDiscordWorker is not null)
            {
                try
                {
                    if (!_sparkyDiscordWorker.Wait(TimeSpan.FromSeconds(5)))
                    {
                        Logger.Error("[Sparky] Discord reply worker did not stop within the shutdown grace period.");
                    }
                }
                catch (AggregateException)
                {
                    Logger.Error("[Sparky] Discord reply worker failed during shutdown.");
                }
            }

            if (_sparkyDiscordWorker is null || _sparkyDiscordWorker.IsCompleted)
            {
                _sparkyDiscordShutdown?.Dispose();
                _sparkyDiscordShutdown = null;
                _sparkyDiscordWorker = null;
                _sparkyDiscordQueue = null;
            }

            Task.Run(async () =>
            {
                await _client.LogoutAsync();
                await _client.StopAsync();
            });

            _client.Log -= Log;
            _client.Connected -= OnDiscordConnected;
            _client.Disconnected -= OnDiscordDisconnected;
            _client.MessageReceived -= OnMessageReceived;

            base.Stop();
        }

        public override void Start()
        {
            base.Start();

            if (_globalConfiguration.EnableSparky &&
                (_sparkyDiscordWorker is null || _sparkyDiscordWorker.IsCompleted))
            {
                var capacity = Math.Clamp(_globalConfiguration.SparkyQueueCapacity, 1, 512);
                _sparkyDiscordQueue = Channel.CreateBounded<SparkyDiscordReply>(new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });
                _sparkyDiscordShutdown = new CancellationTokenSource();
                _sparkyDiscordWorker = ProcessSparkyDiscordRepliesAsync(
                    _sparkyDiscordQueue.Reader,
                    _sparkyDiscordShutdown.Token);
            }

            string discordBotToken = _globalConfiguration.DiscordBotToken;

            if (!string.IsNullOrEmpty(discordBotToken))
            {
                _client.Log += Log;
                _client.Connected += OnDiscordConnected;
                _client.Disconnected += OnDiscordDisconnected;
                _client.MessageReceived += OnMessageReceived;

                // Run the async logic in the background
                Task.Run(async () =>
                {
                    await _client.LoginAsync(TokenType.Bot, discordBotToken);
                    await _client.StartAsync();
                });
            }
        }

        private Task OnMessageReceived(SocketMessage message)
        {

            if (!message.Author.IsBot && !string.IsNullOrEmpty(message.CleanContent))
            {
                _questionCapture.TryCaptureDiscordMessage(
                    message.Channel.Id,
                    message.Channel.Name,
                    message.CleanContent,
                    message.Content);

                string nick = message.Author.GlobalName;

                PublishMessage(new DiscordIntegrationMessage(EventType.DiscordToPerpetuum, message.Channel.Id, nick, message.CleanContent));

                if (message is SocketUserMessage userMessage &&
                    _client.CurrentUser is { } botUser)
                {
                    bool resolvedBotMention = userMessage.MentionedUsers.Any(user => user.Id == botUser.Id);
                    bool rawBotMention = ContainsRawUserMention(userMessage.Content, botUser.Id);
                    bool actualBotMention = resolvedBotMention || rawBotMention;
                    bool textBotAddress = !actualBotMention && ContainsTextualBotAddress(userMessage.CleanContent, botUser);
                    bool addressesSparky = actualBotMention || textBotAddress;
                    if (actualBotMention)
                    {
                        Logger.Info($"[Sparky] Discord bot mention received (rawToken={rawBotMention}, resolvedMention={resolvedBotMention}).");
                    }
                    else if (textBotAddress)
                    {
                        Logger.Info("[Sparky] Discord text address received.");
                    }
                    else if (ContainsBotName(userMessage.CleanContent, botUser))
                    {
                        Logger.Info("[Sparky] Discord bot-name candidate was ignored because it did not contain a bot mention.");
                    }

                    if (addressesSparky)
                    {
                        string content = StripBotMention(userMessage.CleanContent, botUser);
                        bool accepted = _sparkyCoordinatorFactory().TrySubmitDiscordQuestion(
                            message.Channel.Id,
                            message.Author.Id,
                            content);
                        if (!accepted)
                        {
                            Logger.Warning("[Sparky] Discord address was received while the request adapter was disabled.");
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static bool ContainsRawUserMention(string content, ulong userId)
        {
            string userMention = $"<@{userId}>";
            string nicknameMention = $"<@!{userId}>";
            return content.Contains(userMention, StringComparison.Ordinal) ||
                   content.Contains(nicknameMention, StringComparison.Ordinal);
        }

        private static bool ContainsBotName(string content, SocketUser botUser)
        {
            return new[] { botUser.Username, botUser.GlobalName }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Any(name => content.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsTextualBotAddress(string content, SocketUser botUser)
        {
            string[] botNames = new[] { botUser.Username, botUser.GlobalName }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (botNames.Length == 0)
            {
                return false;
            }

            string namesPattern = string.Join("|", botNames.Select(Regex.Escape));
            string botAddress = $@"@(?:{namesPattern})(?:#\d{{4}})?";
            string openingAddress = $@"^\s*(?:hey(?:[\s,]+))?{botAddress}(?=$|[\s,!\?:.;])";
            string trailingAddress = $@",\s*{botAddress}[?!.,]?\s*$";
            const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            return Regex.IsMatch(content, openingAddress, options) ||
                   Regex.IsMatch(content, trailingAddress, options);
        }

        private static string StripBotMention(string content, SocketUser botUser)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string[] botNames = new[] { botUser.Username, botUser.GlobalName }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (botNames.Length > 0)
            {
                string namesPattern = string.Join("|", botNames.Select(Regex.Escape));
                string botMentionPattern = $@"@(?:{namesPattern})(?:#\d{{4}})?(?=$|[\s,!?\.:;])";
                content = Regex.Replace(
                    content,
                    botMentionPattern,
                    "\uE000",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            // CleanContent resolves normal mentions to display names. Remove any unresolved
            // mention tokens too, so raw Discord IDs cannot be forwarded to the Sparky API.
            content = Regex.Replace(content, "<@!?\\d+>|<@&\\d+>|<#\\d+>", " ");

            // A trailing mention is a vocative: remove its preceding comma and retain sentence
            // punctuation after the mention (for example, "..., right, @Sparky?").
            content = Regex.Replace(
                content,
                @"\s*,?\s*\uE000\s*([?!\.])?\s*$",
                "$1",
                RegexOptions.CultureInvariant);

            // For a mention inside or at the start of a sentence, remove its following separator.
            content = Regex.Replace(content, @"\uE000\s*[,;:]?\s*", string.Empty, RegexOptions.CultureInvariant);
            content = Regex.Replace(content, @"\s+([,;:.!?])", "$1").Trim();
            return content;
        }

        private async Task ProcessSparkyDiscordRepliesAsync(
            ChannelReader<SparkyDiscordReply> reader,
            CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var reply in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        if (_client.GetChannel(reply.ChannelId) is IMessageChannel discordChannel)
                        {
                            await discordChannel.SendMessageAsync(
                                reply.Message,
                                allowedMentions: new AllowedMentions { UserIds = [reply.CallerId] }).ConfigureAwait(false);
                            _sparkyCoordinatorFactory().RecordDelivery(reply.TraceId, SparkySource.Discord, "sent", "discord_sent");
                            Logger.Info($"[Sparky] Discord reply for request #{reply.RequestId:D6} sent.");
                        }
                        else
                        {
                            _sparkyCoordinatorFactory().RecordDelivery(reply.TraceId, SparkySource.Discord, "failed", "discord_channel_unavailable");
                            Logger.Warning($"[Sparky] Discord reply channel for request #{reply.RequestId:D6} was unavailable or not message-capable.");
                        }
                    }
                    catch (Exception exception)
                    {
                        _sparkyCoordinatorFactory().RecordDelivery(reply.TraceId, SparkySource.Discord, "failed", "discord_send_error");
                        Logger.Error($"[Sparky] Discord reply for request #{reply.RequestId:D6} could not be sent ({exception.GetType().Name}).");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during service shutdown.
            }
        }

        private sealed record SparkyDiscordReply(long RequestId, Guid TraceId, ulong ChannelId, ulong CallerId, string Message);

        private Task Log(LogMessage msg)
        {
            string exceptionType = msg.Exception is null ? string.Empty : $" ({msg.Exception.GetType().Name})";

            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Logger.Error($"[Discord] {msg.Severity} from {msg.Source}{exceptionType}.");
                    break;
                case LogSeverity.Warning:
                    Logger.Warning($"[Discord] {msg.Severity} from {msg.Source}{exceptionType}.");
                    break;
            }

            return Task.CompletedTask;
        }

        private Task OnDiscordConnected()
        {
            Logger.Info("[Discord] Gateway connected.");
            return Task.CompletedTask;
        }

        private Task OnDiscordDisconnected(Exception exception)
        {
            Logger.Warning($"[Discord] Gateway disconnected ({exception.GetType().Name}).");
            return Task.CompletedTask;
        }
    }
}
