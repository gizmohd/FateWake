# AI Design

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
