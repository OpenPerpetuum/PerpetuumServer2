# Sparky Conversation-First RAG Design

**Status:** Draft for user review

**Date:** 2026-10-14

## Goal

Evolve Sparky from a mostly single-question lookup assistant into a conversational, in-character game assistant. Sparky should interpret each new message in the context of the relevant conversation, retrieve verified game knowledge, and compose a natural answer grounded in that knowledge.

The model should handle flexible language, topic continuity, references, clarifications, and answer phrasing. Application code should provide bounded context, retrieval and exact-data tools, privacy controls, validation, and delivery. It should not encode a separate dialogue branch for every wording or item.

## Relationship to Existing Decisions

This design refines the context and answer-flow sections of the approved Sparky Knowledge Expansion Design dated 2026-10-12. Its conversation behavior replaces the earlier requirements to keep only the last resolved item and not retain recent conversation history. Other approved constraints remain in force, including:

- AI inference remains on the local AI machine through LM Studio.
- Knowledge bundles are built offline from approved local sources; the Sparky service has no live database or general filesystem access.
- Dev testing remains separate from Live, with explicit promotion and rollback.
- Game and Discord conversations remain separate contexts. Player identity and source-platform IDs are never sent to the AI machine.
- No third-party model, public listener, cross-platform identity linking, automatic model training, or live database access is introduced.
- Persistent player memory is not part of this first implementation.

## Design Alternatives

1. **Deterministic dialogue rules.** Add code branches for clarification selections, pronouns, recipe follow-ups, and other anticipated cases. This is predictable for each implemented case but grows into a brittle QA flow and fails on unanticipated phrasing.
2. **Send the whole transcript and knowledge bundle to one model prompt.** This is flexible but exceeds the current model context, weakens source selection, and gives the model too much room to make unsupported claims.
3. **Conversation-first RAG with validated actions (selected).** The model interprets recent dialogue and produces a bounded request for evidence; Sparky retrieves from the approved bundle and exact structured lookups; the model composes a natural answer from that evidence. The application performs only the supported retrieval and lookup actions, validates selections, manages session limits, and enforces privacy and safety rules.

The third approach keeps the model responsible for conversation while retaining the verified item, robot, recipe, and lore sources as authority.

## Conversation Flow

For each addressed request:

1. The game server loads the caller's short-lived conversation context, scoped to that caller and source platform.
2. It submits the new message and a bounded, identity-free context packet to the AI service. The packet contains recent user/assistant turns and a compact continuity summary for older relevant turns. The game server does not submit character IDs, Discord IDs, online status, or delivery metadata.
3. The model interprets the message in context. It may identify a new topic, a continuation, a correction, a reference, or a selection from a pending clarification. It produces a structured interpretation with a contextualized search query and requested answer type; it does not invent facts.
4. Sparky retrieves relevant passages from the approved RAG bundle and/or invokes verified structured lookups for item, robot, stats, ammo, recipe, or other supported data. A candidate selected in response to a clarification must match one of the choices previously presented.
5. The model receives the relevant recent dialogue, retrieved evidence, exact structured facts, persona, and response-language instruction. It writes a concise, natural answer. It may ask a follow-up clarification when the request or evidence is still ambiguous.
6. The game server associates the completed turn with the same source-scoped conversation context and delivers the response only to the originating platform, using the existing chat/email/Discord rules and 2,000-character maximum.

The model can decide how a request relates to prior turns. It cannot bypass retrieval, change verified values, treat a guessed entity as confirmed, or select a clarification choice that was not offered.

## Context Ownership and Limits

