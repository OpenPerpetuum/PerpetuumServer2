# Sparky Local Question Capture Pilot

This capture-only feature collects likely player questions from the four mapped in-game and Discord channels for later human review. It does not send questions to Sparky or LM Studio and does not make Sparky answer them. Run it on the local development server only; do not enable it on VDI.

## Configure the local server

The settings below are JSON properties in the existing `perpetuum.ini` object. Merge them into the file; do not replace its other settings. Replace the four example IDs with the Discord IDs for `op-general`, `op-help`, `op-bug-reports`, and `op-suggestions-feature-discussion`.

```json
{
  "enableSparky": false,
  "sparkyQuestionCaptureEnabled": true,
  "sparkyQuestionCaptureDurationHours": 48,
  "sparkyQuestionCaptureDiscordChannelIds": [
    111111111111111111,
    222222222222222222,
    333333333333333333,
    444444444444444444
  ],
  "sparkyQuestionCapturePath": "logs/sparky-question-capture.jsonl"
}
```

`enableSparky` can remain `false`; capture is controlled independently. Duration must be from 1 to 48 hours. The channel ID list must contain exactly four unique, nonzero IDs. The optional archive path can be absolute or relative to `GameRoot`, but must be on a local drive. If omitted, the archive defaults to `<GameRoot>\logs\sparky-question-capture.jsonl`.

The Discord bot needs the **Message Content Intent** enabled in the Discord Developer Portal as well as in the server client. The server already requests this gateway intent. Capture only sees new messages after the session begins; it does not read message history, DMs, attachments, or other channels.

Before starting the session, post this notice in the four selected channels:

> During a local Sparky pilot, likely questions posted in these channels may be saved on the development machine for up to 48 hours to improve question handling. The archive omits account and platform IDs, but preserves the message text, including names people type.

Then start the local development server. The server creates the session metadata at `<archive path>.session.json` and logs whether capture is enabled. The saved UTC deadline is not extended by a server restart. Capture does not depend on the AI machine or Sparky service.

## Review the archive

The archive is UTF-8 JSON Lines: each accepted question is a separate complete JSON object on one line. For the default path, inspect it in PowerShell with:

```powershell
Get-Content -Tail 30 '<GameRoot>\logs\sparky-question-capture.jsonl'
```

A record looks like this:

```json
{"capturedAtUtc":"2026-10-12T10:15:30.123Z","source":"Discord","channel":"op-help","text":"[user mention], how does this work?"}
```

Only `capturedAtUtc`, `source`, `channel`, and `text` are stored. Sender names and account, Discord, channel, and message IDs are omitted. Raw Discord user mention tokens are replaced with `[user mention]`. Message text is retained and may itself include names or other details typed by a player. Keep the archive on the development machine, review it manually, and do not treat it as approved training or knowledge content without human review.

The server logs capture state and the number of records appended during the current process; it does not log captured text. A failed archive write disables capture for that run while ordinary chat and Discord forwarding continue.

## Manual pilot checks

- With capture disabled, verify no archive or metadata file is created and ordinary chat and Sparky's existing toggle continue as configured.
- With capture enabled, post a candidate question in each selected channel. Check the channel label, timestamp, text, and correct source.
- Send one in-game question in a mapped channel and verify it appears exactly once as `Game` despite the Discord mirror. Send a Discord question and verify exactly one `Discord` record. Confirm bot-authored messages and other channels are ignored.
- Try English, German, Portuguese, and Russian question openings, plus a message with `?`. Confirm unrelated statements are not captured.
- Mention another Discord user in a question and confirm the raw `<@...>` token is stored as `[user mention]`. Confirm no sender or platform ID fields are present.
- Restart before expiry and confirm the metadata's `expiresAtUtc` is unchanged. On a disposable archive, append an incomplete final JSON fragment, restart, and confirm only the incomplete trailing fragment is removed and new records append correctly.
- Confirm the archive is local and inaccessible to unintended local users. Do not perform these checks on the live VDI server.

## Stop capture and start a new session

To stop the pilot, set `sparkyQuestionCaptureEnabled` to `false` in `perpetuum.ini` and restart the local server. Keep or remove the JSONL archive according to your review and retention needs. Leaving the metadata file in place preserves the session deadline if capture is later enabled again before that deadline.

After a session expires, the service remains inactive and will not silently start a new deadline. To explicitly begin a new session, while the local server is stopped:

1. Set `sparkyQuestionCaptureEnabled` to `false` and save `perpetuum.ini`.
2. Remove only `<archive path>.session.json`; keep the JSONL archive.
3. Set `sparkyQuestionCaptureEnabled` to `true`.
4. Start the local server. It creates a new metadata file and a new deadline.

Never use this pilot configuration on VDI. No installation or change to the Sparky AI service is required.
