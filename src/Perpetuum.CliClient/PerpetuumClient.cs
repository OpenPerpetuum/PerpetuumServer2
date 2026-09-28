using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Perpetuum.CliClient.Models;
using Perpetuum.Network;

namespace Perpetuum.CliClient
{
    public class PerpetuumClient : IDisposable
    {
        private PerpetuumClientConnection? _connection;
        private Dictionary<int, string>? _definitionNamesCache;
        private bool _isDisposed;

        public bool IsConnected => _connection != null;
        public WelcomeInfo? Welcome { get; private set; }
        public AccountInfo? Account { get; private set; }
        public CharacterSelectResult? SelectedCharacter { get; private set; }
        public IPEndPoint? RemoteEndPoint { get; private set; }

        public event Action<IMessage>? MessageReceived;
        public event Action<ChatMessage>? ChatMessageReceived;
        public event Action<string>? RawMessageReceived;
        public event Action? Disconnected;

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            // If already a 40-character hex string, assume it is already SHA-1 hashed
            if (password.Length == 40 && password.All(c => Uri.IsHexDigit(c)))
            {
                return password.ToUpperInvariant();
            }

            byte[] bytes = Encoding.ASCII.GetBytes(password);
            byte[] hash = SHA1.HashData(bytes);
            return Convert.ToHexString(hash).ToUpperInvariant();
        }

        public async Task<WelcomeInfo> ConnectAsync(string host, int port, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(15);
            var endPoint = await ResolveEndPointAsync(host, port, cancellationToken);
            RemoteEndPoint = endPoint;

            _connection = PerpetuumClientConnection.Connect(endPoint);
            _connection.MessageReceived += msg =>
            {
                if (msg.Command?.Text == Commands.ChannelNotification.Text && msg.Data != null)
                {
                    var chat = ChatMessage.FromNotification(msg.Data);
                    if (chat != null)
                    {
                        ChatMessageReceived?.Invoke(chat);
                    }
                }
                MessageReceived?.Invoke(msg);
            };
            _connection.Received += text => RawMessageReceived?.Invoke(text);
            _connection.Disconnected += conn => Disconnected?.Invoke();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);

