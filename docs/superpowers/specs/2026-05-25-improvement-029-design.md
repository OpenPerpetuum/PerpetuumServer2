# IMPROVEMENT-029 Design: Pin Daily Activity Announcements in Discord

**Date:** 2026-05-25
**Status:** Approved
**Area:** Seasons / Announcements / Discord Integration

---

## Problem

Daily pool and leaderboard announcements are sent to the integrated Discord channel but quickly get buried by subsequent chat volume. Players miss the current day's active objectives. Pinning the messages keeps them visible regardless of chat activity.

---

## Scope

Pin two announcement types:
- **Daily pool** (`AnnounceDailyPool`) — pinned under slot `DailyPool`
- **Leaderboard** (`AnnounceLeaderboard`) — pinned under slot `Leaderboard`

Each slot holds its own pin independently. All other announcement types (season start/end, objective complete, tier unlock) are unchanged.

---

## Integration Context

The server uses a **Discord.Net bot token** (`DiscordBotToken` in `GlobalConfiguration`). The `Manage Messages` permission is required on the target channel. Webhook-based integrations cannot pin; the bot token path already in use supports it.

---

## Architecture

### New Types

**`PinSlot` enum** — `Perpetuum.Services.EventServices.EventMessages`
```csharp
public enum PinSlot { DailyPool = 0, Leaderboard = 1 }
```

**`DiscordPinnableMessage`** — `Perpetuum.Services.EventServices.EventMessages`
```csharp
public class DiscordPinnableMessage : IEventMessage
{
    public EventType Type => EventType.PerpetuumToDiscord;
    public ulong DiscordChannelId { get; }
    public string Nick { get; }
    public string Message { get; }
    public PinSlot PinSlot { get; }
}
```

Reuses `EventType.PerpetuumToDiscord` — same directional intent as `DiscordIntegrationMessage`. No new `EventType` value needed.

---

### Data Layer

**New table: `discord_pin_state`**
```sql
CREATE TABLE discord_pin_state (
    pin_slot           TINYINT  NOT NULL,
    discord_channel_id BIGINT   NOT NULL,
    discord_message_id BIGINT   NOT NULL,
    CONSTRAINT PK_discord_pin_state PRIMARY KEY (pin_slot)
)
```

One row per `PinSlot` value. Survives server restarts, enabling clean unpin on the next announcement cycle.

**`IDiscordPinStateRepository`**
```csharp
public interface IDiscordPinStateRepository
{
    Task<(ulong channelId, ulong messageId)?> GetAsync(PinSlot slot);
    Task UpsertAsync(PinSlot slot, ulong channelId, ulong messageId);
}
```

Implementation follows existing repository patterns (parameterized queries, `IDbConnectionFactory`). Registered in Autofac in `PerpetuumBootstrapper`.

---

### `ChannelManager` Changes

New method added to `IChannelManager` and `ChannelManager`:

```csharp
void PinnedAnnouncement(string channelName, Character sender, string message, PinSlot pinSlot)
```

Identical in-game broadcast logic to `Announcement()`. Discord dispatch difference: publishes `DiscordPinnableMessage` instead of `DiscordIntegrationMessage`. Existing `Announcement()` method is unchanged.

---

### `EventListenerService` Changes

`IDiscordPinStateRepository` added as a constructor parameter.

New branch in `PublishMessage()`:

```csharp
else if (message is DiscordPinnableMessage pinnableMessage)
{
    if (_client.GetChannel(pinnableMessage.DiscordChannelId) is IMessageChannel discordChannel)
    {
        Task.Run(async () =>
        {
            string messageToSend = $"**<{pinnableMessage.Nick}>**: {pinnableMessage.Message}";
            var sent = await discordChannel.SendMessageAsync(
                messageToSend,
                allowedMentions: new AllowedMentions { AllowedTypes = AllowedMentionTypes.Users });

            var existing = await _pinStateRepository.GetAsync(pinnableMessage.PinSlot);
            if (existing.HasValue)
            {
                try
                {
                    var oldMsg = await discordChannel.GetMessageAsync(existing.Value.messageId);
                    if (oldMsg is IUserMessage oldUserMsg)
                        await oldUserMsg.UnpinAsync();
                }
                catch { /* log warning — unpin failure does not block new pin */ }
            }

            try { await sent.PinAsync(); }
            catch { /* log warning */ }

            await _pinStateRepository.UpsertAsync(
                pinnableMessage.PinSlot,
                pinnableMessage.DiscordChannelId,
                sent.Id);
        });
    }
}
```

Failure modes:
- **Unpin fails** (e.g. old message deleted externally): caught, logged, new pin proceeds.
- **Pin fails** (e.g. missing `Manage Messages` permission): caught, logged, message ID still persisted so the next cycle can attempt unpin.
- **Send fails**: uncaught — consistent with existing fire-and-forget behavior in `PublishMessage`.

`Task.Run(async () => { ... })` pattern is consistent with existing async Discord calls in `Start()` and `Stop()`.

---

### `SeasonService` Changes

Two call sites changed:

| Method | Before | After |
|---|---|---|
| `AnnounceDailyPool()` | `Announcement(SeasonChannelName, sender, msg)` | `PinnedAnnouncement(SeasonChannelName, sender, msg, PinSlot.DailyPool)` |
| `AnnounceLeaderboard()` | `Announcement(SeasonChannelName, sender, msg)` | `PinnedAnnouncement(SeasonChannelName, sender, msg, PinSlot.Leaderboard)` |

All other `Announcement` call sites in `SeasonService` are unchanged.

---

## Files Affected

| File | Change |
|---|---|
| `src/Perpetuum/Services/EventServices/EventMessages/DiscordPinnableMessage.cs` | New |
| `src/Perpetuum/Services/EventServices/EventMessages/PinSlot.cs` | New |
| `src/Perpetuum/Repositories/DiscordPinStateRepository.cs` | New |
| `src/Perpetuum/Services/EventServices/EventListenerService.cs` | Add constructor param, new PublishMessage branch |
| `src/Perpetuum/Services/Channels/ChannelManager.cs` | New `PinnedAnnouncement` method |
| `src/Perpetuum/Services/Channels/IChannelManager.cs` | New interface method |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Two call sites |
| `src/Perpetuum.Server/PerpetuumBootstrapper.cs` | Autofac registration |
| DB migration | `CREATE TABLE discord_pin_state` |

---

## Manual Validation Steps

1. Start server with a configured Discord bot token and `Manage Messages` permission on the target channel.
2. Trigger `AnnounceDailyPool` — verify the message appears in Discord and is pinned.
3. Trigger `AnnounceDailyPool` a second time — verify the previous pin is removed and the new message is pinned.
4. Trigger `AnnounceLeaderboard` — verify it pins independently (daily pool pin remains).
5. Manually delete the pinned Discord message and trigger another announcement — verify the unpin failure is logged but the new message still pins successfully.
6. Remove `Manage Messages` permission from the bot and trigger an announcement — verify the message sends, the pin failure is logged, and the server does not crash or block.
7. Restart the server and trigger an announcement — verify the previous pin is correctly unpinned (DB-persisted ID used).

---

## Potential Regressions

- `ChannelManager.Announcement()` is unchanged — all non-pinned announcement paths are unaffected.
- `EventListenerService.PublishMessage()` existing `DiscordIntegrationMessage` branch is unchanged.
- No changes to in-game chat delivery logic.
- Discord message format (`**<Nick>**: message`) is identical between pinned and non-pinned paths.
