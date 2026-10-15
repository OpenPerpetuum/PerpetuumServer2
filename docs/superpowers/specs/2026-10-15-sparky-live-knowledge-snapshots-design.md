# Sparky Live Knowledge Snapshots Design

**Status:** Draft for review
**Date:** 2026-10-15

## Goal

Let Sparky answer questions about changing, public game state—starting with current and past seasons—without querying the live database for each question or rebuilding the static knowledge bundle whenever live data changes.

The design must keep game-server work bounded, keep Dev and Live data isolated, and make the facts auditable in Sparky traces. The same typed snapshot lifecycle should support later providers such as market data without making the LLM or arbitrary player text authoritative.

## Current state and constraints

- Sparky runs on the AI machine. The game server is either Main (local debug, Dev profile) or VDI (Live profile). AI has an offline knowledge bundle and does not connect to the game database.
- Game-to-AI traffic already uses the authenticated HTTPS API over the restricted reverse SSH tunnel. This work adds no externally reachable listener or tunnel.
- The answer service retrieves mechanics and lore evidence from its active profile, then makes one model call. It already records selected source IDs in request traces.
- `SeasonService` refreshes its active season data every five minutes. It already calculates public leaderboard results hourly and calculates final standings at season end. The season points table stores character IDs and points, not historical nickname snapshots.
- Main's local database has no active or completed season data. It can validate transport and retrieval using a synthetic Dev fixture, but only VDI can provide representative Live season data.
- Static item, robot, and lore knowledge remains in the immutable knowledge bundle. Live snapshots supplement that bundle; they do not replace it.

## Approved design decisions

1. Game code builds season snapshots from data already held in `SeasonService` caches and from leaderboard results it already calculates. It adds no per-question database query and no blocking database work to the game update loop.
2. Each snapshot is immutable after construction and published by swapping one reference. The Sparky background worker reads that reference; request handling does not mutate season state.
3. Snapshot delivery uses the existing game-to-AI HTTPS and reverse-tunnel path. AI does not call back into the game server. A player question results in at most one model call.
4. Current live state is replaceable and freshness-limited. Completed public season results are archived as durable, idempotent historical facts. Static bundle refresh remains a separate operation.
5. Season answers use the existing mechanics retrieval pipeline and evidence-grounded answer generation. No season-specific answer template or new top-level intent is required.
6. Dev and Live snapshots are profile-scoped. Main writes to Dev; VDI writes to Live. Data, feedback, and activation in one profile cannot affect the other.
7. Only public season information is in scope. Do not export player IDs, account data, individual progress or claims, or non-public standings.

## Data model and lifecycle

### Snapshot envelope

Every provider update uses a versioned, typed envelope containing:

- provider/type identifier (initially `seasons`);
- profile and schema version;
- source capture time and a content hash;
- bounded current-state sections and, for season finalization, a bounded historical result section;
- an explicit replace-current, refresh-current (metadata-only heartbeat), or finalize-season operation.

The receiver validates the provider, schema version, profile, payload size, record counts, required fields, and timestamp before accepting data. Unsupported fields or malformed records are rejected or omitted with diagnostics; they are never passed through as unrestricted prompt text. Updates are authenticated using the existing service credentials and tunnel.

The AI service stores the latest accepted replaceable snapshot per profile/provider so it survives a service restart. A newer valid snapshot replaces the prior current snapshot. Current snapshots are excluded from answers after a configurable maximum age, initially 15 minutes. The game cache refreshes every five minutes; snapshot capture age and payload size are visible in diagnostics. When the semantic content hash has not changed, the game sends a small metadata-only refresh with the same hash and a new capture time. AI refreshes freshness only if that profile already has the matching content hash; otherwise it asks the game publisher to resend the full current snapshot. The semantic hash excludes capture time and the hash field itself.

Finalization is an idempotent operation keyed by season identity and final-result content hash. It archives only the public final standings and their source timestamp. Replaying the same finalization does not duplicate history. A correction requires a newer authorized finalization for that season and remains traceable.

Snapshot content is evidence, not conversation memory. It is not inserted into player profiles or retained as conversational context. The LLM cannot write, promote, or finalize snapshots.

### Season provider, version 1

The provider may expose the following public facts when they are already available in existing caches or calculations and their labels can be resolved safely:

- current status (active or off-season), season name, description, start/end times, scoring mode, and public scoring rates;
- objective names, descriptions, targets, and public point bonuses;
- public tier names and point thresholds;
- a public current top ten from the existing hourly leaderboard calculation;
- all available completed-season public top tens, including champion, points, season identity, and historical display name where a reliable historical name exists.

Rewards are included only when their display labels can be resolved from existing safe data. Omit an unresolved label rather than inventing a name. Do not add per-character objective progress, claims, private rewards, or any other individual statistics.

At season end, reuse the same filtered final ranking and display names used by the existing public announcement. Do not query or reconstruct the final result solely for Sparky. Because the database stores character IDs and total points but not historical names, a historical display name is included only if available from the final public announcement/result being archived. Otherwise report the result as unavailable rather than substituting a current nickname.

The provider does not export historical standings at startup by scanning the season database. If existing history is needed, an operator-triggered bounded backfill can be designed separately from archived public announcements or another reviewed source. That backfill must apply the same public-data and name-provenance rules.

