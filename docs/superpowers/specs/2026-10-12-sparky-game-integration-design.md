# Sparky Game and Discord Integration Design

**Status:** Approved for implementation
**Date:** 2026-10-12

## Goal

Connect the existing Sparky item-stat and recipe answer service to ordinary-player in-game chat and Discord, without blocking the game process loop or exposing account and character data to the AI machine. Sparky speaks as the Syndicate Technician Chief. The supported proof-of-concept knowledge remains verified item stats, ammunition, direct recipes, and recursively expanded materials; lore and other knowledge domains remain deferred.

## Agreed decisions

- Use the existing VDM-to-AI reverse SSH tunnel and HTTPS API. The VDM caller connects to the loopback-forwarded `https://127.0.0.1:5080/v1/answer` endpoint, validates the certificate normally, and authenticates with the configured bearer token.
- Use an in-process coordinator with a bounded background HTTP queue. Chat and Discord ingress must not wait for model inference. Completed work returns through the game event queue for delivery.
- Trigger in-game questions with a case-insensitive `Sparky, ...` prefix. Trigger Discord questions by mentioning the bot.
- In Discord, accept mentions in any channel the bot can access, including channels that have no in-game channel mapping.
- Deliver the answer only on the platform where the question originated. Do not mirror Sparky's answer into a linked channel on the other platform. Existing forwarding of the original player question remains unchanged.
- For an in-game question, determine delivery when the answer returns: post to the originating game chat channel if the character is online; otherwise send in-game mail.
- Use `[OPP] Sparky - The Syndicate Welcome Agent` as the game character sender for both chat and offline mail. Its in-character answer text identifies Sparky as the Syndicate Technician Chief.
- Keep the proof of concept English-only.
- Keep short-lived context in memory on the game server, separated by source identity. The default sliding inactivity lifetime is 15 minutes and is configurable. Context retains only the last successfully resolved item display name; there is no database persistence or cross-platform identity linking.
- Limit the complete Discord message and in-game mail body to 2,000 characters. Keep in-game chat answers under the same configured cap for consistent behavior.
- Ordinary players only. No admin-only trigger, command, or data path is added.
- Do not connect Sparky to a live database. The AI service continues to use the offline export of public game content.

## Interaction contract

### In-game chat

After the existing channel membership and mute checks, recognize a message that starts with `Sparky,` (case-insensitive, with optional whitespace after the comma). Remove the trigger text before submitting the question. A message containing the name elsewhere in its text is ordinary chat. A bare trigger receives a short usage hint and does not call the AI service.

The original question remains an ordinary channel message and continues to follow the current channel-to-Discord bridge behavior. Sparky's eventual answer is posted only to the originating in-game channel, even when that channel is linked to Discord. This requires an explicit game-only channel announcement path; the existing `IChannelManager.Announcement` mirrors public announcements to Discord and therefore must not be used for Sparky answers.

When an answer returns, check the caller's current session state. If online, publish the answer to the original channel using the Sparky sender character. If offline, send an in-game character mail from that sender to the caller. If the original channel is no longer available, use mail as the fallback when the recipient character remains resolvable. The mail body, including any Sparky label or formatting, must fit within 2,000 characters.

### Discord

Recognize an actual mention of the Sparky bot, not a textual name match. Remove the bot mention from the submitted question. A mention with no question receives a short usage hint. A successful answer is sent directly to the same Discord channel by the bot, with no user ping and no game-channel mirror.

Questions in an unmapped Discord channel still reach Sparky. Existing Discord-to-game forwarding remains governed by its current channel mapping and is independent of Sparky's direct Discord reply.

### Knowledge and result presentation

Only the verified item/recipe answer service is authoritative. Item lookup, translated display names, recognized stats, recipe components, and recursive materials continue to come from the offline export and existing deterministic lookup. The model may phrase verified facts but must not invent missing stats, costs, or other unsupported facts. `not_found`, `ambiguous`, unavailable-service, queue-full, and model-failure outcomes receive short user-facing messages on the originating platform; they do not cause an answer to be posted on the other platform.

