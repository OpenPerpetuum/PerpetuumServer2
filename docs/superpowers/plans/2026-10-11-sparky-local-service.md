# Sparky Local Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** Add a loopback-only HTTP service mode to `Perpetuum.ItemStatsExporter` that answers supported item stats and recipe questions through configured LM Studio using the verified offline export.

**Architecture:** Keep the current exporter executable and add a `--serve <export.json>` mode hosted by ASP.NET Core/Kestrel. A small service configuration object validates environment settings; a service class owns loopback HTTP endpoints, bearer authentication, input limits, concurrency, logging, lookup, and error mapping. The existing deterministic lookup and LM Studio answerer remain the source of item facts and model behavior.

**Tech Stack:** .NET 8, C# 12, ASP.NET Core/Kestrel via the shared framework, `System.Text.Json`, existing `ItemStatsLookup` and `LocalModelAnswerer`.

**Spec:** `docs/superpowers/specs/2026-10-11-sparky-local-service-design.md`

## Global Constraints

- Windows-only, .NET 8, x64-compatible; do not add a NuGet dependency for HTTP hosting.
- No game-server, Discord, live-database, VDM relay, or inbound LAN/internet integration.
- Bind only to `127.0.0.1`; default URL is `http://127.0.0.1:5080`.
- `SPARKY_API_TOKEN` is required and at least 32 characters; compare bearer tokens in constant time.
- Load the offline export once at startup; do not accept identity, origin, or conversation fields.
- Cap request bodies at 8 KiB, question length at 2,000 characters, answer length at 2,000 characters, and concurrent inferences at 2 by default.
- Non-loopback LM Studio endpoints must use HTTPS; never log question text, answer text, tokens, or caller identifiers.
- Automated tests are excluded by the approved spec. Use a Release build and the spec's manual HTTP validation checklist.
- Do not commit changes unless explicitly asked, per `CLAUDE.md`.

## File Map

- Modify `src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj` to reference the ASP.NET Core shared framework without adding a package.
- Modify `src/Perpetuum.ItemStatsExporter/Program.cs` to dispatch `--serve <export.json>` while preserving existing export, lookup, and ask modes.
- Create `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs` for environment parsing and fail-fast validation.
- Create `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs` for Kestrel setup, API endpoints, authentication, bounds, concurrency, and safe logging.
- Create `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs` for request/response JSON DTOs that exclude internal definition names.
- Modify `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs` to accept cancellation and a per-request inference timeout while preserving CLI defaults.
- Modify `src/Perpetuum.ItemStatsExporter/README.md` with run configuration, PowerShell examples, and manual validation instructions.
- Modify `docs/codebase/STRUCTURE.md` to describe the local service mode.

## Review Focus

The approved spec excludes automated tests. Pin these input/failure cases with the manual requests in Task 4:

1. Missing, malformed, or too-short bearer token must prevent inference and return 401.
2. Missing/malformed export or required configuration must fail startup without printing secret values.
3. Non-loopback LM Studio HTTP URL must be rejected; LM Studio failures must not expose upstream bodies or tokens.
4. Oversized JSON, invalid JSON, and questions over the configured character limit must not reach lookup or inference.
5. Request cancellation, inference timeout, and concurrency saturation must produce no hung request or secret/content-bearing logs.

---

### Task 1: Add service configuration and serve-mode startup

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/Perpetuum.ItemStatsExporter.csproj`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyServiceOptions.cs`

**Interfaces:**
- `SparkyServiceOptions.Load(string exportPath)` reads and validates `SPARKY_SERVICE_URL`, `SPARKY_API_TOKEN`, `SPARKY_LMSTUDIO_BASE_URL`, `SPARKY_LMSTUDIO_MODEL_ID`, `SPARKY_LMSTUDIO_API_TOKEN`, `SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS`, `SPARKY_LMSTUDIO_TIMEOUT_SECONDS`, `SPARKY_MAX_REQUEST_BODY_BYTES`, `SPARKY_MAX_ANSWER_CHARACTERS`, `SPARKY_MAX_QUESTION_CHARACTERS`, and `SPARKY_SERVICE_MAX_CONCURRENCY`.
- `SparkyServiceOptions` exposes the resolved export path, validated loopback URL, secrets, limits, model endpoint, and model ID to `SparkyHttpService`.
- `Program.Main` recognizes exactly `--serve <export.json>` and awaits the host lifetime; all existing command modes retain their current parsing and behavior.