- The game-server process owns volatile conversation context, keyed locally by source platform and caller identity. Those keys never leave the game server and are not shared between game and Discord.
- Context contains recent user and assistant turns, supported language, current conversational topic/entity references, and pending clarification details such as the original request and the exact offered candidates.
- When older turns must be pruned, create a compact rolling summary from the dialogue and retain it with the session. Refresh it only when pruning is needed, not on every message. If summary generation fails, keep the latest turns and structured pending state; never delay a response indefinitely for a summary. A summary is dialogue context only, not evidence; factual claims must be retrieved again from approved sources.
- Keep the existing configurable sliding expiry and bounded entry count. Preserve the current 15-minute default unless local evaluation shows a better value. Add configurable maximum turns and a context-content budget, with hard global limits in code.
- Fit the packet to the model's actual context window while reserving capacity for system instructions, retrieved evidence, and the answer. The currently tested model reports a 4,096-token context; the implementation must not assume that the full context window is available for dialogue history.
- Drop the oldest turns first when the budget is reached, summarizing them before removal when possible. Preserve an active pending clarification and its original request, exact choices, and requested answer type until it is answered, replaced by a clear new topic, or expired.
- Clear all context on game-server restart, expiry, explicit reset, or source/caller separation. Do not persist transcripts or caller facts to a database in this phase.
- If context is missing, expired, over budget, or contradictory, Sparky should ask a natural clarification rather than silently applying stale state.

## Model, Retrieval, and Guardrails

### Interpretation

The model receives the new utterance together with relevant conversation turns. It should infer the user's current goal and identify whether the utterance continues the topic, selects a candidate, corrects prior information, or starts a new topic. It should create search queries that include contextually resolved references, such as resolving “Da Gama” to the selected T3 sensor amplifier and retaining the original stats request.

The interpretation contract is structured and bounded. It must distinguish explicit user text from prior assistant statements and retrieved evidence. The model must treat all player messages, dialogue history, and retrieved passages as untrusted content, not as instructions that override Sparky's system rules.

### Retrieval and structured facts

- Search the current utterance and a context-resolved query, rather than relying on only one wording.
- Continue using deterministic server-derived calculations and validated bundle records for exact values such as stats, robot assemblies, ammo, and recipes.
- Continue using bounded RAG over allowlisted mechanics and lore sources. Retrieve evidence for the current conversational question, including references resolved from prior turns.
- Maintain source IDs, bundle versions, and interpretation/retrieval diagnostics for local evaluation. Do not expose internal source paths or database identifiers in player-facing answers.
- The model may connect and summarize the supplied evidence. It must say when the evidence does not establish an answer, and must not fill gaps from general model knowledge.

### Natural answer generation

- Use the model to compose answers in Sparky's persona for supported factual requests and small talk, informed by conversation context and evidence. Avoid canned QA wording for normal answers.
- Preserve exact structured values, units, translations, and recipe completeness. The model may choose readable prose or lists but may not alter the supplied facts.
- Keep localized fallback templates only for service failures, unsupported or missing evidence, and cases where generation cannot produce a safe answer.
- Preserve response-language behavior and transport limits for game chat, in-game email, and Discord.

### Safety and privacy

- The model only receives the bounded conversation packet and public retrieved evidence; it gets no SQL, live database, account, character, filesystem, game-control, or administrative tools.
- Player IDs and platform IDs remain in game-server memory only. No cross-platform identity matching is introduced.
- Do not log dialogue content, model hidden reasoning, or caller identity by default. Existing per-profile content-capture controls govern whether request/answer material is retained for Dev evaluation. Apply the same controls and retention rules to conversation context; never capture it implicitly.
- Context and summaries are untrusted and may not override system instructions or evidence requirements.
- Existing queue, timeout, concurrency, source-only delivery, and 2,000-character safeguards remain effective.

## Caller Memory

Persistent caller facts and preferences are deferred. The first implementation uses only volatile conversation context. The design should keep dialogue-session state separate from any future caller-memory provider so a later phase can define explicitly:

- which fact types may be stored and for what user benefit;
- whether storage is opt-in or opt-out;
- how callers can inspect, correct, or delete stored facts;
- retention and platform/source scope;
- how to avoid storing sensitive, inferred, or unreliable personal information.

No player facts are inferred and saved as durable memory in this design.

## Admin Panel and Evaluation

The current evaluation corpus and replay flow process single questions. Extend it to support ordered multi-turn conversation cases. Each case should store a sequence of user turns and optionally expected properties for selected turns, such as:

