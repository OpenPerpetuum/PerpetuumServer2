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
- The game server may send a normalized, source-specific caller rank for in-character addressing. It sends no permission lists, account IDs, character IDs, or Discord IDs; rank never grants tools or access.
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
2. It submits the new message and a bounded context packet to the AI service. The packet contains recent user/assistant turns and a compact continuity summary for older relevant turns. It may include a normalized caller rank claim used only to shape in-character wording. It does not include character IDs, Discord IDs, online status, permission lists, or delivery metadata.
3. The model interprets the message in context. It may identify a new topic, a continuation, a correction, a reference, or a selection from a pending clarification. It produces a structured scope decision, contextualized search query when needed, and requested answer type; it does not invent facts.
4. For an in-game request, Sparky retrieves relevant passages from the approved RAG bundle and/or invokes verified structured lookups for item, robot, stats, ammo, recipe, or other supported data. Small talk does not trigger factual retrieval. Out-of-scope and ambiguous requests do not receive factual evidence; they go to refusal-only or clarification-only response generation. A candidate selected in response to a clarification must match one of the choices previously presented.
5. For an in-game answer, the model receives the relevant recent dialogue, retrieved evidence, exact structured facts, persona, and response-language instruction. For small talk, refusal, or clarification, it receives only the context and response constraints needed for that outcome. It writes a concise, natural response. It may ask a follow-up clarification when the request or evidence is still ambiguous.
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

### Conversation scope and refusal behavior

- Sparky's factual domain is the game and its fictional world, using approved game knowledge and verified structured lookups. In-game politics may be discussed when the approved sources support the answer.
- Harmless social small talk remains allowed. Sparky must not discuss real-world political or other configured sensitive topics. It should give a brief, in-character refusal and redirect to the game; when it is unclear whether the caller means the game or the real world, it should ask.
- The base prohibition on real-world political discussion is code-owned and cannot be disabled by editing persona text. Configuration may tune refusal wording or add stricter exclusions, but not weaken the base rule.
- The model returns a bounded scope classification as part of interpretation. Once selected, that outcome is fixed for the turn: game questions may retrieve approved evidence; harmless small talk may receive a conversational answer; out-of-scope requests receive a refusal-only generation request with no factual evidence; ambiguous requests receive a clarification-only generation request. Validate that the composed response follows the selected outcome; retry with tighter constraints if needed. The model cannot override this policy by prompt or retrieved content.
- This boundary must not rely on persona wording alone. Evaluation cases should include direct, indirect, multilingual, and context-dependent attempts to move a conversation into prohibited real-world topics. If the scope is uncertain, fail closed by asking or refusing rather than answering from general model knowledge.

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

- Use the model to compose all normal user-facing responses in Sparky's persona: factual answers, small talk, clarifications, evidence gaps, and policy refusals. Supply the model with the validated outcome and constraints, so it can phrase a natural answer without inventing facts or changing the decision. Avoid canned QA wording while the model is available.
- Preserve exact structured values, units, translations, and recipe completeness. The model may choose readable prose or lists but may not alter the supplied facts.
- Use localized static fallback templates only when Sparky or its model is unavailable, times out, or cannot produce a usable response after bounded recovery. Missing evidence, ambiguity, and policy refusal are normal model-generated conversational outcomes, not template fallbacks.
- Preserve response-language behavior and transport limits for game chat, in-game email, and Discord.

### In-character caller hierarchy

- Use the hierarchy `Technician < Agent < Officer < CEO < Command Director`; Sparky is a Technician.
- The game and Discord adapters independently map their authoritative permission/role data to one normalized rank. Ordinary players default to Agent unless a more specific mapping applies. Explicitly configured administrator roles may map to Command Director; for multiple matches, use the highest configured rank.
- The normalized rank is a style hint only. It cannot grant game permissions, expose additional information, or change retrieval access. Raw permission data and platform identifiers stay on the game server.
- Make rank-to-addressing guidance configurable with the persona, including localized or neutral forms of address. Do not infer a caller's gender; neutral wording is the default when no explicit preference is configured.
- Rank is resolved independently for each source. Do not match Discord and game callers by similar nicknames.

### Safety and privacy

- The model only receives the bounded conversation packet and public retrieved evidence; it gets no SQL, live database, account, character, filesystem, game-control, or administrative tools.
- Player IDs and platform IDs remain in game-server memory only. No cross-platform identity matching is introduced. The normalized rank claim is non-identifying and source-specific.
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
- provenance and freshness for facts such as corporation or achievements, which should come from authoritative game data; preferences should be stated or confirmed by the caller rather than silently inferred.
- whether to support a verified game/Discord account link. Similar display names are not sufficient, and the current phase does not link identities.

No player facts are inferred and saved as durable memory in this design.

## Admin Panel and Evaluation

The current evaluation corpus and replay flow process single questions. Extend it to support ordered multi-turn conversation cases. Each case should store a sequence of user turns and optionally expected properties for selected turns, such as:

