# Fatewake Visual Style Bible — v3

## Core look
A late-1990s console adventure with bright, richly saturated high-color 32-bit-era 2D raster art and a framed, controller-menu feel. Think cinematic illustrated scene, expressive portrait beside the dialogue, and a short list of choices. Strong color and value contrast belong in the artwork as well as the interface. Keep the interactive graphic-novel pacing. “32-bit” describes the period and rendering style. Deliver RGB or RGBA according to the asset's role.

The [shared concept boards](https://chatgpt.com/share/6afe27f5-593c-83ea-8b70-9f7af7741503) establish the slanted wordmark, violet frames, cyan selection bar, portrait-led dialogue and numbered actions. Apply the selected high-color treatment in [Retro Presentation and Local Narrative AI Requirements](../docs/RETRO-AI-REQUIREMENTS.md) to the artwork. Use the [shared `/play` shell](../src/Fatewake.Web/Components/SiteShell.razor) as the site layout reference. The boards' orbital locations and characters are interface mockup content. Season One opens in recognizable suburban America.

## Scene art and portraits

- Use confidently stylized, hand-illustrated forms, fine raster texture, nuanced gradients and crisp silhouettes. Build scenes from distinct foreground, subject and background color/value groups. Give each scene a clear focal point and keep fine detail away from important silhouettes.
- Shade with layered, directional light, colorful shadows and readable shadow detail. Use believable skin tones and recognizable cloth, glass and painted surfaces without photographic rendering. Aim for an illustration that reads at console-scale and still holds up at delivery size.
- Use realistic anatomy and restrained expressions. Keep approved portrait families in a consistent square crop and scale, with a legible face at small sizes. Target 128×128 or 192×192 logical pixels. Change expressions as the scene calls for them. Wardrobe or injury variants follow recorded story state.
- Generate reusable environments, characters, props and lighting layers with matching perspective, scale and light direction. Leave clean alpha edges on transparent layers and preserve negative space for other layers and the interface.

## Palette

**In-world art.** Use a vivid, varied console-adventure palette with deliberately saturated local colors and strong, readable warm/cool contrast. This is not a globally muted, gray, charcoal or desaturated treatment.

- Give vegetation, painted surfaces, skies and ordinary household objects distinct illustrated colors. Rich greens, blues, warm ambers and other scene-appropriate hues should remain legible instead of collapsing into a shared neutral wash.
- Use colored shadow planes and reflected light: blue/violet shadows against warm light are useful when the scene supports them. Preserve tonal transitions and material detail; avoid crushed blacks, clipped highlights and fluorescent overexposure.
- Neutral walls, asphalt and canonical dark clothing are valid object colors, not a rule to desaturate the whole scene. Preserve approved skin tones, clothing colors and location references. Do not recolor every object to match the interface.
- Follow recorded time, weather and light sources. Dawn can use clear cool blue against warm amber; interiors and night scenes retain appropriate darkness with readable color separation. Strong saturation does not imply midday brightness, powered lights during an outage, supernatural glow or a neon cyberpunk setting.

**Interface.** The current shell uses the Neon Frontier palette with these colors and roles. These are UI tokens, not a restricted scene-art palette. Keep their existing values and contrast/accessibility roles; vivid artwork should complement the frame without copying every accent.

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

## Prompt guidance

Start each art prompt with its subject, framing, story state and approved continuity references. Apply the shared runtime generation style and exclusions below, then the asset-role and scene-specific requirements. Naturalistic anatomy and recognizable materials do not require photographic rendering or a muted palette. Anime/manga influence is not categorically prohibited; it must still preserve the approved identity, proportions, world and shared console-adventure direction. Keep chrome, sprites and atmospheric overlays separate from the scene when they need to be reused.

The API reads the exact two sections below and this document's title version into generation requests. Keep each section a concise plain-text paragraph, with its heading unchanged. Do not place UI instructions, examples or Markdown lists inside them. Missing, empty or duplicate sections and an invalid versioned title are configuration errors, not permission to fall back to an older style.

## Runtime generation style

High-color 32-bit-era 2D console adventure illustration with late-1990s influence; vivid, richly saturated local colors, strong readable color and value contrast, scene-appropriate colorful shadows, layered directional light, nuanced gradients, fine raster detail and crisp silhouettes. Preserve approved skin tones, material and clothing colors, recognizable forms, and story-specific lighting.

## Runtime style exclusions

photorealistic photography, glossy 3D render, plastic skin, low-resolution pixel art, visible pixel grids, pixelation, heavy dithering, RGB565 quantization, muddy desaturation, washed-out color grading, crushed shadow detail, clipped highlights, neon cyberpunk scenery, fashion-editorial posing, superhero costumes, fantasy armor, zombies, excessive gore, unexplained accessories or wardrobe changes, captions, speech balloons, logos, watermarks

## Approval guidance

Before approving an asset, review it in the game frame at desktop and mobile sizes. Check for vivid illustrated color, readable warm/cool and value separation, nuanced gradients, recognizable subjects and intact shadow detail. Reject a muddy neutral wash or an abstract/unreadable subject, not a character's canonically dark clothing. Lighting/atmosphere layers must remain lighting treatments, not unrelated objects. A physical in-world display may contain legible text only when the scene explicitly calls for it.

Check that the face, clue or action reads immediately, the layers agree on direction and scale, and character and location continuity match approved references. Use this direction for new assets and explicit versioned replacements. Preserve approved artwork and journal-pinned versions. These are visual-review requirements; technical format/alpha validation alone does not establish style compliance.
