# Fatewake Presentation System

## Direction
Fatewake is presented as an interactive graphic novel / restrained motion comic, not a text-adventure form.

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
