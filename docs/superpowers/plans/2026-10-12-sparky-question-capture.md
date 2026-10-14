# Sparky Passive Question Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Add a disabled-by-default, local-only, 48-hour maximum archive of likely questions from the four linked Discord and in-game chat channels, with every accepted record flushed to disk before the capture call returns.

**Architecture:** Add a singleton capture service that owns the channel allowlist, deterministic multilingual candidate filter, persisted session deadline, append-only JSONL archive, and recovery of a partial final line. Call it synchronously from game chat ingress and Discord message ingress; it never calls the Sparky answer service. Both sources use the mapped Discord channel IDs for allowlisting, while records store only the source, channel label, UTC timestamp, and text.

**Tech Stack:** .NET 8, C# 12, Newtonsoft.Json, Autofac, Discord.Net, `FileStream.Flush(flushToDisk: true)`.

**Spec:** `docs/superpowers/specs/2026-10-12-sparky-question-capture-design.md` (approved by the user on 2026-10-12).

## Global Constraints

- Only capture human-authored likely questions in `op-general`, `op-help`, `op-bug-reports`, and `op-suggestions-feature-discussion`.
- Capture game messages at `ChannelManager.Talk` before their Discord mirror; ignore bot-authored Discord mirrors.
- Capture only new messages after the session starts. Do not backfill, capture DMs, attachments, or other channels.
- `SparkyQuestionCaptureEnabled` defaults to `false` and is independent from `EnableSparky`.
- Session duration defaults to 48 hours and accepts only values from 1 through 48 hours. A restart keeps the persisted original deadline.
- Every accepted record is synchronously appended as one UTF-8 JSONL line and flushed to disk before capture returns. No in-memory queue is used for capture records.
- JSONL fields are UTC timestamp, source (`Discord` or `Game`), channel label, and text. Never write author names, character metadata, account IDs, Discord user IDs, message IDs, or channel IDs. Replace raw Discord user-mention tokens in stored text with `[user mention]`.
- If capture configuration or storage fails, log the failure without message contents, disable capture for the run, and keep normal chat/Discord forwarding operational.
- Keep the archive and metadata on the local development machine. Do not send captured text to Sparky, LM Studio, or another service.
- Preserve all unrelated changes already present in the working tree. Do not commit.
- Do not add or run automated tests unless the user asks for tests or verification; validate this change with the build and the manual pilot checklist below.

## Review Focus

- A game question mirrored to Discord must appear once with `Game` origin, while a Discord-authored question must appear once with `Discord` origin.
- A candidate mentioning another Discord user must store `[user mention]`, without the raw mention token or sender identity.
- English, German, Portuguese, and Russian question openings must be captured without sending channel statements or unrelated channel messages to the archive.
- A restart before the deadline must resume the original session deadline; an expired session must stay off until the operator explicitly starts a new session.
- Concurrent source callbacks must produce separate complete JSONL lines; a write/flush failure or interrupted trailing line must not block normal chat, lose earlier complete records, or corrupt subsequent appends.

---

### Task 1: Add configuration and the durable capture service

**Files:**
- Modify: `src/Perpetuum/GlobalConfiguration.cs`
- Create: `src/Perpetuum/Services/Sparky/SparkyQuestionCaptureService.cs`
- Create: `src/Perpetuum/Services/Sparky/SparkyQuestionCandidateFilter.cs`

**Interfaces:**
- `SparkyQuestionCandidateFilter.IsCandidate(string text)` returns `false` for null, empty, or whitespace-only text; otherwise it returns `true` for `?` or `？`, or for a supported question opening after trimming leading whitespace, punctuation, and configured greetings.
- `SparkyQuestionCaptureService` constructor: `SparkyQuestionCaptureService(GlobalConfiguration configuration, IHostStateService hostStateService)`.
- `SparkyQuestionCaptureService.TryCaptureGameMessage(string channelName, ulong? discordChannelId, string message)` checks the mapped ID against the configured allowlist, applies the candidate filter, then appends a `Game` record using `channelName` as its label.
- `SparkyQuestionCaptureService.TryCaptureDiscordMessage(ulong discordChannelId, string channelLabel, string candidateText, string storedText)` checks the allowlist, filters `candidateText`, redacts `<@id>` and `<@!id>` tokens from `storedText`, then appends a `Discord` record using `channelLabel`.
- `TryCapture...` methods never propagate failures to callers; a storage failure disables the service and logs the error. They return whether a record was durably appended. Callers must not use the return value to gate chat delivery.