### Future providers

Other providers use the same envelope, profile isolation, bounds, diagnostics, and freshness controls. A market provider is replaceable current-state evidence with a TTL; it does not archive every order or expose seller/owner identifiers. Historical market analytics are out of scope until separately designed and approved.

## Game-to-AI flow

1. On the existing five-minute season-cache refresh, `SeasonService` builds a candidate current snapshot from the refreshed cache and the most recently computed public leaderboard. It publishes a new immutable reference only when the semantic content hash changes.
2. The game-side Sparky coordinator sends a full snapshot only when that semantic hash changes. Otherwise, it sends a bounded metadata-only refresh so unchanged but still-current facts do not expire after 15 minutes. These updates do not trigger a model call. Season finalization publishes the public final standings through the same path as an idempotent finalize operation.
3. The AI service validates the envelope and stores it under the request's Dev or Live profile. Existing answer requests use that profile's accepted snapshot as retrieval evidence. The precise API DTO/route split is an implementation detail, but it must not introduce an AI callback, a second model call, a new external port, or live-database access from AI.
4. For a question, Sparky converts eligible season sections to ephemeral mechanics evidence passages and uses the existing retrieval and answer-generation path. The model can phrase a concise conversational response, but facts must be supported by the selected evidence.
5. Follow-up questions may reuse the same accepted snapshot while it remains fresh. The snapshot itself is not copied into persistent conversation history.

If current data is absent or too old, Sparky may answer from static offline evidence where appropriate, but it must say that it cannot verify current season state. Archived final standings remain usable as historical evidence after current state expires.

## Retrieval and diagnostics

- Season snapshot sections are searchable through the existing mechanics retrieval category; no new top-level intent is required. Existing route aliases and retrieval settings remain tunable through the Admin Panel.
- Current and historical season facts have distinct stable source identifiers, such as `live-season-current` and `live-season-final:<season-id>`.
- Traces record profile, snapshot provider/schema, capture age, content hash, freshness decision, and selected season source IDs. They do not log player IDs or private data.
- Request traces should distinguish “no snapshot,” “stale snapshot,” “snapshot rejected,” and “no matching season evidence.” Logs must not dump the full snapshot by default.

## Performance and reliability

- Do not add a season database query per Sparky question.
- Build and hash candidates after the existing cache refresh; do not serialize or transmit a full snapshot on every refresh when its hash is unchanged. Send only the small metadata heartbeat in that case.
- Reuse leaderboard rankings already produced by the existing hourly and season-end paths. Do not add leaderboard work to the normal update loop.
- Publish snapshots by immutable-reference replacement. Keep payload sizes and record counts bounded.
- Sending/storing an update must be asynchronous and must not delay game ticks, chat delivery, season transitions, or the existing game request queue. Transient delivery failure retries the latest current snapshot; finalization is retried until acknowledged. The implementation plan must choose a bounded retry/outbox mechanism that fits the server's lifecycle.
- A service restart must not discard accepted snapshots or finalized public results. An invalid update must not replace a previously valid snapshot.

## Security and privacy

- Accept updates only from the authenticated game service over the existing restricted tunnel. Never expose snapshot ingestion directly to the public internet.
- Validate provider and profile against the authenticated client configuration; clients cannot choose another profile by changing request content.
- Allowlist exported fields and cap payload size, history count, text length, and nested collection sizes.
- Do not include character/account IDs, private activity, messages, arbitrary database rows, or caller identity in AI evidence.
- Treat all display text as untrusted evidence. It cannot alter the system prompt, route configuration, credentials, or snapshot lifecycle.

## Development and rollout

### Dev validation

Main's empty season database is not seeded or modified. Add a synthetic, deterministic season snapshot fixture to the Dev evaluation/replay workflow. Use it to validate:

- active season status and objective facts;
- current public top-ten retrieval;
- historical champion and final top-ten retrieval;
- stale/missing snapshot behavior;
- profile isolation, replace semantics, and idempotent finalization;
- traces exposing snapshot age and selected sources.

The fixture validates contract and answer behavior only. It is not evidence that Live data extraction is correct.

### Live validation

Real extraction can be verified only after the updated game-server code is deployed to VDI. That deployment requires a planned game-server restart so the new provider code loads. Do not add an inbound port or change the current tunnel design. Before that deployment, complete Dev fixture validation and prepare a rollback to the previous game-server build. Live rollout timing is an operator decision; this spec does not authorize a production deployment.

## Out of scope

- Fine-tuning or training the model on snapshots.
- Direct AI access to Main or Live databases.
- Rebuilding the static knowledge bundle for ordinary current-season updates.
- Automatically importing arbitrary player feedback, chat, or model-generated claims into trusted knowledge.
- Individual player season histories or private progress.
- Market data extraction, market history, and automated historical analysis.
- VDI deployment or restart as part of this design/specification step.

## Open implementation details

The implementation plan must settle the exact DTO and endpoint shape, durable storage format and atomic replacement method, retry/acknowledgement behavior, and how existing profile credentials identify Dev versus Live. Those choices must preserve the approved constraints above and avoid introducing another model request or network path.
