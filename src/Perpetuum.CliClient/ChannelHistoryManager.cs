using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Perpetuum.CliClient.Models;

namespace Perpetuum.CliClient
{
    public class ChannelHistoryManager
    {
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _channelHistories =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly int _maxHistoryPerChannel;

        public ChannelHistoryManager(int maxHistoryPerChannel = 100)
        {
            _maxHistoryPerChannel = maxHistoryPerChannel;
        }

        public void AddMessage(ChatMessage message)
        {
            if (string.IsNullOrEmpty(message.ChannelName))
            {
                return;
            }

            var list = _channelHistories.GetOrAdd(message.ChannelName, _ => new List<ChatMessage>());
            lock (list)
            {
                list.Add(message);
                if (list.Count > _maxHistoryPerChannel)
                {
                    list.RemoveAt(0);
                }
            }
        }

        public IReadOnlyList<ChatMessage> GetRecentMessages(string channelName, int count = 5)
        {
            if (!_channelHistories.TryGetValue(channelName, out var list))
            {
                return Array.Empty<ChatMessage>();
            }

            lock (list)
            {
                int skip = Math.Max(0, list.Count - count);
                return list.Skip(skip).ToList();
            }
        }

        public IReadOnlyList<ChatMessage> GetAllMessages(string channelName)
        {
            if (!_channelHistories.TryGetValue(channelName, out var list))
            {
                return Array.Empty<ChatMessage>();
            }

            lock (list)
            {
                return list.ToList();
            }
        }

        public void Clear(string channelName)
        {
            if (_channelHistories.TryGetValue(channelName, out var list))
            {
                lock (list)
                {
                    list.Clear();
                }
            }
        }
    }
}
