# Sparky Knowledge Expansion Design

**Status:** Draft for user review
**Date:** 2026-10-12

## Goal

Expand the existing local Sparky item-answer service into a multilingual, source-grounded game assistant while preserving its current secured path between the development game server, Sparky service, and LM Studio.

The first expansion remains local and non-live. It adds broader game-mechanics and lore answers, bounded in-character small talk, and a repeatable manual knowledge refresh. It does not add persistent player profiles or live database access.

This spec extends the approved game/Discord integration design dated 2026-10-12. That design's transport, authentication, message routing, privacy, and non-live rollout constraints remain in force. This spec replaces its English-only and lore-deferred knowledge scope for the expansion stage.

## Current baseline

The existing exporter builds one English item bundle from a local database copy and a single custom dictionary file. Deterministic lookup covers translated item names, recognized stats, ammo variants, direct recipes, and recursively expanded full materials. The local Sparky service loads the bundle at startup and sends matched verified facts to LM Studio for phrasing.

The game and Discord integration has been exercised against the local debug game server. Basic item answers, recipe follow-ups, full materials, source-platform replies, and offline in-game mail have worked. One Discord request initially produced no reply but later succeeded with the same wording. Local validation must include request and delivery reliability before any live rollout.

## Approved design decisions

### Runtime and privacy boundary

- Continue to use the existing local reverse-SSH and authenticated HTTPS path. Do not add a public listener, third-party inference service, external search, or direct live database access.
- Keep the game server as the owner of short-lived, per-source context and delivery metadata. Send only the question, optional public item context, and optional prior language context to the AI machine. The AI service returns the selected language with the answer result. Verified public facts and retrieved public excerpts are sent only from the AI service to LM Studio.
- Do not send account IDs, character IDs, Discord IDs, online state, account data, or private player data to Sparky or LM Studio.
- Do not give the model arbitrary SQL, database, or filesystem access. Export and index only explicitly approved public game-content sources.
- Keep development and acceptance testing on the local debug server. Live VDI rollout requires a later, separate acceptance decision.

### Supported languages and dictionary behavior

- Support each game language for which a corresponding custom dictionary is included in the knowledge build.
- Treat customDictionary/0.json as the canonical and most complete inventory of public translation keys. Other language dictionaries provide localized values for those keys. Do not infer translations for missing dictionary entries.
- Build language-aware item aliases, display names, stat labels, units, ammo names, recipe component names, and material names from the dictionaries.
- Detect the caller's language on the local AI machine from each message; no third-party detector is used. For very short follow-ups whose language cannot be detected confidently, reuse the prior supported language passed with the existing short-lived context. A clearly different language in a later message updates the language context. Return the selected language to the game server so it can retain it for the next terse follow-up.
- Resolve an item using the selected language's aliases first. If the localized term is absent, also try the English dictionary alias. If an English term resolves the item, answer in the detected caller language using that language's verified facts and labels where available.
- If no supported-language or English alias identifies a term, respond in the caller's language, explain that Sparky could not find the term in that dictionary, and ask the player to try the English term. Do not silently change the whole answer to English.
- Keep recipes complete. Never present a partial localized recipe as complete. If the required recipe names are unavailable for the selected language, use the localized missing-term response and request an English item term rather than omit components or invent translations.
- The model receives an explicit response-language instruction and localized verified facts. It may phrase those facts but may not create translations for game terms that are missing from the supplied dictionary data.
- Maintain a small set of Sparky-specific response templates for missing terms, ambiguous matches, busy/unavailable service, and unsupported questions, keyed by the authoritative game language ID. A language is considered supported only when its dictionary and required templates are both present. Validate this coverage during bundle creation so lookup failures can still receive a localized response before normal answer generation.

### Knowledge sources and answer routing

Keep deterministic data answers separate from retrieval-based answers:

1. **Structured item facts:** Use the exporter and server-derived calculations for stats, compatible ammo, direct recipes, and recursive materials. Recognize common intent wording deterministically where feasible, including full/total/raw materials and follow-ups referring to the last item.
2. **Game mechanics and lore:** Build an offline search index from approved project documentation, mechanics-related server-code behavior, and allowlisted public game-content data from the local database copy. Preserve source/version metadata for every indexed passage. Exclude secrets, credentials, account or character data, authentication and authorization internals, administrative-only behavior, and operational infrastructure details.
3. **Small talk:** Allow short greetings, thanks, and light in-character banter. This path must not present ungrounded mechanics, lore, or current-world facts.

For mechanics and lore questions, retrieve a bounded set of relevant passages and provide only those passages plus the question and response language to LM Studio. Retrieval must work across supported question languages while the source material may be English. The local model may summarize and connect retrieved evidence, but it must not fill gaps with unsupported claims.

Questions that combine structured facts with explanation may use both routes: include exact structured facts and retrieved passages in one answer request. If evidence is absent, conflicting, or too weak, Sparky should state that its sources do not establish the answer or ask a clarifying question.

Do not expose source file paths, internal definition names, SQL identifiers, or implementation details in player-facing responses. Retain source identifiers and bundle versions for local evaluation and troubleshooting without logging question or answer text.

### Context and player memory

