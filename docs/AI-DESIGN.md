# AI Design

## Local Narrative Model Requirements

See [Retro Presentation and Local Narrative AI Requirements](RETRO-AI-REQUIREMENTS.md) for the RTX Spark 128 GB target, Qwen3-14B versus 8B evaluation, manuscript-derived training examples, PostgreSQL/pgvector canon retrieval, structured-output validation, and GPU scheduling. These are planned requirements, not active providers or verified hardware capabilities. Model/tool selections remain benchmark- and license-dependent.

## Governing Principle
**The GameEngine decides what happens. AI decides how it is told.**

AI is a narrator, dialogue/content system and intent interpreter—not the authoritative rules engine.

## Resolution

    var outcome = gameEngine.Resolve(choice, state);
    var narrative = await narrator.RenderAsync(outcome, playerContext);

## AI May
- Render deterministic outcomes as personalized prose.
- Generate contextual dialogue within supplied facts.
- Interpret free-form actions into candidate intents.
- Reference relevant supplied player history.
- Vary tone without changing canonical state.

## AI Must Not
- Invent authoritative resources, deaths, relationships or world facts.
- Bypass event prerequisites.
- Grant impossible actions merely because prose sounds plausible.
- Rewrite canonical history.

## Free-Form Pipeline
Player text → safety/content validation → intent classification → candidate game action → deterministic validation/resolution → Wake generation → AI narrative rendering.

## Journal Understanding
AI may analyze player-authored journal entries as a source of **candidate player context**, including observations, theories, entities, relationships, promises, concerns, questions and future intentions that authored choices or telemetry may not capture.

Journal understanding must preserve provenance. Store the player's original text independently from derived structured observations. Derived observations should include interpretation metadata such as category, confidence, referenced entities/events and source entry/day.

A player belief is not automatically a world fact. For example, `I think Marcus stole the medicine` may create a structured belief or suspicion associated with the player, but it cannot set `Marcus.StoleMedicine = true`. The deterministic engine or subsequent evidence decides canonical truth.

This separation enables later narrative responses to what the player noticed without allowing generative interpretation to rewrite world state.

## Generated Content Reuse and AI Cost Control
AI-generated content is treated as a reusable asset when its inputs and applicability can be identified safely.

Before requesting generation, the application builds a normalized **generation context** containing the content purpose, canonical narrative facts, relevant knowledge state, tone/audience constraints, locale, rules/content version, prompt-template version and other inputs that materially affect correctness. A stable fingerprint of that context is used for exact reuse.

Reuse levels:
1. **Exact reuse** — identical normalized generation context may reuse a previously approved/generated artifact without another model call.
2. **Parameterized/template reuse** — recurring content can be stored as a reusable authored/generated template with safe variable substitution.
3. **Semantic candidate reuse** — later, embeddings/search may locate similar prior assets, but similarity alone must not authorize reuse. Applicability must be validated against current canonical facts and constraints.
4. **Regeneration** — generate when no valid asset applies or when prompt/rules/content versions require a fresh render.

Decision/action trees generated with AI should be persisted as versioned content definitions or candidate content assets where appropriate. Once validated/published, deterministic game logic references the stored version rather than asking AI to recreate the tree for every player.

Generated assets retain provenance: provider/model, prompt/template version, normalized-context fingerprint, generation time, token/cost metadata when available, validation status, usage count and supersession/version information.

Personal or secret state must be part of applicability. Content generated from one survivor's private knowledge, journal, relationships or hidden state cannot be reused for another survivor merely because the prose is similar.

A cache miss is never permission for AI to decide canonical state. Reuse and generation remain presentation/content-production concerns around deterministic rules.

## Artwork Prompt Authoring

The optional SQL-backed artwork prompt authorer is a presentation-only text operation. It uses OpenAI Responses with GPT-6 Luna, reasoning effort `none`, strict structured output and no tools. It receives immutable server-owned scene/layer metadata, canonical visual context, style, framing and continuity—not freeform player text, profile data or hidden future facts. Output is bounded and validated, then stored as an immutable SQL prompt revision before a successor image job is created. The feature is disabled by default and does not enable or replace the separately configured image-generation provider.