- [x] **Step 1: Add the disabled-by-default configuration properties**

  Add these properties to `GlobalConfiguration`:

  ```csharp
  public bool SparkyQuestionCaptureEnabled { get; set; }
  public int SparkyQuestionCaptureDurationHours { get; set; } = 48;
  public List<ulong> SparkyQuestionCaptureDiscordChannelIds { get; set; } = new();
  public string SparkyQuestionCapturePath { get; set; }
  ```

  Add `using System.Collections.Generic;`. A missing or invalid ID list, or a duration outside 1–48, disables capture with an explicit log message rather than failing server startup.

- [x] **Step 2: Implement the deterministic candidate filter**

  Strip only configured leading greetings and punctuation, then match complete word or phrase prefixes case-insensitively. Include these question-opening families:

  | Language | Interrogative openings | Example “anyone know” phrases |
  |---|---|---|
  | English | `who`, `what`, `when`, `where`, `why`, `how`, `which`, `is`, `are`, `was`, `were`, `do`, `does`, `did`, `can`, `could`, `would`, `should`, `has`, `have` | `anyone know`, `anybody know`, `does anyone know`, `can someone tell` |
  | German | `wer`, `was`, `wann`, `wo`, `warum`, `wieso`, `wie`, `welche`, `ist`, `sind`, `war`, `hat`, `haben`, `kann`, `können` | `weiß jemand`, `weiss jemand`, `kennt jemand`, `kann mir jemand sagen` |
  | Portuguese | `quem`, `o que`, `qual`, `quais`, `quando`, `onde`, `por que`, `porque`, `como`, `é`, `está`, `são`, `tem`, `têm`, `pode`, `podem` | `alguém sabe`, `alguem sabe`, `alguém poderia`, `alguem poderia` |
  | Russian | `кто`, `что`, `когда`, `где`, `почему`, `зачем`, `как`, `какой`, `какая`, `есть ли`, `можно ли` | `кто-нибудь знает`, `кто нибудь знает`, `кто-то знает`, `подскажите` |

  Also strip common leading greetings `hi`, `hello`, `hey`, `hallo`, `guten tag`, `olá`, `ola`, `oi`, `привет`, and `здравствуйте`. Do not normalize arbitrary message content or use an AI classifier.

- [x] **Step 3: Implement capture-session initialization and path resolution**

  Resolve the default archive path to `<GameRoot>\logs\sparky-question-capture.jsonl`; if `SparkyQuestionCapturePath` is absolute, use it, and if relative, resolve it under `GameRoot`. Place metadata beside it at `<archive path>.session.json`.

  If capture is disabled, keep the service inactive and do not create files. If enabled and metadata is absent, validate the four unique nonzero Discord IDs and duration, create the archive directory, and durably persist UTC start/deadline metadata before enabling capture. If metadata exists, load the original values and resume only while current UTC time is earlier than the saved deadline. Invalid metadata disables capture and must not silently reset the deadline.

  At each capture call, check the persisted deadline before filtering or writing; after expiry, disable capture and append nothing.

  To start a new session after expiry, document the explicit operator action as: set `SparkyQuestionCaptureEnabled` to `false`, stop the local server, remove only the adjacent `.session.json` metadata file, set the flag to `true`, and start the server. Keep the JSONL archive intact.