- Preserve the existing bounded, volatile context per source identity, with the existing configurable sliding expiry.
- Extend the context entry to include the last resolved public item identity/name and supported response language. Pass only those hints, not the caller identity, to the AI service. Use them for short item follow-ups and terse language-ambiguous messages.
- Do not retain full conversation history or prior generated answers.
- Persistent per-player facts or preferences are out of scope for v1. A future proposal must separately define allowed fact types, purpose, storage location, retention, visibility, correction/deletion, and player controls before implementation.

### Knowledge refresh and rollback

Knowledge changes are delivered by a manual, operator-run refresh after updating the local database copy, dictionaries, documentation, or server-code revision:

1. Rebuild one complete bundle containing structured exports, language dictionaries, and the mechanics/lore search index from the intended matching source revisions.
2. Validate bundle schema, supported language mapping, translation coverage, recipe completeness, index provenance, and exclusion of non-public/account data.
3. Write and validate the new bundle in a staging location. Keep the currently active bundle intact until validation succeeds.
4. Publish the new bundle as the active snapshot and retain the previous known-good snapshot for rollback.
5. Restart only the Sparky service to load the new snapshot. Do not restart the game server for a content refresh.

The bundle manifest must identify the game/content revision, dictionary languages and revisions or hashes, source/index build time, and bundle schema version. The active service should expose a safe local health/diagnostic value that identifies the active bundle version without exposing paths, secrets, or player data.

The first version is manually triggered; no scheduled polling or automatic refresh is required. Refresh tooling should automate the build, validation, staging, activation, and rollback steps as far as practical.

### Small-talk scope

V1 small talk is limited to greetings, acknowledgements, thanks, and brief light banter in supported languages, in Sparky's Technician Chief persona. It does not include open-ended general knowledge, live/current information, personal advice, or persistent relationship simulation. Game questions continue to use the grounded mechanics/lore and structured-data routes.

### Failure behavior

- Unsupported, unresolved, or ambiguous terms receive a short response in the caller's language. Where appropriate, ask for the English term or a more specific item name.
- Missing or incomplete evidence must not produce a guessed game answer.
- Queue saturation, timeout, tunnel failure, certificate/authentication failure, model failure, and Discord delivery failure must be distinguishable in local outcome/timing diagnostics while remaining generic to players.
- A handled request failure should produce a source-platform response when possible. The previous silent Discord attempt must be investigated during local acceptance.
- Keep question/answer content, player identities, credentials, and private player facts out of new logs.
- A failed refresh must leave the current bundle active. A failed activation must permit return to the previous known-good bundle.

## Local acceptance criteria

Before any live rollout:

1. Build a bundle from the local database copy and every configured dictionary language; verify that an absent language or malformed dictionary fails validation clearly.
2. For each supported language, manually check a translated item lookup, stats, direct recipe, full recursive materials, and a short follow-up. Verify key values remain identical across languages while display text is localized.
3. Ask using an English item term in a non-English question and confirm the answer remains in the caller's language. Ask using an untranslated local term and confirm Sparky asks for the English term in the caller's language.
4. Verify missing translations never create a partial recipe or invented label/material.
5. Test representative mechanics and lore questions against source-backed expected answers, including unknown, ambiguous, and conflicting-source cases.
6. Test the small-talk boundary in supported languages, including a greeting, thanks, light banter, and an unrelated factual question.
7. Test refresh success, invalid bundle rejection, service restart, rollback, and confirmation that the game server remains running.
8. Recheck game chat and Discord routing, follow-up context, offline mail, queue/service/model failures, answer length limits, and the prior silent-response scenario locally.
9. Confirm no account/character data is indexed, no player identifiers or message contents are sent to the AI machine, and new logs remain content-free.
10. Do not modify or deploy to the live VDI as part of this design or its local acceptance work.

## Affected areas

- Item exporter and API: multi-dictionary ingestion, locale-aware aliases and answer views, structured intent handling, and response language.
- Offline knowledge builder: allowlisted source extraction, searchable mechanics/lore index, provenance, bundle validation, and refresh/rollback tooling.
- Sparky AI service: language detection/selection, grounded retrieval, bounded small talk, localized fallback behavior, and bundle-version diagnostics.
- Game-server coordinator: retain selected language with existing short-lived context and preserve current source-only response delivery.
- Documentation: update exporter/service operations and the Sparky architecture/integration records after implementation.

## Risks and constraints

- Dictionary filenames may use game language IDs rather than standard locale tags. The exporter must map IDs using an authoritative game source and reject unknown mappings rather than guess.
- Partial dictionaries can make a language appear available while leaving important item or recipe terms untranslated. Bundle validation and per-language acceptance data must make coverage visible.
- Language detection can be uncertain for short messages, mixed-language item names, and transliteration. Reuse recent language context for terse follow-ups and prefer an explicit unsupported/clarification response to silently choosing the wrong locale.
- Server code and public database content can disagree or describe different game revisions. Every bundle must be built from a documented revision set and preserve provenance.
- Local retrieval and expanded prompts can increase inference time and answer length. Keep retrieval bounded and preserve the existing 2,000-character transport cap and request timeout configuration.
