using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.EventServices.EventMessages;

namespace Perpetuum.Services.Channels
{
    public interface IChannelManager
    {
        IEnumerable<Channel> Channels { get; }

        [CanBeNull]
        Channel GetChannelByName(string name);

        string GetChannelNameByDiscordId(ulong discordId);

        void CreateChannel(ChannelType type, string name);
        void DeleteChannel(string channelName);
        void JoinChannel(string channelName, Character member, ChannelMemberRole role, string password);

        void LeaveAllChannels(Character character);
        void LeaveChannel(string channelName, Character character);
        void SetMemberRole(string channelName, Character issuer, Character character, ChannelMemberRole role);
        void SetPassword(string channelName, Character issuer, string password);
        void SetTopic(string channelName, Character issuer, string topic);
        void Talk(string channelName, Character sender, string message, IRequest request);
        /// <summary>
        /// Post an announcement message for one/all member
        /// </summary>
        /// <param name="channelName"> Channel name</param>
        /// <param name="sender">Sender character</param>
        /// <param name="message">Message string</param>
        /// <param name="rerecipient">Member to receive the announcement. null - for all members</param>
        void Announcement(string channelName, Character sender, string message, Character? recipient = null);
        void PinnedAnnouncement(string channelName, Character sender, string message, PinSlot pinSlot);
        void KickOrBan(string channelName, Character issuer, Character character, string message, bool ban);
        void UnBan(string channelName, Character issuer, Character character);

        IEnumerable<Character> GetBannedCharacters(string channelName, Character issuer);

        IEnumerable<Channel> GetChannelsByMember(Character member);
        IEnumerable<Channel> GetPublicChannels();
        IEnumerable<Channel> GetAllChannels();
    }
}