The final formatted transport message is capped after adding any sender text or formatting. The 2,000-character cap applies to the entire Discord message and the entire in-game mail body, not just the raw model field.

## Request coordinator and threading

1. The in-game channel ingress and Discord message callback perform only trigger parsing, input bounds checks, and a non-blocking admission attempt.
2. The coordinator places accepted work in a bounded background queue. Queue capacity, worker count, request timeout, and per-caller pending-request limits are configurable. A full queue or duplicate outstanding request returns a generic busy message on the source platform.
3. A worker sends HTTPS to the loopback-forwarded Sparky API. It passes the stripped question and, when present, the last resolved item display name. It does not send channel/source IDs, character IDs, Discord IDs, account data, or session state.
4. The worker turns the response into a result event containing only the local delivery metadata required by the game server and the response data. It publishes that event to the existing event queue.
5. The event processor delivers the response. It does not perform model I/O or wait for a network operation on the game update thread.

Use bounded queues and cancellation on shutdown. Do not use `.Result`, synchronous waits, untracked fire-and-forget work, or automatic request retries. A dropped tunnel, timeout, full queue, or Sparky service failure must not hold a chat handler or zone update. Logs may include outcome category, origin type, request correlation ID, and elapsed time, but not question/answer text, API tokens, or player identifiers.

## Follow-up context

The context store is volatile, bounded, and owned by the game-server process:

- In-game context is keyed by the local character ID. Discord context is keyed by the Discord author ID. These keys never leave the game server and are not joined to each other.
- Each entry stores only the last item display name returned by a successful `answered` result plus its last-activity time. Do not keep full chat history, prior generated answers, or account data.
- Expire entries after 15 minutes of inactivity by default; make the lifetime and maximum entry count configurable. Restarting the game server clears all context.
- A newly named, explicit item takes precedence over saved context. For an item-related follow-up without a new item name, apply the saved item as a lookup hint. Update context only after a successful, unambiguous answer. `not_found`, `ambiguous`, and failure outcomes do not replace it.

The AI API request model therefore gains an optional, bounded `contextItem` display-name field. The API validates it against its loaded export and uses it only as a deterministic lookup hint. It remains stateless and does not accept caller IDs or persist context. No arbitrary prior message text is sent back to the model.

## Security and configuration

- Keep the AI API bound to loopback behind the SSH remote forward. Do not add a public or LAN listener, router port forward, third-party relay, or certificate-validation bypass.
- Keep normal TLS certificate validation enabled on VDM. The API bearer token is required in addition to SSH; do not put it in a URL, process command line, source control, or logs.
- Store the VDM-side bearer token outside `perpetuum.ini` in a machine-protected configuration file with NTFS access limited to the game-server service identity and administrators. Do not copy the AI machine's private SSH key or certificate private key to VDM.
- Sparky-specific non-secret settings include an explicit disabled-by-default enable flag, API URL, request timeout, maximum response length, queue capacity, worker count, pending-request limit, context lifetime, maximum context entries, and sender-character name.
- Invalid Sparky-only configuration, missing credentials, or an unresolved sender character must disable the Sparky feature with a safe configuration error; it must not prevent unrelated game-server startup.
- The Sparky API and LM Studio receive only the player's question and optional public item display name. They never receive account IDs, character IDs, Discord IDs, online status, or live-database records. Keep item export generation offline and restricted to game-content sources.
- Existing ordinary channel logging remains as it is for the player's original chat message. Add no Sparky-specific content or identity logging.

## Failure behavior

- Unsupported or unrecognized questions are answered with a short grounded response; do not ask the model to answer from general knowledge.
- Missing/ambiguous item results are formatted from the API's structured result without exposing internal definition names.
- If the queue is full, report that Sparky is busy and ask the caller to retry later.
- If the API, tunnel, certificate, or model is unavailable, return a generic unavailable response on the source platform. Do not expose exception text, upstream response bodies, or secrets.
- Do not retry a submitted question automatically. A user can ask again after the connection recovers.
- If a caller goes offline while the model is working, use in-game mail when the response event is handled. If an in-game mail send fails, log a redacted outcome and do not place the full message or player identifiers in application logs.

