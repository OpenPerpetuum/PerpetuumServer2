# Sparky conversation tuning and evaluation

This guide covers tuning and testing the conversation-first Sparky pipeline in the Admin Panel. It does not enable Live or change the game/Discord adapters. Keep experiments in Dev until reviewed.

## Profile settings

Open the Admin Panel, select **Dev**, and load profiles. Save edits as a draft, validate them, replay reviewed cases against Active and Draft, then activate the draft when the comparison looks correct. Profile activation is hot; it does not restart the service. Live stays isolated and changes only through the explicit promotion flow.

The profile editor exposes:

| Setting | Bounds | Default | Effect |
| --- | --- | --- | --- |
| Conversation history messages | Even number, 2–16 | 8 | Maximum user/assistant messages considered. Newest complete exchanges are retained first. |
| Conversation history character budget | 256–8,000 | 4,000 | Combined budget for the dialogue summary and retained complete exchanges. |
| Answer temperature | 0–2 | 0.2 | Wording variation in answer composition. |
| Answer top-p | 0.01–1 | 0.9 | Sampling range in answer composition. |

The values are code-validated even when a request bypasses browser controls. The question interpreter remains deterministic. The game server still owns the short-lived, source-separated player context; the AI API receives the bounded context for a request and does not retain a caller session. The profile budget can reduce incoming context, but cannot restore turns already trimmed by the game server. Caller rank is passed only as a normalized style hint; names, IDs, raw roles, and permissions are not sent to the model.

Profile schema v1 JSON migrates lazily to schema v2 when loaded. Missing new values receive conservative defaults: 8 messages, 4,000 characters, temperature 0.2, and top-p 0.9. Historical revision JSON remains unchanged. New saves write schema v2. A malformed draft is rejected without replacing the current active snapshot. Dev and Live profile records remain separate.

## Review a conversation

1. In **Traces**, select the captured request that should begin the example. The question is prefilled as the first turn.
2. In **Add reviewed evaluation case**, add follow-up turns in the order the player asked them. Move turns to correct their order, or remove an accidental turn.
3. Add expectations only where they are useful. Each turn supports:
   - scope: `Game`, `SmallTalk`, `OutOfScope`, or `Ambiguous`;
   - intent: for example `StructuredItem`, `StructuredRobot`, or `MechanicsLore`;
   - resolved entity and expected source IDs;
   - required facts and forbidden claims;
   - additional expected properties such as `entity:Da Gama` or `focus:Recipe`.
4. Add an optional sequence note describing the behavior under review, then save.

Example sequence:

| Turn | Player text | Useful expectations |
| --- | --- | --- |
| 1 | `What are T3 sensor amplifier stats?` | Game / StructuredItem / ambiguous entity or clarification |
| 2 | `Da Gama` | Game / StructuredItem / Da Gama |
| 3 | `And its full recipe?` | Game / StructuredItem / Da Gama / required material facts |

The selected trace's request ID remains attached as provenance. The sequence text and expectations are stored in the local AI SQLite database. Content retention clears sequence text, notes, and replay output along with other captured content; metadata retention later removes linked records.

## Replay and compare

Choose the selected Dev draft revision and run replay. Every case gets a new context. Active and Draft are run in separate contexts, and turns within one sequence run in order. Replay uses the same bounded interpreter, retrieval, answer, and profile settings as the service, but it never reads online player state or queues game/Discord delivery.

For each turn, compare:

- expected scope and intent against the interpreted scope and route;
- contextualized query and resolved entity;
- selected source IDs and the structured target summary;
- answer wording and any required/forbidden facts;
- model ID, revision, bundle, temperature, top-p, and context budget.

Replay displays the expectations; it does not automatically certify factual correctness. Use a small set first because each turn is run against both revisions and may involve multiple model requests. Feedback labels remain annotations and do not automatically alter routing or answers.

## Compatibility and rollback

Existing stored one-question evaluation cases appear as one-turn sequences. The SQLite change adds nullable columns and leaves old trace, feedback, evaluation, and replay rows intact. Existing evaluations remain available unless their text has expired under the configured retention policy.

If a draft behaves worse, leave it unactivated or use **Rollback profile** to create a new Dev revision from the previous activated configuration. Do not promote to Live as part of local verification. Live uses a separate token and profile and remains untouched by Dev activation or rollback.

## Local verification checklist

Use the Dev service and a few reviewed sequences to check:

- entity clarification followed by a name-only answer and a further recipe follow-up;
- two similar lore names and a pronoun follow-up;
- clear topic change and a case with no prior context;
- supported game politics versus real-world politics refusal;
- small talk and prompt injection;
- ordinary and privileged rank wording without caller identity exposure;
- model timeout or malformed output, where only the fallback path uses a static template.

Inspect traces with content capture both enabled and disabled. With content capture disabled, traces may include route metadata, selected source IDs, model/profile identifiers, and numeric tuning values, but not the question, answer, search query, or entity candidates. Verify output delivery on local Main separately; replay itself never posts to a platform.
