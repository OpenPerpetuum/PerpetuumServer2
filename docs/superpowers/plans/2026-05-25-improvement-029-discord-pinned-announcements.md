# IMPROVEMENT-029: Discord Pinned Announcements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When the server sends daily pool or leaderboard announcements to Discord, automatically pin the message and unpin the previous one for that slot so players can always find them.

**Architecture:** A new `DiscordPinnableMessage` event type carries a `PinSlot` tag through the existing event pipeline. `EventListenerService` handles it by sending, unpinning the old message (looked up from a `discord_pin_state` DB table), pinning the new one, and persisting the new message ID. `ChannelManager` gets a `PinnedAnnouncement` method that emits this new type. `SeasonService` calls it for daily pool and leaderboard announcements only.

**Tech Stack:** C# 12 / .NET 8, Discord.Net (`IUserMessage.PinAsync/UnpinAsync`, `IMessageChannel.GetMessageAsync`), SQL Server (`MERGE` upsert), Autofac DI.

---

## File Map

| Action | Path |
|---|---|
| Create | `src/Perpetuum/Services/EventServices/EventMessages/PinSlot.cs` |
| Create | `src/Perpetuum/Services/EventServices/EventMessages/DiscordPinnableMessage.cs` |
| Create | `src/Perpetuum/Services/EventServices/IDiscordPinStateRepository.cs` |
| Create | `src/Perpetuum/Services/EventServices/DiscordPinStateRepository.cs` |
| Modify | `src/Perpetuum/Services/Channels/IChannelManager.cs` |
| Modify | `src/Perpetuum/Services/Channels/ChannelManager.cs` |
| Modify | `src/Perpetuum/Services/EventServices/EventListenerService.cs` |
| Modify | `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` |
| Modify | `src/Perpetuum/Services/Seasons/SeasonService.cs` |
| Create | `docs/db_structure/migrations/add_discord_pin_state.sql` |

---

## Task 1: DB migration — create `discord_pin_state` table

**Files:**
- Create: `docs/db_structure/migrations/add_discord_pin_state.sql`

- [ ] **Step 1: Create the migration SQL file**

Create `docs/db_structure/migrations/add_discord_pin_state.sql` with:

```sql
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'discord_pin_state'
)
BEGIN
    CREATE TABLE discord_pin_state (
        pin_slot           TINYINT      NOT NULL,
        discord_channel_id VARCHAR(20)  NOT NULL,
        discord_message_id VARCHAR(20)  NOT NULL,
        CONSTRAINT PK_discord_pin_state PRIMARY KEY (pin_slot)
    );
END
```

- [ ] **Step 2: Run the migration against your development database**

Open SSMS (or your preferred SQL client), connect to the game database, and execute the script. Verify that `discord_pin_state` now appears in the table list with zero rows.

- [ ] **Step 3: Commit**

```bash
git add docs/db_structure/migrations/add_discord_pin_state.sql
git commit -m "feat: add discord_pin_state table migration (IMPROVEMENT-029)"
```

---

## Task 2: `PinSlot` enum and `DiscordPinnableMessage`

**Files:**
- Create: `src/Perpetuum/Services/EventServices/EventMessages/PinSlot.cs`
- Create: `src/Perpetuum/Services/EventServices/EventMessages/DiscordPinnableMessage.cs`

- [ ] **Step 1: Create `PinSlot.cs`**

```csharp
namespace Perpetuum.Services.EventServices.EventMessages
{
    public enum PinSlot
    {
        DailyPool = 0,
        Leaderboard = 1,
    }
}
```

- [ ] **Step 2: Create `DiscordPinnableMessage.cs`**

```csharp
namespace Perpetuum.Services.EventServices.EventMessages
{
    public class DiscordPinnableMessage : IEventMessage
    {
        public EventType Type => EventType.PerpetuumToDiscord;
        public ulong DiscordChannelId { get; }
        public string Nick { get; }
        public string Message { get; }
        public PinSlot PinSlot { get; }

        public DiscordPinnableMessage(ulong discordChannelId, string nick, string message, PinSlot pinSlot)
        {
            DiscordChannelId = discordChannelId;
            Nick = nick;
            Message = message;
            PinSlot = pinSlot;
        }
    }
}
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with zero errors.

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Services/EventServices/EventMessages/PinSlot.cs
git add src/Perpetuum/Services/EventServices/EventMessages/DiscordPinnableMessage.cs
git commit -m "feat: add PinSlot enum and DiscordPinnableMessage (IMPROVEMENT-029)"
```

---

