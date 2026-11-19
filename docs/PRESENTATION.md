# Pathweaver Presentation System

## Brand identity

| Element | Public identity |
| --- | --- |
| Game | Pathweaver |
| Domain | thepathweaver.com |
| Franchise | The Wayfinder Universe |
| Credit | From the universe of The Wayfinder's Heir |
| Tagline | Every choice shapes reality. |
| Genre | Episodic science-fiction narrative adventure |
| Visual brief | 16-bit cinematic pixel art |
| Core mechanic | Player-driven branching realities |
| Players | Pathweavers |
| Daily episodes | Chapters |
| Major arcs | Acts |
| Alternate timelines | Threads |
| Convergence events | Convergences |

Brand introduction: **There are those who find the path. And those who weave it.**

Public copy follows this terminology. Existing `Fatewake` project names, `survivor`/`timeline` storage and transport identifiers, OAuth registrations, browser storage and configured service URLs remain compatibility identifiers, not public branding. Do not silently change callback URLs to the marketing domain.

The new visual brief is a branding direction, not permission to quantize, rescale or overwrite existing images. The current runtime Style Bible remains the explicit generation contract until separately revised; immutable high-resolution masters, validated WebP/optimized PNG derivatives, required alpha and 640x360 logical backgrounds remain mandatory. No bulk image regeneration is part of the site rebrand.

## Current runtime artwork contract
Pathweaver retains an interactive graphic-novel / restrained motion-comic structure, not a text-adventure form. The existing generation contract uses **high-color 32-bit-era 2D adventure art with a 1990s console/VGA-style interface**; the brand brief above records the newly requested cinematic pixel-art direction without pretending the existing pipeline or approved library has already changed. See [Retro Presentation and Local Narrative AI Requirements](RETRO-AI-REQUIREMENTS.md) for resolution, mobile layout, portraits, accessibility, and acceptance criteria. Existing approved assets remain versioned content; style replacements must be explicit, not silent history rewrites.

Scene artwork and portraits follow the chunky pixel-art, neon-twilight direction in [the Style Bible](../art/STYLE-BIBLE.md) (v4), matching the game-screen mockup. Its shared runtime style sections are authoritative; canonical clothing, neutral objects and low-light scenes keep readable color separation within the palette.

Gameplay authority remains outside presentation:
**Canonical State → Scene Projection → Panels/Beats → Player Interaction → CandidateAction → GameEngine**

Artwork, animation, dialogue placement and prose never determine canonical outcomes.

## Hierarchy
- **Chapter** — playable Pathweaver-day narrative unit (the technical episode identifier remains unchanged).
- **Scene** — location/time dramatic unit.
- **Panel** — visual composition within a scene.
- **Beat** — timed/revealed presentation moment within a panel.
- **Interaction** — authored choices and/or free-form action surface.

## Visual principles
1. Mobile portrait is the primary composition.
2. Artwork owns most of the screen; only scene time and signal cues overlay it. Dialogue and choices use separate, readable panels.
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
Every application artwork placeholder must use a stable, server-allowlisted `ArtworkKey`. Seed established authored prompts in `art/prompt-seeds/`; operational prompt revisions are stored in PostgreSQL, and a valid missing prompt follows the durable generate-on-miss path rather than reading a local Markdown file.

A prompt revision is incomplete unless its server-owned context:
- references the current Style Bible;
- includes every recurring character/location continuity sheet visible in the panel;
- states narrative purpose and known story state;
- defines composition/crop safe areas;
- identifies details the player must not learn yet;
- explicitly excludes generated UI/dialogue text unless text is a physical story clue.

When an image becomes approved, future panels featuring the same character/location should use that approved image as a visual reference when supported. Text prompts alone are not sufficient for long-term identity continuity.

### Art asset metadata
Production assets record:
- artwork key
- exact resolved positive/negative prompt and SQL revision when provenance is available
- Style Bible version
- referenced character/location versions
- generation provider/model
- reference asset IDs
- approval status
- continuity notes
- created timestamp

This metadata belongs to presentation provenance, not canonical game state. Historic generations with no recorded prompt provenance are shown as unavailable rather than reconstructed.


## Implemented vertical-slice runtime
The full-window `/play` shell follows the live-game panel in the Neon Frontier design reference: a retro masthead and chapter display, beveled violet frames, a game menu, a large scene viewport, speaker-attributed dialogue, and numbered choices with cyan keyboard and pointer highlights. On narrow screens the menu wraps above the scene. The menu exposes implemented routes only; the reference's art samples and engineering panels are not part of gameplay. The first-day setting and approved artwork are unchanged, and Michelle's dialogue badge reuses her approved scene layer when it is available.

The Day 1 opening is now authored as runtime beats in `DayOneScenes`. `ArtCompositionView` displays the shared `art-generating.svg` placeholder while queued artwork is being prepared. It checks for ready artwork every five seconds, including after a browser image load fails, and renders approved layers as they become available. Explicit failed, cancelled, or disabled generation statuses end polling for those layers. The server persists `SceneKey` and `BeatKey` in the active event state so presentation resumes at the same beat after refresh/return.

Presentation progression never resolves gameplay. Only an Interaction beat submits a CandidateAction to GameEngine. Motion respects `prefers-reduced-motion`.
