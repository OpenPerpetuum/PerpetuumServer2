# Sparky Local Service Prototype

## Goal

Provide a locally runnable HTTP service that answers supported item-stat and production-recipe questions using the verified export and the configured LM Studio model, without connecting to the live game server, Discord, or a live database.

## Agreed constraints

- Sparky answers as the Syndicate Technician Chief and responds in English.
- Supported knowledge in this prototype is the existing verified item statistics, ammo, direct production recipes, and recursive full-material data.
- The service uses the existing exporter data and answer generation path; it does not reimplement game calculations or let the model invent missing facts.
- Maximum answer length defaults to 2,000 characters and remains configurable.
- The service is a standalone process/prototype. It does not run inside the game server and has no game or Discord adapter.
- The API binds to loopback by default and requires a configured bearer token on question requests.
- LM Studio's OpenAI-compatible base URL and model are configurable. Non-loopback LM Studio URLs must use HTTPS, matching the existing `LocalModelAnswerer` transport restriction.
- Requests are stateless. The prototype does not persist player identity, origin, or conversation context.
- Do not log question text, generated answer text, bearer tokens, or caller identifiers.
- No live database access; the service loads the previously generated offline export JSON.

## Approaches considered

1. **Add an HTTP mode to `Perpetuum.ItemStatsExporter` (recommended).** Reuse the existing export format, item lookup, answer prompt, and LM Studio client. This keeps one executable and avoids a second implementation of trusted-fact selection.
2. **Create a separate Sparky service project immediately.** This gives a clean deployment boundary but requires extracting exporter contracts and answer logic before their responsibilities are stable.
3. **Put the HTTP endpoint in the game server.** This would mix the prototype with live runtime state and violate the agreed staged rollout, so it is deferred.

The first approach is intentionally a prototype boundary; it can be split into a service project after request/response behavior and deployment needs have been validated.

## Service shape

The existing executable gains an explicit serve mode hosted by ASP.NET Core/Kestrel. It loads one export JSON document at startup, accepts requests through a small HTTP API, performs deterministic item lookup against that document, and calls the existing LM Studio answerer only when the lookup has verified facts to present. Missing or ambiguous items return structured lookup results without invoking the model. This avoids a new service project and reuses the current exporter/answer path; a separate project can be considered after the contract is proven.

Initial endpoints:

- `GET /health` returns `{"status":"ok"}` after configuration and export loading have succeeded. It does not require a bearer token and does not reveal configuration details.
- `POST /v1/answer` requires `Authorization: Bearer <configured-token>`. Request JSON contains only `question`. A successful match returns `{"status":"answered","item":"<display name>","answer":"<text>"}`. A missing item returns `{"status":"not_found"}`. An ambiguous match returns `{"status":"ambiguous","candidates":["<display name>", ...]}`. The API never returns internal definition names. It does not accept account IDs, character IDs, or channel/source metadata.

The listener binds only to loopback in this prototype and defaults to `http://127.0.0.1:5080`; the port is configurable. The prototype does not expose the inbound API to the LAN or internet. If later work requires a non-loopback listener, that change must include an explicit TLS design and network access policy before enabling it. Outbound calls to a non-loopback LM Studio host require HTTPS.

## Configuration and limits

Configuration is supplied outside source control through environment variables or equivalent local configuration:

- bind URL, defaulting to loopback;
- export JSON path;
- LM Studio base URL and model ID;
- LM Studio API token, optional and never logged;
- Sparky API bearer token, required for serve mode and at least 32 characters;
- maximum HTTP request body, default 8 KiB;
- maximum question characters, default 2,000;
- maximum answer characters, default 2,000;
- maximum completion tokens and inference timeout, default 120 seconds;
- maximum concurrent answer requests, default 2.

Serve mode fails at startup if required configuration is missing or malformed, if the export cannot be read, or if the bearer token is shorter than 32 characters. The configured token is compared in constant time. Input and output are bounded. Invalid JSON or overlong questions receive a client error; missing/invalid bearer credentials receive 401; requests beyond the concurrency limit receive 429; LM Studio failures receive a generic service error without returning upstream response bodies or secrets. The listener enforces the concurrency limit so slow inference cannot create unbounded pending work.

## Data flow

1. The caller submits a question over the loopback HTTP API with the bearer token.
2. The service validates content type, request size, question length, and authentication.
3. Existing deterministic item lookup selects only the matching item's verified answer view.
4. Unmatched/ambiguous lookup results are returned directly, with only display-name candidates. Matched facts are passed to the existing LM Studio client.
5. The model produces a concise in-character response; the service applies the configured character cap and returns a structured response.
6. Logs may include request ID, outcome category, elapsed time, and status code only. They exclude user content and secrets.

## Error behavior

- Bad input: 400 or 413 with a stable, non-sensitive error code.
- Missing or invalid token: 401 with no details about the expected token.
- Too many concurrent inferences: 429 with a retry hint.
- Upstream timeout/unavailable/invalid response: 502 or 504 with a generic response; do not retry automatically in this prototype.
- Startup configuration/export errors: fail fast and identify the setting or file problem without printing secret values.
- Cancellation: pass the HTTP request cancellation token through the LM Studio request and stop upstream work when the caller disconnects.

## Validation

No automated tests are to be added or run unless requested. Validate with a Release build and manual requests against a local service and the configured LM Studio instance. The manual checklist will cover health, a verified stats question, a direct recipe question, a recursive full-material question, an unmatched question, invalid/missing bearer token, too-long input, an unavailable model endpoint, answer-length enforcement, and concurrent request rejection. Confirm logs contain no question, answer, token, or user identity.

## Out of scope

- Live game-server changes, server installer/service registration, and game chat/email delivery.
- Discord bot connection or publishing.
- VDM relay, persistent outbound tunnel, VPN, public endpoint, port forwarding, or inbound LAN exposure.
- Player IDs, source tracking, conversation persistence, follow-up context, and account/character database data.
- Lore and other knowledge domains beyond currently verified item statistics and recipes.
- Automatic export refresh or database access.

## Affected areas

- `src/Perpetuum.ItemStatsExporter/`: optional serve mode, HTTP request/response handling, service configuration, and API documentation.
- `docs/codebase/STRUCTURE.md`: describe the prototype's local HTTP mode.
- `docs/superpowers/specs/`: this approved design record.

No game-server gameplay subsystem, live DB state, or existing request-handler contract is changed.
