# Sparky Passive Question Capture Design

**Status:** Approved by user on 2026-10-12
**Date:** 2026-10-12

## Goal

Collect examples of questions players ask each other in the existing Discord and in-game chat channels, before Sparky is made available to players. Run the pilot from the local development server for up to 48 hours. Captured questions are written locally for later review; they are not sent to Sparky or LM Studio and do not trigger answers.

## Approved scope

- Listen to Discord and in-game chat only in the four linked channels: `op-general`, `op-help`, `op-bug-reports`, and `op-suggestions-feature-discussion`.
- Capture human-authored, question-like text after the capture mode starts. Do not backfill Discord history, read direct messages, capture attachments, or capture other channels.
- Store the original source as `Discord` or `Game`. In-game messages are captured at game chat ingress before the normal Discord bridge. Discord messages are captured at Discord ingress. The Discord bridge's bot-authored copies of game messages are ignored, preventing duplicate records.
- Keep capture independent from `EnableSparky`. For the pilot, the answer feature can remain disabled while the Discord bot and passive capture stay active.
- Keep capture disabled by default. An explicit configuration flag enables a session that expires 48 hours after its first start. A restart resumes the same session deadline rather than extending it.

## Capture and persistence flow

The in-game capture point is `ChannelManager.Talk`, after channel membership and mute checks and after admin-command messages have been separated from ordinary chat. It uses the mapped Discord channel ID on the game channel to apply the same allowlist as Discord ingress. Normal chat delivery, channel logging, and forwarding continue through their existing paths.

The Discord capture point is `EventListenerService.OnMessageReceived`. Capture only non-bot messages in the allowlisted Discord channel IDs. Existing bot-authored messages that mirror game chat are ignored. The Discord client already requests `GatewayIntents.MessageContent`; Discord Developer Portal access for that privileged intent must be enabled for unmentioned channel messages.

Use a deterministic, language-aware question-candidate filter rather than sending all channel traffic to an AI classifier. Capture a message if it contains a question mark or, after leading greetings and punctuation are removed, begins with a common interrogative opening in English, German, Portuguese, or Russian. Include the common who/what/when/where/why/how and yes/no question forms, plus phrases such as “anyone know” / “weiß jemand” / “alguém sabe” / “кто-нибудь знает”. The initial pilot accepts false positives and may miss indirect questions; review its yield after the pilot before broadening it.

Each accepted candidate is serialized as one UTF-8 JSON Lines record containing UTC capture time, source (`Discord` or `Game`), configured channel label, and message text. Do not store author display names, character nicknames from message metadata, account IDs, Discord user IDs, message IDs, or channel IDs. Replace raw Discord user-mention tokens in stored text with a neutral marker. Message text itself may still contain names or other details that a player typed; the archive is local and access is limited to the development machine.

Append the complete record synchronously and call `FileStream.Flush(flushToDisk: true)` before the capture call returns. Do not queue candidate records in memory: an abrupt server stop or power loss must not discard already accepted records. Serialize writes behind a small lock so game and Discord callbacks cannot interleave JSON lines. On startup after an unclean stop, truncate any incomplete trailing line before appending new records. The file path defaults under the local game's log directory and is configurable.

The synchronous flush can briefly delay the single chat message that matched the filter. It runs in chat ingress, not a zone simulation update, and should be low frequency because only candidate questions are written. A write or flush failure is logged clearly, capture is disabled for the remainder of that run, and ordinary chat continues; capture must never fail silently.

## Configuration and operation

- `SparkyQuestionCaptureEnabled`: opt-in, default `false`.
- `SparkyQuestionCaptureDurationHours`: defaults to 48 and accepts values from 1 through 48 hours.
- `SparkyQuestionCaptureDiscordChannelIds`: explicit allowlist for the four named channels. The game-side path matches the Discord IDs already attached to its mapped channels.
- `SparkyQuestionCapturePath`: optional local archive path; default is under the configured game root's logs directory.
- Persist the session start/deadline in a local capture-session metadata file so a server restart or brief outage does not reset the duration limit.
- Startup and shutdown logs report capture state and record count, but do not include captured text or player identity.
- Put a short notice in the selected channels before capture begins, explaining that likely questions may be saved locally to improve Sparky. Capture does not cause the bot to post or reply.

## Failure behavior and recovery

- If Discord channel IDs are missing, do not start capture; report the configuration problem without affecting server startup.
- If the archive directory cannot be created or opened, keep normal chat and Discord bridging operational, disable capture, and log an explicit error.
- Flush each record to disk before considering it captured. On restart, preserve all complete records and remove only an incomplete final line caused by interruption during a write.
- If the configured duration has elapsed, capture stays off after restart until explicitly enabled for a new session.
- Stop capturing when the flag is disabled or the session deadline passes. Keep existing archives until the operator reviews or removes them.

## Rollout and validation

1. Add the capture-only feature to the local development server; do not enable the Sparky answer feature or deploy it to VDI.
2. Configure the four mapped Discord channel IDs, enable capture, and post the channel notice.
3. Verify one question from each source becomes one durable record with the correct origin and no author or channel IDs.
4. Verify a game-originated message mirrored into Discord is captured once as `Game`, not a second time as `Discord`.
5. Verify ordinary statements and bot-authored messages are excluded, and unsupported channels are ignored.
6. Force-stop and restart the local server after several captured records; confirm records remain intact, capture resumes with the original deadline, and an interrupted final line does not corrupt later records.
7. Verify the 48-hour deadline disables capture after restart and normal chat/Discord forwarding remain operational after capture is disabled.

No AI service, bundle refresh, database, installer, live game server, or VDI changes are part of the pilot.

## Affected code and documentation

- `src/Perpetuum/Services/Channels/ChannelManager.cs` — in-game chat ingress and mapped channel identification.
- `src/Perpetuum/Services/EventServices/EventListenerService.cs` — Discord ingress and bot-mirror filtering.
- `src/Perpetuum/GlobalConfiguration.cs` — disabled-by-default capture settings.
- A focused capture service under `src/Perpetuum/Services/Sparky/` — question filtering, durable append, session deadline, and recovery.
- `docs/codebase/INTEGRATIONS.md` and `docs/codebase/ARCHITECTURE.md` — operator and subsystem notes.

`ChannelManager`, `EventListenerService`, and `GlobalConfiguration` are high-connectivity types. Before implementation edits, inspect the graph report and run the required dependency checks; the graph report already identifies `ChannelManager` and `EventListenerService` as God Nodes.

## Out of scope

- Making Sparky answer players during this data-gathering pilot.
- Capturing all channel messages, Discord DMs, in-game private messages, or chat history predating startup.
- Sending any collected text to LM Studio, the Sparky HTTPS API, or an external service.
- Persisting player identity, correlating game and Discord users, or analyzing individual players.
- Automatically converting captured examples into knowledge or training data. Human review is required before using them to expand Sparky.
