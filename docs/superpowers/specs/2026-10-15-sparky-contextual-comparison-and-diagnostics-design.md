# Sparky Contextual Comparison and Diagnostics Design

**Status:** Approved for implementation planning
**Date:** 2026-10-15

## Goal

Let Sparky compare structured game entities across conversational turns, beginning with items and robots. Improve trace review and JSON exports so an operator can determine what context, interpretation, resolution, and verified facts produced an answer.

For example, after a player asks for Kain's stats, asks about Tyrannos, and then asks “Which one is faster?”, Sparky should compare the two recently discussed robots and answer from verified facts. The answer must not turn that contextual question into a global “fastest robot” lookup.

## Approved Scope and Constraints

- First comparison domains: structured item facts and robot facts.
- Keep a small extension seam for future structured domains. Lore, mechanics, seasons, and other domains are not part of this implementation; they may be added later through domain-specific evidence providers.
- Do not build a universal comparison framework or make comparisons from arbitrary model knowledge.
- Use the existing local LM Studio model for contextual interpretation and natural phrasing. Use code and the approved local knowledge bundle to validate subjects and calculate numeric comparisons.
- Preserve the current AI/Main/Live separation, short-lived source-scoped game-server context, local database-copy workflow, Dev/Live profiles, API security, response limits, and explicit promotion process.
- No Live deployment, live database access, player identity sent to AI, cross-platform identity linking, persistent player memory, hidden reasoning capture, or automatic model training.

## Current Failure

Three Dev traces from the same bundle and profile revision show this sequence:

1. “What are stats for Kain?” returned Kain's top speed as 103.32 kph.
2. “And Tyrannos?” returned Tyrannos's top speed as 93.6 kph.
3. “Which one is faster?” was interpreted as `StructuredRobot/Fastest` and received a generic no-answer response instead of comparing the two robots.

The expected comparison is Kain at 103.32 kph versus Tyrannos at 93.6 kph; Kain is faster by 9.72 kph.

The current flow gives the interpreter conversation history, but the structured robot lookup operates on the current question and interpreted query. The comparison resolver requires two robot matches and explicit comparison routing; comparative follow-up wording may instead take the global-fastest route. The AI request trace is emitted before the answer router applies its corrections, so its route may not match the route used by the lookup. The diagnostic export also omits the search query, entity candidates, and actual conversation context. Together these limits make this class of failure hard to diagnose and prevent reliable conversational comparison.

## Design Alternatives

1. **Prompt-only comparison.** Ask the model to resolve prior entities and write a comparison query, then keep relying on existing lookups. This is a small change, but the model can still omit or confuse the subjects, and exact lookup behavior remains implicit.
2. **Shared comparison contract with domain resolvers (selected).** Have the interpreter identify comparison intent, subjects, and requested attribute from current text and bounded conversation. Validate those references, then delegate exact facts and calculations to item or robot resolvers. This keeps conversation handling reusable while preserving domain accuracy.
3. **General comparison engine for every domain.** Add item, robot, lore, mechanics, season, and future comparison support at once. This expands the first release substantially and risks comparing unsupported or non-comparable facts.

The second approach is selected. It solves the current failure, supports both approved structured domains, and leaves later domains to add their own evidence or calculation provider.

## Comparison Flow

For an addressed request:

1. The game server supplies the current message and its bounded, source-specific conversation context, as it does today. Player and platform IDs remain on the game server.
2. The interpreter identifies whether the turn is a comparison, a continuation, or an unrelated new topic. For a comparison follow-up, it proposes the subjects and requested attribute using recent dialogue. Examples include resolving “those two” to two recently discussed entities and mapping “faster” to maximum speed.
3. The service validates each proposed subject against current player wording, recent conversational entities, and the active knowledge bundle. Model output alone does not establish an entity or a fact. A clear new topic prevents stale entities from being reused.
4. The comparison coordinator dispatches only to a supported structured-domain resolver. The first implementations are item and robot resolvers. Each resolver obtains localized labels, units, and exact values from the active bundle.
5. The resolver compares the requested common field or fields. Numeric differences and ordering are computed in code using stable field identity and compatible units. Items and robots are not compared to one another unless a later provider explicitly declares compatible facts.
6. The answer model receives validated subjects and results and phrases a concise response in Sparky's persona. It may not alter the supplied values or infer missing values.
7. The game server stores the exchange in the same source-scoped, expiring context and delivers it under existing chat, email, or Discord rules.

### Interpretation and comparison criteria

- The model may propose subject references, attribute intent, and whether the player wants one attribute or a broad stat comparison. The response is validated against the supported subject and field catalogs.
- Attribute requests such as “faster” or “more armor” must resolve to a supported field for the selected domain. The implementation must not invent a numeric comparison when no compatible field is present.
- A single-attribute comparison returns that attribute, its values, unit, and which subject leads. “Compare their stats” may return the shared comparable fields, within existing response limits.
- A vague request such as “Which is better?” prompts for a criterion instead of imposing one.
- A question explicitly asking for a global ranking, such as “Which robot is fastest?”, continues to use the global ranking behavior. A contextual “Which one is faster?” compares the identified pair.
- If the context identifies no pair, conflicting pairs, an unsupported subject type, or a missing criterion, Sparky asks a concise clarification. It must not choose arbitrary recent entities.
- If data is absent from the local bundle, Sparky says it cannot verify the comparison from its local records. It does not query a live database or infer the missing value.

### Future domain expansion

