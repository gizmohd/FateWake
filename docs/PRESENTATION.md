# Fatewake Presentation System

## Direction
Fatewake retains an interactive graphic-novel / restrained motion-comic structure, not a text-adventure form. The current target rendering is **high-color 32-bit-era 2D adventure art with a 1990s console/VGA-style interface**. See [Retro Presentation and Local Narrative AI Requirements](RETRO-AI-REQUIREMENTS.md) for resolution, mobile layout, portraits, accessibility, and acceptance criteria. This supersedes the earlier 16-bit pixel-art direction. Existing approved assets remain versioned legacy content; apply the new style to new assets and explicit versioned replacements rather than silently mixing styles or overwriting history. This direction is not yet fully implemented.

Gameplay authority remains outside presentation:
**Canonical State → Scene Projection → Panels/Beats → Player Interaction → CandidateAction → GameEngine**

Artwork, animation, dialogue placement and prose never determine canonical outcomes.

## Hierarchy
- **Episode** — playable survivor-day narrative unit.
- **Scene** — location/time dramatic unit.
- **Panel** — visual composition within a scene.
- **Beat** — timed/revealed presentation moment within a panel.
- **Interaction** — authored choices and/or free-form action surface.

## Visual principles
1. Mobile portrait is the primary composition.
2. Artwork owns most of the screen; text overlays are concise.
3. Dialogue is character-attributed and visually distinct from narration.
4. Choices appear at dramatic decision points rather than permanently occupying the UI.
5. Major anomalies may deliberately break the normal panel language.
6. Motion is restrained: fades, parallax, crops, environmental effects and expression swaps rather than constant animation.
7. Text-only/accessibility rendering remains possible from the same scene data.

## Asset strategy
Prefer reusable layered assets over generating every frame:
- environment/background
- character
- pose
- expression
- foreground
- lighting/weather/effect
- optional special-event illustration

Generated visual assets require provenance/version metadata and may be reused when scene applicability is valid. Character identity/style continuity takes priority over novelty.

## Day 1 reference
The Day 1 6:17 opening is the reference implementation for Fatewake's visual language:
1. phone/clock cold open
2. silent house / loss of service
3. exterior reveal
4. Maya + injured stranger + Eli
5. first interaction
6. consequence panel
7. radio anomaly
8. 6:43 shutdown
9. recap


## Placeholder Prompt Contract
Every application artwork placeholder must use a stable `ArtworkKey`. Before or with introduction of a new key, add `art/prompts/{ArtworkKey}.md`.

A prompt file is incomplete unless it:
- references the current Style Bible;
- references every recurring character/location continuity sheet visible in the panel;
- states narrative purpose and known story state;
- defines composition/crop safe areas;
- identifies details the player must not learn yet;
- explicitly excludes generated UI/dialogue text unless text is a physical story clue.

When an image becomes approved, future panels featuring the same character/location should use that approved image as a visual reference when supported. Text prompts alone are not sufficient for long-term identity continuity.

### Art asset metadata
Production assets should eventually record:
- artwork key
- prompt version
- Style Bible version
- referenced character/location versions
- generation provider/model
- reference asset IDs
- approval status
- continuity notes
- created timestamp

This metadata belongs to presentation provenance, not canonical game state.


## Implemented vertical-slice runtime
The Day 1 opening is now authored as runtime beats in `DayOneScenes`. `ArtCompositionView` displays the shared `art-generating.svg` placeholder while queued artwork is being prepared. It checks for ready artwork every five seconds, including after a browser image load fails, and renders approved layers as they become available. Explicit failed, cancelled, or disabled generation statuses end polling for those layers. The server persists `SceneKey` and `BeatKey` in the active event state so presentation resumes at the same beat after refresh/return.

Presentation progression never resolves gameplay. Only an Interaction beat submits a CandidateAction to GameEngine. Motion respects `prefers-reduced-motion`.
