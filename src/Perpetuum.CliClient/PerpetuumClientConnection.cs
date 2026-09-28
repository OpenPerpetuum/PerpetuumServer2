using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Perpetuum.GenXY;
using Perpetuum.Network;

namespace Perpetuum.CliClient
{
    public class PerpetuumClientConnection : EncryptedTcpConnection
    {
        private readonly Rc4 _rc4;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource<IMessage>>> _commandQueue =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action<IMessage>? MessageReceived;
        public new event Action<string>? Received;

        public PerpetuumClientConnection(Socket socket) : base(socket)
        {
            _rc4 = new Rc4(FastRandom.NextBytes(40));
            ObjectHelper.Swap(ref inIncrement, ref outIncrement);
            ObjectHelper.Swap(ref inDecodingByte, ref outEncodingByte);
        }

        public static PerpetuumClientConnection Connect(IPEndPoint endPoint)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            socket.Connect(endPoint);
            var connection = new PerpetuumClientConnection(socket);
            connection.Receive();
            return connection;
        }

        public Task<IMessage> SendHandshakeAsync()
        {
            byte[]? encryptedKey = Rsa.Encrypt(_rc4.streamKey);
            if (encryptedKey == null)
            {
                throw new InvalidOperationException("Failed to encrypt RSA handshake stream key.");
            }

            byte[] data = new byte[encryptedKey.Length + 4];
            Array.Copy(encryptedKey, 0, data, 4, encryptedKey.Length);

            var source = new TaskCompletionSource<IMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueCompletionSource(Commands.Welcome.Text, source);

            // Bypass RC4 for RSA handshake, rolling XOR cipher is still applied by base.Send
            base.Send(data);
            return source.Task;
        }

        public Task<IMessage> SendAsync(IMessage clientMessage)
        {
            var commandText = clientMessage.Command?.Text ?? string.Empty;
            var source = new TaskCompletionSource<IMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueCompletionSource(commandText, source);

            Send(clientMessage.ToBytes());
            return source.Task;
        }

        public override void Send(byte[] data)
        {
            byte[] t = new byte[data.Length + 4];
            Array.Copy(data, 0, t, 4, data.Length);
            _rc4.Encrypt(t, 1, t.Length - 1);
            base.Send(t);
        }

        private void EnqueueCompletionSource(string commandText, TaskCompletionSource<IMessage> source)
        {
            var q = _commandQueue.GetOrAdd(commandText, _ => new ConcurrentQueue<TaskCompletionSource<IMessage>>());
            q.Enqueue(source);
        }

        protected override void OnReceived(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            _rc4.Decrypt(data, 1, data.Length - 1);

            byte compressionLevel = data[0];
            string messageText;

            if (compressionLevel == 2)
            {
                byte[] decompressed = GZip.Decompress(data, 5);
                int offset = 3; // Decompressed stream includes 3 leading zero bytes
                int length = Math.Max(0, decompressed.Length - offset);
                messageText = Encoding.UTF8.GetString(decompressed, offset, length);
            }
            else
            {
                int offset = 4; // Uncompressed includes 1 byte compression level + 3 zero bytes
                int length = Math.Max(0, data.Length - offset);
                messageText = Encoding.UTF8.GetString(data, offset, length);
            }

            OnReceivedMessageText(messageText);
            base.OnReceived(data);
        }

        private void OnReceivedMessageText(string messageText)
        {
            if (string.IsNullOrEmpty(messageText))
            {
                return;
            }

            var parts = messageText.Split(new[] { ':' }, 3);
            var commandText = parts[0];
            var command = Commands.GetCommandByText(commandText) ?? new Command(commandText);
            var sender = parts.Length > 1 ? parts[1] : string.Empty;
            var genxyData = parts.Length > 2 ? parts[2] : string.Empty;

            Dictionary<string, object> deserializedData;
            try
            {
                deserializedData = GenxyConverter.Deserialize(genxyData);
            }
            catch
            {
                deserializedData = new Dictionary<string, object>();
            }

            var message = new Message(command, deserializedData)
            {
                Sender = sender
            };

            MessageReceived?.Invoke(message);

            if (_commandQueue.TryGetValue(commandText, out var q))
            {
                if (q.TryDequeue(out var source))
                {
                    source.TrySetResult(message);
                }
            }

            Received?.Invoke(messageText);
        }
    }
}