The comparison request/result contract should separate conversational reference resolution from domain fact retrieval. A later structured domain can supply comparable, verified fields through a domain resolver. A future lore resolver may compare entities or events only from retrieved passages and should present supported similarities/differences rather than numeric rankings. These future resolvers are extension points, not part of this implementation.

## Trace and Export Diagnostics

The Admin Panel trace view and sanitized JSON export must show enough to distinguish missing context, interpretation errors, entity-resolution errors, structured lookup misses, answer-composition failures, and delivery failures.

### Always-retained metadata

Record bounded metadata for every request, whether or not content capture is enabled:

- actual context message count and character count received by the AI service;
- whether a dialogue summary or pending clarification was present;
- whether the context packet was reduced by a configured budget;
- raw parsed interpreter route and focus, plus the effective route and focus after deterministic corrections;
- resolver category and outcome, such as resolved, ambiguous, not found, unsupported domain, or unsupported attribute;
- comparison subject count, requested attribute category, result category, and selected knowledge source IDs;
- model, bundle, profile revision, elapsed time, finish status, and configured history limits.

The configured context message limit must be labeled separately from actual received message count. An 8-message limit does not mean eight messages were supplied on a request.

### Content-controlled diagnostics

When the profile's content capture is enabled, retain the following bounded fields under the same profile content retention and ACL protections as the question and answer. Include them in JSON export only when the operator also selects **include content**:

- the actual bounded prior turns and summary sent to interpretation;
- the current question, parsed contextualized query, and parsed entity mentions;
- the validated subject names, their resolution origin (current message or conversation context), and comparison attribute;
- verified values and calculated comparison result;
- the final answer and bounded resolver/composer error details suitable for diagnosis.

The export must never include raw model prompts, hidden reasoning, credentials, caller IDs, platform IDs, account/character IDs, or unrelated game logs. With content capture disabled, no question, answer, conversation text, search query, entity names, or structured comparison values are persisted or exported. Metadata and stable public source IDs remain available.

### Export schema and panel presentation

- Add a diagnostic export schema version and preserve the existing fields so older exports remain readable.
- Bound list lengths, text sizes, and total JSON size. Keep the existing export size limit or revise it only with a documented bound.
- Display interpreter and effective route distinctly in the trace UI. Display actual context counts next to configured limits. Show resolved comparison subjects, attribute, verified values, and result when content is included.
- Keep selected source IDs and configuration/bundle identifiers visible as metadata.
- Apply the same Dev/Live profile content-capture, access, retention, and deletion controls to the new diagnostic fields.

## Evaluation and Validation

Add ordered multi-turn Dev evaluation sequences for:

1. Kain stats → “And Tyrannos?” → “Which one is faster?” Expected comparison: Kain 103.32 kph, Tyrannos 93.6 kph, Kain ahead by 9.72 kph.
2. Two named items with a shared numeric field and a comparative follow-up such as “Which has more armor?”
3. A broad “compare their stats” request, checking only common supported fields are returned.
4. An ambiguous follow-up with more than two plausible recent subjects; expected clarification.
5. A vague criterion such as “Which is better?”; expected request for the desired attribute.
6. A clear topic change followed by an ambiguous comparison; expected no stale entity reuse.
7. Explicit global ranking versus contextual pair comparison.
8. Missing field/value or unsupported domain; expected evidence-bound clarification or no-data response.

Replay continues to use isolated ephemeral context for Active and Draft, one new context per evaluation case. It must not deliver messages to game or Discord, inspect online player state, or mutate production configuration.

Trace/export validation must verify:

- actual context counts differ correctly from configured maxima;
- interpreter route and effective post-correction route are both correct;
- context text, queries, entity names, and comparison values are absent when capture is disabled and when export content is excluded;
- bounded context and details appear only when capture and export inclusion are both enabled;
- schema-versioned exports remain within size bounds and contain no prohibited identifiers, secrets, full prompts, or hidden reasoning;
- old trace rows remain readable after additive database migration.

## Risks and Mitigations

- **Wrong contextual pair:** validate subject names against recent source-scoped turns and the active bundle; ask when more than one pair fits.
- **Stale entities after topic changes:** have interpretation mark continuations versus topic changes; clear or ignore comparison candidates when the topic changes.
- **Model chooses wrong metric:** constrain attribute selection to fields provided by a supported resolver and clarify if no field maps safely.
- **Model changes numeric facts:** provide exact result facts and validate answer wording against those facts; use a fallback only when answer generation is unavailable or unusable.
- **Content capture includes private text:** apply existing explicit content-capture controls, export opt-in, retention, ACL, and deletion behavior to context content.
- **Future-domain pressure expands scope:** keep the extension contract small; do not implement lore comparisons, dynamic seasons, or arbitrary cross-domain ranking in this slice.
- **Low-context-window model:** keep the existing bounded context behavior and do not increase inference context requirements to make comparison work.

## Out of Scope

- Lore, faction, agent, event, or real-world comparisons.
- Dynamic live-season, market, loadout, or server-state comparison.
- Item-to-robot comparisons without explicitly compatible structured fields.
- Persistent player profiles or cross-platform identity linking.
- Admin Panel rule authoring for arbitrary formulas, scripts, SQL, or unsupported domains.
- Live deployment or profile promotion.

## Implementation Handoff

The user approved this written spec on 2026-10-15. The implementation plan sequences the comparison contract, item/robot resolvers, trace persistence/export/UI, and Dev replay coverage. Keep changes local to Main and AI development. Do not deploy to Live. Respect all pre-existing uncommitted repository changes and do not commit unless the user explicitly asks.