- [x] **Step 4: Implement append, mention redaction, and crash recovery**

  Use one private lock for writes. Serialize each record as one compact JSON object plus `\n`, encode as UTF-8 without a BOM, write all bytes to the JSONL file, and call `Flush(flushToDisk: true)` before reporting success. Do not log captured text or identifiers.

  Use exactly these JSONL properties, with `capturedAtUtc` serialized as an ISO-8601 UTC timestamp ending in `Z`: `capturedAtUtc`, `source`, `channel`, and `text`. A record should look like:

  ```json
  {"capturedAtUtc":"2026-10-12T10:15:30.123Z","source":"Discord","channel":"op-help","text":"[user mention], how does this work?"}
  ```

  On startup, inspect the archive and truncate only bytes after its last complete newline, preserving all complete lines. Persist a new session metadata file with only `startedAtUtc` and `expiresAtUtc`, through a temporary file that is flushed to disk before atomic replacement. Catch I/O, permission, serialization, and flush exceptions, disable capture for this process, and report the failure without throwing into chat ingress. Subscribe to `IHostStateService.StateChanged`; report enabled/disabled state and the count of records appended during this process at startup and when the host enters `HostState.Off`.

- [x] **Step 5: Self-review Task 1 against the capture/privacy invariants**

  Review that disabling capture creates no files; invalid allowlist/duration/metadata cannot start capture; records contain exactly the four approved data fields; raw user mentions are redacted; and failure paths never throw back to chat callers.

---

### Task 2: Wire both chat ingress paths and register one shared service

