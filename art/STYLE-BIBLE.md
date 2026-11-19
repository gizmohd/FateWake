# Fatewake Visual Style Bible — v2

## Core look
A late-1990s console adventure with high-color 32-bit-era 2D raster art and a framed, controller-menu feel. Think cinematic illustrated scene, expressive portrait beside the dialogue, and a short list of choices. Keep the interactive graphic-novel pacing. “32-bit” describes the period and rendering style. Deliver RGB or RGBA according to the asset's role.

The [shared concept boards](https://chatgpt.com/share/6afe27f5-593c-83ea-8b70-9f7af7741503) establish the slanted wordmark, violet frames, cyan selection bar, portrait-led dialogue and numbered actions. Apply the selected high-color treatment in [Retro Presentation and Local Narrative AI Requirements](../docs/RETRO-AI-REQUIREMENTS.md) to the artwork. Use the [shared `/play` shell](../src/Fatewake.Web/Components/SiteShell.razor) as the site layout reference. The boards' orbital locations and characters are interface mockup content. Season One opens in recognizable suburban America.

## Scene art and portraits

- Use hand-illustrated forms, fine raster texture, nuanced gradients and crisp silhouettes. Build scenes from distinct foreground, subject and background value groups. Give each scene a clear focal point and keep fine detail away from important silhouettes.
- Shade with layered, directional light and readable shadow detail. Use believable skin tones, cloth, glass and painted surfaces. Aim for an illustration that reads at console-scale and still holds up at delivery size.
- Use realistic anatomy and restrained expressions. Keep approved portrait families in a consistent square crop and scale, with a legible face at small sizes. Target 128×128 or 192×192 logical pixels. Change expressions as the scene calls for them. Wardrobe or injury variants follow recorded story state.
- Generate reusable environments, characters, props and lighting layers with matching perspective, scale and light direction. Leave clean alpha edges on transparent layers and preserve negative space for other layers and the interface.

## Palette

**In-world art.** Use warm-gray interiors, charcoal and asphalt shadows, natural vegetation, weathered neutrals, and cool dawn light with restrained morning amber. Save red and cold electronic blue for an emergency, a display or an anomaly when the scene calls for it.

**Interface.** The current shell uses the Neon Frontier palette with these colors and roles. Keep saturated color concentrated in the frame, masthead and interaction states.

| Role | Current shell color | Use |
| --- | --- | --- |
| Void | `#090817` | Outer background and dark text on selected rows |
| Deep Space | `#17123B` | Panel surface, shaded toward near-black |
| Violet | `#AD72EF` | Outer borders with darker inset bevels |
| Cyan | `#72EAFF` | Selection, menu glyphs and scene readouts |
| Rose | `#FF72C3` | Masthead and wordmark accent |
| Starlight | `#E9E8FF` | Primary interface text |
| Muted lavender | `#BEB9E3` | Secondary labels and narration |
| Amber | `#FFBD76` | Keyboard focus outline |

## Console interface

- Use a full-width starfield masthead with a slanted, high-contrast Fatewake wordmark. Below it, use a narrow game menu and a wider content area. Apply beveled violet borders and dark panel fills throughout the site. Keep chapter and survivor-day labels on the story screen.
- On `/play`, stack the framed scene viewport, portrait and dialogue panel, and story controls. Attribute each line to its speaker. Use legible monospaced text, compact labels and numbered choices. Use a cyan fill with dark text for the active menu row or focused choice, with a separate visible keyboard focus ring.
- At narrow widths, wrap the game menu above the content and stack the story panels. Let text and controls reflow independently of the scene image. Keep captions, choices and status readouts in the application UI as selectable, accessible text.
- Keep motion restrained. Any CRT treatment, typewriter reveal or parallax must preserve text and clues, support reduced motion and offer an immediate way to read the full text.

## Camera language

Use 640×360 as the logical target for widescreen scene presentations. Keep the existing Day 1 9:16 layered masters for the portrait composition. Plan safe crops or letterboxing for other viewports. Keep important clues, faces and contact poses within the central safe area, then inspect the actual crop on a phone and at desktop size. Preserve gradients and edges with high-quality resampling.

Use establishing wides, intimate medium shots, selective close-ups, foreground framing and occasional asymmetric panels. Reserve centered portraits for deliberate reference or identity shots.

## World
Near-future United States, recognizable contemporary technology and architecture. Season One begins in a fictionalized suburban environment inspired by the southeastern United States. No identifiable real private residence. No post-apocalyptic decay at the beginning: the collapse has just started.

## Continuity
Recurring characters must retain facial structure, apparent age, ethnicity, body type, hair, wardrobe base, accessories and distinguishing marks across panels. Clothing may become dirty/damaged only when story state establishes it.
Recurring locations preserve architecture, orientation and major props.
Time-of-day and weather changes must follow story state.

## Emotional tone
Tension through restraint. People look scared, exhausted, suspicious, relieved or determined in human ways. Avoid melodrama. Violence and injury are grounded and generally non-graphic.

## Prompt and approval guidance

Start each art prompt with its subject, framing, story state and approved continuity references. Specify high-color 32-bit-era 2D adventure illustration, naturalistic faces and materials, fine raster detail, controlled gradients, crisp silhouettes and cinematic directional light. Apply scene-specific lighting and composition after the style direction. Keep chrome, sprites and atmospheric overlays separate from the scene when they need to be reused.

Exclude glossy 3D, photorealistic photography,  plastic skin, chunky pixel grids, heavy dithering, RGB565 quantization, neon cyberpunk scenery, fashion posing, superhero costumes, fantasy armor, unexplained accessories or wardrobe changes, excessive gore, and creatures outside canon. Exclude captions, speech balloons, logos and watermarks. A physical in-world display may contain legible text only when the scene explicitly calls for it.

Before approving an asset, review it in the game frame at desktop and mobile sizes. Check that the face, clue or action reads immediately, the layers agree on direction and scale, and character and location continuity match approved references. Use this direction for new assets and explicit versioned replacements. Preserve approved artwork and journal-pinned versions.
