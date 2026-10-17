using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class ChatMessage
    {
        public string ChannelName { get; init; } = string.Empty;
        public int SenderId { get; init; }
        public string Message { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public static ChatMessage? FromNotification(IDictionary<string, object> notificationData)
        {
            var channelName = notificationData.GetString(k.channel);
            var command = notificationData.GetInt(k.command);

            // ChannelNotify.Message is 6
            if (command != 6)
            {
                return null;
            }

            var innerData = notificationData.GetDictionary(k.data);
            if (innerData == null)
            {
                return null;
            }

            var senderId = innerData.GetInt(k.sender);
            var message = innerData.GetString(k.message);

            return new ChatMessage
            {
                ChannelName = channelName,
                SenderId = senderId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                RawData = (notificationData as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(notificationData)
            };
        }
    }
}
