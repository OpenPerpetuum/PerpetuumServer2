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
        private Channel<SparkyDiscordReply>? _sparkyDiscordQueue;
        private CancellationTokenSource? _sparkyDiscordShutdown;
        private Task? _sparkyDiscordWorker;

        public EventListenerService(
            GlobalConfiguration globalConfiguration,
            IDiscordPinStateRepository pinStateRepository,
            Func<SparkyRequestCoordinator> sparkyCoordinatorFactory)
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

        internal bool TryQueueSparkyDiscordReply(ulong channelId, string message)
        {
            return _sparkyDiscordQueue?.Writer.TryWrite(new SparkyDiscordReply(channelId, message)) == true;
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
                string nick = message.Author.GlobalName;

                PublishMessage(new DiscordIntegrationMessage(EventType.DiscordToPerpetuum, message.Channel.Id, nick, message.CleanContent));

                if (message is SocketUserMessage userMessage &&
                    _client.CurrentUser is { } botUser &&
                    userMessage.MentionedUsers.Any(user => user.Id == botUser.Id))
                {
                    string content = StripBotMention(userMessage.CleanContent, botUser);

                    _sparkyCoordinatorFactory().TrySubmitDiscordQuestion(
                        message.Channel.Id,
                        message.Author.Id,
                        content);
                }
            }

            return Task.CompletedTask;
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
                string botMentionPattern = $@"@(?:{namesPattern})(?=$|[\s,!?\.:;])";
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
                                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
                        }
                    }
                    catch (Exception)
                    {
                        Logger.Error("[Sparky] Discord reply could not be sent.");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during service shutdown.
            }
        }

        private sealed record SparkyDiscordReply(ulong ChannelId, string Message);

        private Task Log(LogMessage msg)
        {
            // There has to be logging logic but meh.

            return Task.CompletedTask;
        }
    }
}