- whether the model treated a turn as a continuation, clarification selection, correction, or new topic;
- expected resolved entity/intent or expected source IDs;
- required factual properties and forbidden claims;
- whether the final answer is grounded and appropriately phrased.

Replay a full sequence against active and draft profile revisions using isolated ephemeral context. Show each turn's interpretation, contextualized query, selected sources/structured result, and answer side by side. Replay must not send messages to game chat or Discord, alter production configuration, or use a caller's live context.

Expose AI-side prompt/history budget controls in the profile editor, within hard code-level limits. Keep game-side session expiry, entry capacity, and maximum retained turns in the protected game-server settings because the game server owns caller-scoped context. Changing those client-side settings remains a local configuration operation unless a separately reviewed secure configuration-distribution path is designed. Feedback remains human-reviewed evidence; it does not automatically edit prompts, train weights, or promote changes.

## Local Acceptance Criteria

1. “What are the stats for T3 sensor amplifier?” followed by “Da Gama” selects only a candidate Sparky previously offered and returns that item's verified stats while preserving the original request.
2. A follow-up such as “and its full recipe?” resolves the previously discussed entity and returns complete verified recursive materials.
3. Lore follow-ups such as “what happened to him afterwards?” use the earlier entity and question, retrieve supporting passages, and avoid mixing unrelated named entities from earlier turns.
4. A clear topic change starts a new request without stale entity context leaking into it.
5. A correction such as “I meant the other one” updates the active subject only when the referent is unambiguous; otherwise Sparky asks which one.
6. An expired, missing, or truncated context causes a concise clarification rather than an unsupported answer.
7. Prompt-injection text in a player turn, summary, or retrieved passage does not change system behavior or grant tools/data access.
8. Multi-turn answer generation is natural and in persona while exact structured values and localized labels remain unchanged.
9. Every configured context bound is enforced under concurrent callers; game and Discord sessions never mix, including for a user present on both platforms.
10. AI requests contain no caller or platform IDs. Default logs contain no conversation text or hidden reasoning. Dev capture works only when explicitly enabled and obeys retention.
11. Admin Panel replay can reproduce a saved multi-turn sequence against a draft without changing live sessions or delivering messages.
12. Context clears on expiry and game-server restart; no durable player memory is written.
13. Local Main/AI testing confirms queue behavior, model timeouts, source-only delivery, and the existing 2,000-character response cap. No changes are deployed to Live.

## Implementation Scope

- Game-server Sparky coordinator and context store: bounded turn history, clarification state, context packet creation, and reset/expiry behavior.
- Sparky API request contract: accept bounded context without caller identifiers; return the answer, offered clarification choices, and a bounded optional continuity-summary update so the coordinator can update session state.
- AI service: context-aware interpretation, context-resolved retrieval, grounded answer generation, validation, and safe fallback behavior.
- Admin Panel: ordered multi-turn evaluation examples, isolated sequence replay, and supported context controls.
- Documentation and diagnostics: update Sparky operational docs and trace semantics while preserving privacy and existing profile isolation.

The implementation plan should sequence these changes so each slice can be exercised locally before the next one. It must not include Live VDI deployment.

## Risks and Mitigations

- **Small-model reasoning limits:** the current 4B model may misread long or subtle references. Keep context compact, use constrained interpretation output, validate candidate selection, and evaluate complete transcripts before changing Live.
- **Context crowding and latency:** history, retrieval passages, and generated answers compete for a 4,096-token window. Allocate explicit budgets and measure inference latency on the AI machine.
- **Summary drift:** a summary may omit or distort older details. Keep pending clarifications and entity selections in validated structured state, treat summary text as non-evidence, and retrieve authoritative facts again.
- **Prompt injection:** player text and RAG passages are untrusted. Preserve role separation, prohibit tool escalation, and test adversarial multi-turn cases.
- **Evaluation gaps:** single-question tests cannot catch conversation failures. Multi-turn sequences are required for acceptance and future regression checks.
