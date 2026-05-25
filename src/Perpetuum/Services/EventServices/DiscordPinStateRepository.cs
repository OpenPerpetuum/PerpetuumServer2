using Perpetuum.Data;
using Perpetuum.Services.EventServices.EventMessages;

namespace Perpetuum.Services.EventServices
{
    public class DiscordPinStateRepository : IDiscordPinStateRepository
    {
        public (ulong channelId, ulong messageId)? Get(PinSlot slot)
        {
            var record = Db.Query(
                "SELECT discord_channel_id, discord_message_id " +
                "FROM discord_pin_state WHERE pin_slot = @pin_slot")
                .SetParameter("@pin_slot", (int)slot)
                .ExecuteSingleRow();

            if (record == null)
                return null;

            return (
                ulong.Parse(record.GetValue<string>("discord_channel_id")),
                ulong.Parse(record.GetValue<string>("discord_message_id"))
            );
        }

        public void Upsert(PinSlot slot, ulong channelId, ulong messageId)
        {
            Db.Query(
                "MERGE discord_pin_state AS t " +
                "USING (VALUES (@pin_slot, @channel_id, @message_id)) " +
                "    AS s (pin_slot, discord_channel_id, discord_message_id) " +
                "ON t.pin_slot = s.pin_slot " +
                "WHEN MATCHED THEN " +
                "    UPDATE SET discord_channel_id = s.discord_channel_id, " +
                "               discord_message_id = s.discord_message_id " +
                "WHEN NOT MATCHED THEN " +
                "    INSERT (pin_slot, discord_channel_id, discord_message_id) " +
                "    VALUES (s.pin_slot, s.discord_channel_id, s.discord_message_id);")
                .SetParameter("@pin_slot", (int)slot)
                .SetParameter("@channel_id", channelId.ToString())
                .SetParameter("@message_id", messageId.ToString())
                .ExecuteNonQuery();
        }
    }
}