## Rollout and rollback

1. Update and publish the self-contained Sparky service on the AI PC first, adding optional `contextItem` support while preserving its request limits and privacy-safe logs.
2. Implement and validate the game-server adapter in a non-live development environment. Keep the feature disabled by default.
3. Provision the VDM-side protected bearer-token file and non-secret settings only during a planned maintenance window. No live VDI change or reboot is part of this design phase.
4. Enable Sparky only after a manual end-to-end check has confirmed chat delivery, offline mail delivery, Discord mention/reply behavior, source-only answers, follow-up context, certificate validation, and the 2,000-character limit.
5. Roll back by disabling the feature flag and removing the VDM API token/configuration. This must leave ordinary chat, Discord bridging, and mail behavior operational.

## Affected systems and documentation

- `src/Perpetuum/Services/Channels/ChannelManager.cs` and `IChannelManager`: game trigger ingress and a game-only channel announcement path.
- `src/Perpetuum/Services/EventServices/`: Discord mention/author capture, bounded asynchronous coordination, and response-event delivery.
- A focused `src/Perpetuum/Services/Sparky/` service area plus Autofac/bootstrapper registration; follow the existing event processor and constructor injection patterns.
- `src/Perpetuum/Services/Mail/MailHandler.cs` usage for offline delivery; use the existing mail path and its `cmails.body nvarchar(2000)` limit.
- `src/Perpetuum/GlobalConfiguration.cs` or a narrow companion settings type for non-secret settings; a separate protected file for the bearer token.
- `src/Perpetuum.ItemStatsExporter/`: optional context-item request contract and deterministic follow-up lookup.
- `docs/codebase/ARCHITECTURE.md`, `docs/codebase/STRUCTURE.md`, and `docs/codebase/INTEGRATIONS.md` to record the new runtime boundary after implementation.

`ChannelManager`, `IChannelManager`, `EventListenerService`, and `GlobalConfiguration` have broad impact. Before changing them, inspect `docs/graph/GRAPH_REPORT.md` and run `tools/query-graph.ps1` for direct dependents. The implementation plan must include these checks before edits.

## Validation

Validate in a non-live game-server environment before any VDI rollout. The manual checks should cover:

1. Case-insensitive in-game prefix handling, bare prefix behavior, ordinary text containing “Sparky”, and unchanged question mirroring.
2. Discord bot mention detection, mention stripping, any-accessible-channel handling, bare mention behavior, and unchanged mapped-channel bridge behavior.
3. A stats question, direct recipe, recursive full-material question, explicit different item overriding context, and a follow-up using the saved item context.
4. In-game online channel response, logout during inference followed by in-game mail, and source-only delivery for linked channels.
5. Discord response in the source channel without pinging or game mirror.
6. Queue saturation, a service/tunnel failure, certificate rejection, missing/invalid API credentials, and game-server shutdown with work pending.
7. Output cap enforcement at 2,000 characters, including mail and Discord formatting.
8. Context expiry, process restart clearing context, and absence of caller identifiers or message text in new Sparky logs or outbound API payloads.

Do not run automated tests unless requested. Do not deploy to or restart the live VDI as part of spec review. A later implementation plan and deployment approval will cover the maintenance-window rollout.

## Out of scope

- Lore, general game advice, or knowledge outside verified item stats and production recipes.
- Live database access from the AI PC or Sparky service.
- Persisted conversation history, account-level context, linking identities between game and Discord, or any caller ID sent to the AI service.
- Public Sparky API exposure, router port forwarding, VPN, managed tunnel, or third-party relay.
- Mirroring Sparky answers across platforms.
- Admin-only commands or player/account information lookup.
- Changes to existing channel membership, chat permissions, Discord channel mapping, or normal mail rules.