```csharp
internal sealed record SparkyServiceOptions(
    string ExportPath,
    Uri ServiceUri,
    string ApiToken,
    string ModelBaseUrl,
    string ModelId,
    string? ModelApiToken,
    int MaxRequestBodyBytes,
    int MaxQuestionCharacters,
    int MaxAnswerCharacters,
    int MaxCompletionTokens,
    TimeSpan InferenceTimeout,
    int MaxConcurrentRequests);
```

- [ ] Add a `FrameworkReference` to `Microsoft.AspNetCore.App` in the exporter project; do not add a NuGet package.
- [ ] Implement typed environment parsing with these defaults: service URL `http://127.0.0.1:5080`, request body 8192 bytes, question 2000 chars, answer 2000 chars, completion tokens 700, timeout 120 seconds, max concurrent inference 2. Require export path, model ID, valid HTTP(S) model base URL, and an API bearer token at least 32 characters long. Reject malformed service URLs and any address that is not the IPv4 loopback address; reject nonpositive limits, malformed model URLs, URL user-info, query strings, and fragments; never include secret values in configuration errors.
- [ ] Add `SPARKY_MAX_REQUEST_BODY_BYTES` with an 8192-byte default and pass the validated value to Kestrel.
- [ ] Add `--serve <export.json>` dispatch that resolves the JSON path and deserializes `ExportDocument` once before creating the host; report the path for missing/unreadable export files without dumping export contents.
- [ ] Preserve `--answer`, `--ask`, `--project-existing`, and export command modes exactly; update help text with serve syntax and required environment names.

**Manual check:** Build the exporter in Release configuration and invoke help plus each existing CLI mode's argument parsing to confirm the new branch does not shadow current commands. Do not invoke LM Studio in this task.

### Task 2: Implement the authenticated loopback HTTP contract

**Files:**
- Create: `src/Perpetuum.ItemStatsExporter/SparkyApiModels.cs`
- Create: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`

**Interfaces:**
- `SparkyApiRequest` has one nullable `Question` property.
- `SparkyApiResponse` represents either `answered` with `Item` and `Answer`, `not_found`, or `ambiguous` with display-name-only candidates.
- `SparkyHttpService.RunAsync(ExportDocument export, SparkyServiceOptions options, CancellationToken cancellationToken)` starts Kestrel on `http://127.0.0.1:{port}` and owns the HTTP lifetime.

```csharp
internal sealed record SparkyApiRequest(string? Question);

internal sealed record SparkyApiResponse(
    string Status,
    string? Item = null,
    string? Answer = null,
    IReadOnlyList<string>? Candidates = null);
```

- [ ] Configure Kestrel's maximum request body to the configured byte limit and listen on the validated IPv4 loopback URL only.
- [ ] Implement `GET /health` returning only `{"status":"ok"}` after startup validation and export loading.
- [ ] Implement `POST /v1/answer`: require JSON and `Authorization: Bearer ...`; reject invalid/missing auth with 401 before calling lookup. Compare UTF-8 token bytes using `CryptographicOperations.FixedTimeEquals` after verifying equal length.
- [ ] Validate that the request body deserializes to `SparkyApiRequest`, `Question` is nonempty, and its trimmed content does not exceed the configured length. Return stable generic 400/413 errors.
- [ ] Call `ItemStatsLookup.Resolve(question, export.Items, export.ProductionRecipes)`. Convert `not_found` and `ambiguous` results into the public DTO without including `DefinitionName`; do not call LM Studio for either result.
- [ ] Add a `SemaphoreSlim` concurrency gate. If no slot is immediately available, return 429 with `Retry-After: 1`; release the slot in `finally` after the inference completes or fails.
- [ ] Log only request ID, outcome, elapsed time, and HTTP status. Do not install request-body logging or log exception messages that may include request content or secrets.