            try
            {
                var welcomeMessage = await _connection.SendHandshakeAsync().WaitAsync(cts.Token);
                CheckResponseError(welcomeMessage);
                Welcome = WelcomeInfo.FromDictionary(welcomeMessage.Data);
                return Welcome;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Connection handshake timed out after {effectiveTimeout.TotalSeconds:F0}s connecting to {host}:{port}");
            }
        }

        public async Task<AccountInfo> SignInAsync(string email, string password, bool alreadyHashed = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var passwordHash = alreadyHashed ? password.ToUpperInvariant() : HashPassword(password);

            var data = new Dictionary<string, object>
            {
                { k.email, email },
                { k.password, passwordHash },
                { k.client, 0 },
                { k.hash, "CLI-" + Environment.MachineName },
                { k.language, 0 }
            };

            var request = new Message(Commands.SignIn, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            Account = AccountInfo.FromDictionary(response.Data);
            return Account;
        }

        public async Task<CharacterListResult> GetCharacterListAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var request = new Message(Commands.CharacterList, new Dictionary<string, object>());
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            return CharacterListResult.FromDictionary(response.Data);
        }

        public async Task<CharacterSelectResult> SelectCharacterAsync(int characterId, long baseEid = 0, string baseName = "", TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var data = new Dictionary<string, object>
            {
                { k.characterID, characterId },
                { k.language, 0 }
            };

            var request = new Message(Commands.CharacterSelect, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            SelectedCharacter = CharacterSelectResult.FromDictionary(response.Data);
            if (baseEid > 0 && SelectedCharacter.BaseEid == 0)
            {
                SelectedCharacter.BaseEid = baseEid;
            }
            if (!string.IsNullOrEmpty(baseName) && string.IsNullOrEmpty(SelectedCharacter.BaseName))
            {
                SelectedCharacter.BaseName = baseName;
            }

            return SelectedCharacter;
        }

        public async Task DeselectCharacterAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || SelectedCharacter == null)
            {
                return;
            }

            var request = new Message(Commands.CharacterDeselect, new Dictionary<string, object>());
            try
            {
                var response = await SendAsync(request, timeout ?? TimeSpan.FromSeconds(5), cancellationToken);
                CheckResponseError(response);
            }
            finally
            {
                SelectedCharacter = null;
            }
        }

        public async Task SignOutAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || Account == null)
            {
                return;
            }

            if (SelectedCharacter != null)
            {
                await DeselectCharacterAsync(timeout, cancellationToken);
            }

            var request = new Message(Commands.SignOut, new Dictionary<string, object>());
            try
            {
                var response = await SendAsync(request, timeout ?? TimeSpan.FromSeconds(5), cancellationToken);
                CheckResponseError(response);
            }
            finally
            {
                Account = null;
                SelectedCharacter = null;
            }
        }

        public async Task<IMessage> CreateAccountAsync(string email, string password, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var passwordHash = HashPassword(password);
            var cmd = Commands.GetCommandByText("accountOpenCreate") ?? new Command("accountOpenCreate") { AccessLevel = AccessLevel.notDefined };

            var data = new Dictionary<string, object>
            {
                { k.email, email },
                { k.password, passwordHash }
            };

            var request = new Message(cmd, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);
            return response;
        }

        public async Task<int> CreateCharacterAsync(string nick, int raceId = 1, int schoolId = 1, int majorId = 1, int sparkId = 1, Dictionary<string, object>? avatar = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var data = new Dictionary<string, object>
            {
                { k.nick, nick },
                { k.avatar, avatar ?? new Dictionary<string, object>() },
                { k.raceID, raceId },
                { k.schoolID, schoolId },
                { k.majorID, majorId },
                { k.sparkID, sparkId }
            };

            var request = new Message(Commands.CharacterCreate, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            if (response.Data != null && response.Data.TryGetValue(k.characterID, out var cidObj))
            {
                return Convert.ToInt32(cidObj);
            }

            return 0;
        }

        public async Task<Dictionary<int, string>> GetEntityDefaultsAsync(bool forceRefresh = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (_definitionNamesCache != null && !forceRefresh)
            {
                return _definitionNamesCache;
            }

            EnsureConnected();
            var request = new Message(Commands.GetEntityDefaults, new Dictionary<string, object>());
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            var map = new Dictionary<int, string>();
            if (response.Data != null)
            {
                foreach (var kvp in response.Data)
                {
                    if (kvp.Value is IDictionary<string, object> defDict)
                    {
                        var defId = defDict.GetInt(k.definition);
                        var defName = defDict.GetString(k.definitionName);
                        if (defId > 0 && !string.IsNullOrEmpty(defName))
                        {
                            map[defId] = defName;
                        }
                    }
                }
            }

            _definitionNamesCache = map;
            return _definitionNamesCache;
        }

        public async Task<StorageResult> GetStorageAsync(long? baseEid = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            long targetBaseEid = 0;

            if (baseEid.HasValue && baseEid.Value > 0)
            {
                targetBaseEid = baseEid.Value;
            }
            else if (SelectedCharacter != null && SelectedCharacter.BaseEid > 0)
            {
                targetBaseEid = SelectedCharacter.BaseEid;
            }

            if (targetBaseEid == 0)
            {
                throw new InvalidOperationException("No station base specified and current character is not docked at a known base.");
            }

            if (_definitionNamesCache == null)
            {
                try
                {
                    await GetEntityDefaultsAsync(timeout: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
                }
                catch
                {
                    // Proceed even if entity defaults lookup fails
                }
            }

            var data = new Dictionary<string, object>
            {
                { k.baseEID, targetBaseEid }
            };

            var request = new Message(Commands.BaseGetMyItems, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            var result = StorageResult.FromDictionary(response.Data, _definitionNamesCache);
            if (string.IsNullOrEmpty(result.BaseName) && SelectedCharacter != null && !string.IsNullOrEmpty(SelectedCharacter.BaseName))
            {
                result.BaseName = SelectedCharacter.BaseName;
            }

            return result;
        }

        public async Task<List<ChannelInfo>> GetChannelsAsync(bool myChannelsOnly = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var cmd = myChannelsOnly ? Commands.ChannelMyList : Commands.ChannelList;
            var request = new Message(cmd, new Dictionary<string, object>());
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);

            var list = new List<ChannelInfo>();
            if (response.Data != null)
            {
                foreach (var kvp in response.Data)
                {
                    if (kvp.Value is IDictionary<string, object> channelDict)
                    {
                        list.Add(ChannelInfo.FromDictionary(channelDict));
                    }
                }
            }

            return list;
        }

        public async Task JoinChannelAsync(string channelName, string password = "", TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var data = new Dictionary<string, object>
            {
                { k.channel, channelName },
                { k.password, password }
            };

            var request = new Message(Commands.ChannelJoin, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);
        }

        public async Task LeaveChannelAsync(string channelName, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var data = new Dictionary<string, object>
            {
                { k.channel, channelName }
            };

            var request = new Message(Commands.ChannelLeave, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);
        }

        public async Task SendChatMessageAsync(string channelName, string messageText, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var data = new Dictionary<string, object>
            {
                { k.channel, channelName },
                { k.message, messageText }
            };

            var request = new Message(Commands.ChannelTalk, data);
            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);
        }

        public async Task<IMessage> SendCommandAsync(string commandText, Dictionary<string, object>? parameters = null, string target = "", TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var cmd = Commands.GetCommandByText(commandText) ?? new Command(commandText);
            var request = new Message(cmd, parameters ?? new Dictionary<string, object>())
            {
                Sender = target
            };

            var response = await SendAsync(request, timeout, cancellationToken);
            CheckResponseError(response);
            return response;
        }

        public async Task<IMessage> SendAsync(IMessage clientMessage, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(15);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);

            try
            {
                var sendTask = _connection!.SendAsync(clientMessage);
                return await sendTask.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Command '{clientMessage.Command?.Text}' timed out after {effectiveTimeout.TotalSeconds:F0}s waiting for server reply");
            }
        }

        public static void CheckResponseError(IMessage response)
        {
            if (response.Data == null)
            {
                return;
            }

            if (response.Data.TryGetValue(k.rErr, out var errObj) && errObj != null)
            {
                var errorCode = (ErrorCodes)Convert.ToInt32(errObj);
                if (errorCode != ErrorCodes.NoError)
                {
                    IDictionary<string, object>? extra = null;
                    if (response.Data.TryGetValue(k.extra, out var extraObj) && extraObj is IDictionary<string, object> ed)
                    {
                        extra = ed;
                    }

                    throw new PerpetuumClientException(errorCode, extraData: extra != null ? new Dictionary<string, object>(extra) : null);
                }
            }
        }

        private void EnsureConnected()
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("Not connected to a Perpetuum server. Call ConnectAsync first.");
            }
        }

        private static async Task<IPEndPoint> ResolveEndPointAsync(string host, int port, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out var ip))
            {
                return new IPEndPoint(ip, port);
            }

            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            var v4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                     ?? addresses.FirstOrDefault();

            if (v4 == null)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            return new IPEndPoint(v4, port);
        }

        public void Disconnect()
        {
            if (_connection != null)
            {
                _connection.Disconnect();
                _connection.Dispose();
                _connection = null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
