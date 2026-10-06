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