**Manual check:** Start the API with a representative export and use PowerShell `Invoke-RestMethod` for health, missing/invalid token, malformed JSON, too-long question, not-found, and ambiguous cases. Confirm no request reaches model inference for rejected or unmatched input and inspect console logs for content leakage.

### Task 3: Connect matched requests to LM Studio safely

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/LocalModelAnswerer.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/SparkyHttpService.cs`
- Modify: `src/Perpetuum.ItemStatsExporter/Program.cs`

**Interfaces:**
- Extend `LocalModelAnswerer.AnswerAsync` with `TimeSpan timeout` and optional `CancellationToken cancellationToken = default`.
- The method keeps the existing request content/system prompt and applies timeout/cancellation to `HttpClient.SendAsync` without placing tokens in URLs or logs.
- The HTTP handler maps a normal result to `answered`, request disconnect to cancellation/no further work, timeout to 504, and other upstream failures to a generic 502.

Call the answerer with all configured limits and the request cancellation token:

```csharp
var answer = await LocalModelAnswerer.AnswerAsync(
    options.ModelBaseUrl,
    options.ModelId,
    request.Question,
    lookup.Item,
    options.MaxAnswerCharacters,
    options.MaxCompletionTokens,
    options.InferenceTimeout,
    httpContext.RequestAborted);
```

- [ ] Set the shared `HttpClient.Timeout` to infinite and create a linked cancellation source per call; apply the configured inference timeout and pass the linked token into `SendAsync`.
- [ ] Preserve `--ask` timeout behavior at 120 seconds by passing its current default explicitly; preserve its existing token and answer-length configuration.
- [ ] When the lookup status is `found`, pass only that matched `ItemStatsLookupAnswer` to the existing answerer with configured model, completion tokens, timeout, and answer cap.
- [ ] Map model timeout to 504 and other `HttpRequestException`, invalid response, or empty answer to generic 502 JSON. Do not return upstream response bodies, exception details, or credentials to the caller.
- [ ] If `HttpContext.RequestAborted` is signaled, stop upstream inference and do not attempt to write a response to the disconnected caller.
- [ ] Apply the answer-character cap before constructing the HTTP response and use the display name, not the internal definition name, in the response.

**Manual check:** With a reachable LM Studio endpoint, ask a question for a known weapon stats item, a direct recipe, and a recursive full-material list. Confirm results match the CLI answer path and remain at or below the configured character limit. Use an unreachable endpoint and a short timeout to confirm generic 502/504 responses and clean cancellation.

### Task 4: Document operation and validate the complete prototype

**Files:**
- Modify: `src/Perpetuum.ItemStatsExporter/README.md`
- Modify: `docs/codebase/STRUCTURE.md`

- [ ] Document the serve command, all environment variable names/defaults, loopback-only binding, bearer-token generation guidance, and the HTTPS requirement for non-loopback LM Studio endpoints.
- [ ] Add PowerShell examples that set secrets only in the current process environment, start the service, call health, and submit an authenticated sample item question. Do not place real tokens in repository files or command examples.
- [ ] Document expected response shapes for `answered`, `not_found`, `ambiguous`, and errors, including the default 2,000-character response cap.
- [ ] Update the project structure table/description to identify the exporter as both an offline export/CLI tool and optional local HTTP prototype.
- [ ] Run a Release build of the exporter; do not run automated tests.
- [ ] Complete the approved spec's manual checklist: health, stats, direct recipe, recursive materials, unmatched item, wrong/missing token, overlong and oversized input, unavailable model, answer cap, concurrency rejection, and safe logs.
- [ ] Inspect `git diff --check` and confirm no test files, live-server files, account/character data, or generated exports were added by this service change.

## Execution Notes

- The repository already contains user data and prior Sparky work in the working tree. Inspect `git status --short` before editing and do not clean, reset, or overwrite unrelated files.
- Do not commit; the repository instruction says commits require an explicit user request.
- No game-server gameplay subsystem, live DB state, installer, Discord adapter, outbound VDM relay, LAN listener, or persisted player context is part of this implementation.