**Files:**
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`
- Modify: `src/Perpetuum/Services/Channels/ChannelManager.cs`
- Modify: `src/Perpetuum/Services/EventServices/EventListenerService.cs`

**Interfaces:**
- Register `SparkyQuestionCaptureService` once with Autofac as `.SingleInstance()` so both chat ingress paths share the same lock, session, archive handle, and failure state.
- Extend `ChannelManager` constructor with `SparkyQuestionCaptureService questionCapture` and retain it in a private field.
- Extend `EventListenerService` constructor with `SparkyQuestionCaptureService questionCapture` and retain it in a private field.

- [x] **Step 1: Recheck graph dependencies immediately before editing high-connectivity files**

  Run:

  ```powershell
  .\tools\query-graph.ps1 EventListenerService -Direction in
  .\tools\query-graph.ps1 GlobalConfiguration -Direction in
  .\tools\query-graph.ps1 ChannelManager -Direction in
  ```

  The current graph query for `ChannelManager` returns 27 matching method nodes and is ambiguous; inspect the `Talk()` node and its inbound edges in `docs/graph/graph.json` by file path before editing. Treat `ChannelManager` and `EventListenerService` as high-risk God Nodes even if graph edges are incomplete. Confirm that adding the service does not create an Autofac cycle: the capture service depends only on `GlobalConfiguration` and `IHostStateService`, not on either ingress class.

- [x] **Step 2: Register the shared capture service**

  In `PerpetuumBootstrapper.InitContainer`, register `SparkyQuestionCaptureService` as a single instance alongside the Sparky coordinator and event listener registrations. Rely on construction when the server resolves its consumers; do not register it as an answer coordinator or connect it to LM Studio.

- [x] **Step 3: Capture in-game channel questions before delivery and mirroring**

  In `ChannelManager.Talk`, keep channel lookup, membership, mute, and admin-command handling unchanged. In the ordinary-chat branch, call:

  ```csharp
  _questionCapture.TryCaptureGameMessage(channel.Name, channel.DiscordId, message);
  ```

  before `SendMessageToAll` and before publishing the Discord bridge message. Let the capture service match `channel.DiscordId` to the allowlist; an unmapped or unlisted game channel must be ignored.

- [x] **Step 4: Capture human Discord messages independently of Sparky answers**

  At the start of `EventListenerService.OnMessageReceived`, before the normal Discord-to-game bridge, capture only non-bot messages with nonempty content. Pass `message.Channel.Id`, `message.Channel.Name`, `message.CleanContent`, and `message.Content` to `TryCaptureDiscordMessage`. Keep the capture call outside the `EnableSparky` branch and outside the `_client.CurrentUser` / bot-mention branch. Continue the existing bridge and mention processing unchanged. Bot-authored messages, including mirrored game messages, must never reach the capture method.

- [x] **Step 5: Review failure isolation and platform deduplication**

  Check that capture storage errors are caught inside the service; `ChannelManager.Talk` and `OnMessageReceived` continue their existing delivery/bridge work. Confirm a game message is recorded before its bot mirror arrives and that the bot mirror is excluded.

---

### Task 3: Document setup, lifecycle, privacy, and manual rollout

**Files:**
- Modify: `docs/codebase/INTEGRATIONS.md`
- Modify: `docs/codebase/ARCHITECTURE.md`
- Modify: `docs/codebase/STRUCTURE.md`
- Create: `docs/superpowers/sparky-question-capture.md`

- [x] **Step 1: Document the new capture service in architecture and integration references**

  State that passive capture is independent from `EnableSparky`, disabled by default, local-only, and limited to the four allowlisted channels and 48-hour maximum. Document JSONL and metadata path defaults, settings names, UTC deadline behavior, the mirrored-message deduplication rule, and that records can contain names players typed even though account and platform identity metadata is omitted.

- [x] **Step 2: Write the local operator runbook**

  In `docs/superpowers/sparky-question-capture.md`, show the four settings in `perpetuum.ini`, explain that Discord Message Content intent must be enabled, and give these steps in order: configure IDs/path/duration; post the selected-channel notice; start the local server; inspect the archive; stop capture by disabling the flag and restarting; and start a fresh session after expiry by deleting only the metadata file while the server is stopped. Include this notice template: “During a local Sparky pilot, likely questions posted in these channels may be saved on the development machine for up to 48 hours to improve question handling. The archive omits account and platform IDs, but preserves the message text, including names people type.” State that this is for the local development server only and must not be enabled on VDI during this pilot.

- [ ] **Step 3: Build the server solution**

  Build attempted with the approved command. A single-node retry compiled the application projects, including `Perpetuum`, `Perpetuum.Bootstrapper`, and `Perpetuum.Server`, but the full solution exited nonzero in its three WiX installer projects because ICE validation could not access the Windows Installer service in this environment. NuGet vulnerability-feed warnings also appeared because `api.nuget.org` was unreachable. See the execution ledger for details.

  Run:

  ```powershell
  dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
  ```

  Expected: exit code 0 and no build errors.

- [ ] **Step 4: Perform the local pilot validation**

  Pending operator-run validation on the local development server; do not run on VDI. Follow `docs/superpowers/sparky-question-capture.md`.

  Configure test IDs for the four linked channels and run only on the local development server. Before enabling, post the notice in those channels. Verify:

  1. With capture disabled, ordinary game chat, Discord chat, and the existing Sparky toggle behave as before, and no capture archive or metadata is created.
  2. With capture enabled, candidate messages from each of the four channels appear with the source/channel label and UTC timestamp, while unrelated channels, statements without question signals, and bot-authored posts do not appear.
  3. Run English, German, Portuguese, and Russian interrogative openings and one `?` message; check that greetings and punctuation do not prevent matching.
  4. Send one question from in-game in a linked channel and verify one `Game` entry despite the Discord mirror; send one Discord question and verify one `Discord` entry. Send candidates through both paths in quick succession and confirm each is a separate complete JSONL line.
  5. Mention another Discord user in a candidate and confirm the archive contains `[user mention]` and no raw numeric mention token; inspect that records have no sender metadata, account IDs, Discord IDs, channel IDs, or message IDs.
  6. With a disposable archive path, append an intentionally incomplete final JSON fragment, restart the local server, and confirm startup truncates only that fragment; all earlier complete lines remain valid and the next candidate appends as a new line.
  7. Restart before the saved deadline and confirm the deadline is unchanged. For expiry, use a disposable session metadata file with a past deadline; confirm the service stays off until the operator deletes only that metadata file, keeps the archive, and explicitly starts a new session.
  8. Make the archive path unwritable in a disposable local configuration; confirm capture logs a failure and disables itself while normal game and Discord chat continue.

  Do not run this pilot on the live VDI server. Keep the archive local for the agreed one- or two-day collection window, then stop capture and review the records manually before using them to expand Sparky.