- whether the model treated a turn as a continuation, clarification selection, correction, or new topic;
- expected resolved entity/intent or expected source IDs;
- required factual properties and forbidden claims;
- whether the final answer is grounded and appropriately phrased.

Replay a full sequence against active and draft profile revisions using isolated ephemeral context. Show each turn's interpretation, contextualized query, selected sources/structured result, and answer side by side. Replay must not send messages to game chat or Discord, alter production configuration, or use a caller's live context.

Expose AI-side prompt/history budget and supported inference settings in the profile editor, within hard code-level limits. Use Dev evaluation sequences to compare settings and record the model/config revision used for each replay. This is runtime inference tuning, not weight fine-tuning. Keep game-side session expiry, entry capacity, and maximum retained turns in the protected game-server settings because the game server owns caller-scoped context. Changing those client-side settings remains a local configuration operation unless a separately reviewed secure configuration-distribution path is designed. Feedback remains human-reviewed evidence; it does not automatically edit prompts, train weights, or promote changes.

## Local Acceptance Criteria

1. “What are the stats for T3 sensor amplifier?” followed by “Da Gama” selects only a candidate Sparky previously offered and returns that item's verified stats while preserving the original request.
2. A follow-up such as “and its full recipe?” resolves the previously discussed entity and returns complete verified recursive materials.
3. Lore follow-ups such as “what happened to him afterwards?” use the earlier entity and question, retrieve supporting passages, and avoid mixing unrelated named entities from earlier turns.
4. A clear topic change starts a new request without stale entity context leaking into it.
5. A correction such as “I meant the other one” updates the active subject only when the referent is unambiguous; otherwise Sparky asks which one.
6. An expired, missing, or truncated context causes a concise clarification rather than an unsupported answer.
7. Prompt-injection text in a player turn, summary, or retrieved passage does not change system behavior or grant tools/data access.
8. Real-world sensitive or political requests receive a natural in-character refusal; supported in-game political questions remain answerable from retrieved evidence; ambiguous scope prompts for clarification.
9. Answers, clarifications, evidence-gap responses, and refusals are model-composed while the model is available. Static fallback text is used only when service/model generation is unavailable or unusable.
10. The caller's authoritative game or Discord role maps to the expected normalized rank and changes wording only; raw IDs and permission lists never reach AI.
11. Multi-turn answer generation is natural and in persona while exact structured values and localized labels remain unchanged.
12. Every configured context bound is enforced under concurrent callers; game and Discord sessions never mix, including for a user present on both platforms.
13. AI requests contain no caller or platform IDs. Default logs contain no conversation text or hidden reasoning. Dev capture works only when explicitly enabled and obeys retention.
14. Admin Panel replay can reproduce a saved multi-turn sequence against a draft without changing live sessions or delivering messages.
15. Context clears on expiry and game-server restart; no durable player memory is written.
16. Local Main/AI testing confirms queue behavior, model timeouts, source-only delivery, and the existing 2,000-character response cap. No changes are deployed to Live.

## Implementation Scope

- Game-server Sparky coordinator and context store: bounded turn history, clarification state, source-specific rank normalization, context packet creation, and reset/expiry behavior.
- Sparky API request contract: accept bounded context without caller identifiers; return the answer, offered clarification choices, and a bounded optional continuity-summary update so the coordinator can update session state.
- AI service: context-aware interpretation, conversation-scope enforcement, context-resolved retrieval, grounded natural answer generation, validation, and service-unavailability fallback behavior.
- Admin Panel: ordered multi-turn evaluation examples, isolated sequence replay, and supported context controls.
- Documentation and diagnostics: update Sparky operational docs and trace semantics while preserving privacy and existing profile isolation.

The implementation plan should sequence these changes so each slice can be exercised locally before the next one. It must not include Live VDI deployment.

## Risks and Mitigations

- **Small-model reasoning limits:** the current 4B model may misread long or subtle references. Keep context compact, use constrained interpretation output, validate candidate selection, and evaluate complete transcripts before changing Live.
- **Context crowding and latency:** history, retrieval passages, and generated answers compete for a 4,096-token window. Allocate explicit budgets and measure inference latency on the AI machine.
- **Summary drift:** a summary may omit or distort older details. Keep pending clarifications and entity selections in validated structured state, treat summary text as non-evidence, and retrieve authoritative facts again.
- **Prompt injection:** player text and RAG passages are untrusted. Preserve role separation, prohibit tool escalation, and test adversarial multi-turn cases.
- **Scope misclassification:** a small model may mistake indirect or multilingual real-world topics for game discussion. Fail closed when uncertain and include such variations in replay cases.
- **Rank mapping mistakes:** incorrect Discord-role or game-permission mappings could cause awkward forms of address. Rank affects wording only; unmapped callers use Agent.
- **Tuning regressions:** inference changes may improve one answer while degrading others. Keep settings bounded and versioned, and compare the full evaluation set in Dev before promotion.
- **Evaluation gaps:** single-question tests cannot catch conversation failures. Multi-turn sequences are required for acceptance and future regression checks.