## Task 3: `IDiscordPinStateRepository` and `DiscordPinStateRepository`

**Files:**
- Create: `src/Perpetuum/Services/EventServices/IDiscordPinStateRepository.cs`
- Create: `src/Perpetuum/Services/EventServices/DiscordPinStateRepository.cs`

- [ ] **Step 1: Create `IDiscordPinStateRepository.cs`**

```csharp
using Perpetuum.Services.EventServices.EventMessages;

namespace Perpetuum.Services.EventServices
{
    public interface IDiscordPinStateRepository
    {
        (ulong channelId, ulong messageId)? Get(PinSlot slot);
        void Upsert(PinSlot slot, ulong channelId, ulong messageId);
    }
}
```

- [ ] **Step 2: Create `DiscordPinStateRepository.cs`**

```csharp
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
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with zero errors.

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Services/EventServices/IDiscordPinStateRepository.cs
git add src/Perpetuum/Services/EventServices/DiscordPinStateRepository.cs
git commit -m "feat: add DiscordPinStateRepository (IMPROVEMENT-029)"
```

---

## Task 4: `IChannelManager.PinnedAnnouncement` and `ChannelManager.PinnedAnnouncement`

**Files:**
- Modify: `src/Perpetuum/Services/Channels/IChannelManager.cs`
- Modify: `src/Perpetuum/Services/Channels/ChannelManager.cs`

- [ ] **Step 1: Add the method to `IChannelManager.cs`**

In `src/Perpetuum/Services/Channels/IChannelManager.cs`, add a `using` for the event messages namespace at the top:

```csharp
using Perpetuum.Services.EventServices.EventMessages;
```

Then add the method declaration after the existing `Announcement` signature:

```csharp
void PinnedAnnouncement(string channelName, Character sender, string message, PinSlot pinSlot);
```

The `Announcement` declaration is on line 32. The full relevant section after your edit:

```csharp
void Announcement(string channelName, Character sender, string message, Character? recipient = null);
void PinnedAnnouncement(string channelName, Character sender, string message, PinSlot pinSlot);
void KickOrBan(string channelName, Character issuer, Character character, string message, bool ban);
```

- [ ] **Step 2: Implement the method in `ChannelManager.cs`**

In `src/Perpetuum/Services/Channels/ChannelManager.cs`, insert the new method immediately after the closing brace of `Announcement()` (around line 359). The existing `Announcement()` ends with the recipient path; add the new method after it:

```csharp
public void PinnedAnnouncement(string channelName, Character sender, string message, PinSlot pinSlot)
{
    if (!_channels.TryGetValue(channelName, out Channel? channel))
        return;

    channel.Logger.LogMessage(sender, message);
    channel.SendMessageToAll(_sessionManager, sender, message);

    if (channel.DiscordId != null && sender.Nick != "Discord")
    {
        _eventChannel.PublishMessage(
            new DiscordPinnableMessage(
                channel.DiscordId.Value,
                sender.Nick,
                message,
                pinSlot));
    }
}
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with zero errors.

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Services/Channels/IChannelManager.cs
git add src/Perpetuum/Services/Channels/ChannelManager.cs
git commit -m "feat: add PinnedAnnouncement to ChannelManager (IMPROVEMENT-029)"
```

---

## Task 5: `EventListenerService` — handle `DiscordPinnableMessage`

**Files:**
- Modify: `src/Perpetuum/Services/EventServices/EventListenerService.cs`

- [ ] **Step 1: Add the repository field and update the constructor**

In `EventListenerService.cs`, add the field after `_globalConfiguration`:

```csharp
private readonly IDiscordPinStateRepository _pinStateRepository;
```

Update the constructor signature and body (the constructor currently starts at line 24):

```csharp
public EventListenerService(GlobalConfiguration globalConfiguration, IDiscordPinStateRepository pinStateRepository)
{
    _observers = new Dictionary<EventType, IList<IEventProcessor>>();
    _queue = new ConcurrentQueue<IEventMessage>();

    _client = new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
    });

    _globalConfiguration = globalConfiguration;
    _pinStateRepository = pinStateRepository;
}
```

- [ ] **Step 2: Add the `DiscordPinnableMessage` branch in `PublishMessage`**

In `PublishMessage`, the existing `if` block ends at line 53 with `}`. Add the new branch immediately after it, before the `else` that enqueues:

```csharp
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
                string messageToSend = $"**<{pinnableMessage.Nick}>**: {pinnableMessage.Message}";
                var sent = await discordChannel.SendMessageAsync(
                    messageToSend,
                    allowedMentions: new AllowedMentions { AllowedTypes = AllowedMentionTypes.Users });

                var existing = _pinStateRepository.Get(pinnableMessage.PinSlot);
                if (existing.HasValue)
                {
                    try
                    {
                        var oldMsg = await discordChannel.GetMessageAsync(existing.Value.messageId);
                        if (oldMsg is IUserMessage oldUserMsg)
                            await oldUserMsg.UnpinAsync();
                    }
                    catch { }
                }

                try { await sent.PinAsync(); }
                catch { }

                _pinStateRepository.Upsert(
                    pinnableMessage.PinSlot,
                    pinnableMessage.DiscordChannelId,
                    sent.Id);
            });
        }
    }
    else
    {
        _queue.Enqueue(message);
    }
}
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with zero errors.

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Services/EventServices/EventListenerService.cs
git commit -m "feat: handle DiscordPinnableMessage with pin/unpin logic in EventListenerService (IMPROVEMENT-029)"
```

---

## Task 6: Autofac registration

**Files:**
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`

- [ ] **Step 1: Register `DiscordPinStateRepository`**

In `PerpetuumBootstrapper.cs`, find the comment `// OPP: EventListenerService and consumers` (around line 581). Add the registration on the line immediately before it:

```csharp
_ = _builder.RegisterType<DiscordPinStateRepository>().As<IDiscordPinStateRepository>().SingleInstance();

// OPP: EventListenerService and consumers
```

You will also need to add a `using` for the EventServices namespace at the top of the bootstrapper file if it is not already present:

```csharp
using Perpetuum.Services.EventServices;
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with zero errors. Autofac will now inject `DiscordPinStateRepository` into `EventListenerService` automatically because `EventListenerService` is registered as `SingleInstance()` and Autofac resolves constructor parameters by type.

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs
git commit -m "feat: register DiscordPinStateRepository in Autofac (IMPROVEMENT-029)"
```

---

## Task 7: `SeasonService` — switch two call sites to `PinnedAnnouncement`

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Add `using` for `PinSlot`**

At the top of `SeasonService.cs`, add:

```csharp
using Perpetuum.Services.EventServices.EventMessages;
```

- [ ] **Step 2: Update `AnnounceDailyPool`**

Locate the `AnnounceDailyPool` method (around line 422). Change the final `Announcement` call:

```csharp
// Before:
_channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, sb.ToString());

// After:
_channelManager.Value.PinnedAnnouncement(SeasonChannelName, _announcer.Value, sb.ToString(), PinSlot.DailyPool);
```

- [ ] **Step 3: Update `AnnounceLeaderboard`**

Locate the `AnnounceLeaderboard` method (around line 377). Change the final `Announcement` call:

```csharp
// Before:
_channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());

// After:
_channelManager.Value.PinnedAnnouncement(SeasonChannelName, _announcer.Value, chatMessage.ToString(), PinSlot.Leaderboard);
```

- [ ] **Step 4: Build to verify no errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with zero errors.

- [ ] **Step 5: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat: pin daily pool and leaderboard announcements in Discord (IMPROVEMENT-029)"
```

---

## Manual Validation

After all tasks are complete, validate end-to-end in a running server with a Discord bot that has `Manage Messages` permission on the target channel:

1. **First daily pool announcement** — trigger via server restart or admin command. Verify the message appears in Discord and is pinned (check Pinned Messages in Discord).
2. **Second daily pool announcement** — trigger again. Verify the first pin is removed and the new message is pinned.
3. **Leaderboard announcement** — wait for or trigger a leaderboard update. Verify it pins independently — the daily pool pin should still be present alongside it.
4. **Unpin failure recovery** — manually delete the pinned Discord message, then trigger another announcement. Verify the server does not crash and the new message is pinned successfully (the `catch { }` in the unpin path handles this).
5. **Missing permission** — temporarily remove `Manage Messages` from the bot, trigger an announcement. Verify the message still sends to Discord, the server does not crash, and the message ID is still persisted to `discord_pin_state`.
6. **Restart recovery** — confirm `discord_pin_state` has rows. Restart the server. Trigger another announcement. Verify the old message is unpinned and the new one is pinned (DB-persisted ID was used for the unpin).

---

## Potential Regressions

- All `Announcement()` call sites in `SeasonService` other than the two modified here are unchanged.
- The `DiscordIntegrationMessage` branch in `EventListenerService.PublishMessage` is unchanged.
- In-game chat delivery in `ChannelManager` is unchanged for both `Announcement` and `PinnedAnnouncement`.
- No changes to zone update paths, NPC AI, combat, or market systems.
