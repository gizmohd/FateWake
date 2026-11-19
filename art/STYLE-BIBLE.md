# Fatewake Visual Style Bible — v6

## Core look
High-color 32-bit-era console illustration with fine pixel detail in a neon-twilight palette, matching the Fatewake console interface: deep indigo and violet shadows, magenta and coral sunset glow, cyan and electric-blue highlights, and warm rim light on characters. Think a cinematic console scene with a clear focal point, an expressive portrait beside the dialogue, and a short list of choices. The artwork and the interface share one retro-console identity. Keep the interactive graphic-novel pacing. Deliver RGB or RGBA according to the asset's role.

The reference frame is the Fatewake game-screen mockup: a wide pixel-art scene (a character seen from behind looking over a glowing dusk landscape, large ringed or banded planet and stars, layered silhouettes with a violet-to-magenta-to-orange horizon), a square pixel portrait with a dark outline, and violet-framed panels with a cyan selection bar. Use the [shared `/play` shell](../src/Fatewake.Web/Components/SiteShell.razor) as the layout reference. The mockup's space-station setting, locations and characters are interface mockup content: Season One opens in recognizable suburban America, rendered in this pixel-art style and palette.

## Scene art and portraits

- Use fine, deliberate pixel detail. Keep faces, hands and silhouettes readable at gameplay size with selective outlines and warm/cool separation. Use restrained dithering in skies and gradients and reserve large pixel blocks for effects.
- Build scenes from distinct foreground, subject and background layers: dark foreground silhouettes, a mid-ground subject, and a glowing background. Give each scene a clear focal point, strong value contrast, and keep fine detail away from important silhouettes.
- Light with a strong dominant source and colored rim light, with violet and indigo shadows against warm coral, amber or magenta glow, plus cool cyan accents. Keep skin tones believable and readable under this lighting; clothing keeps its canonical colors, shaded toward blue and violet.
- Use realistic proportions with restrained expressions, simplified into pixel form. Keep approved portrait families in a consistent square crop and scale with a legible face at small sizes and a dark pixel frame. Target 256×256 pixels or higher per portrait. Change expressions as the scene calls for them. Wardrobe or injury variants follow recorded story state.
- Generate reusable environments, characters, props and lighting layers with matching perspective, pixel size, scale and light direction. Leave clean, hard alpha edges on transparent layers and preserve negative space for other layers and the interface.

## Palette

**In-world art.** Use a limited, saturated neon-twilight palette: indigo, violet and deep purple for shadow and sky; magenta, rose and coral for glow; cyan, electric blue and teal for highlights and cool accents; warm amber for practical light. Each image should read as a handful of cohesive color ramps, not a photographic spectrum.

- Give skies, ground, foliage, buildings and objects distinct ramps within this palette. A suburban street at dusk or dawn uses violet houses, a magenta-to-orange horizon, cyan window and sky highlights and dark indigo shadows.
- Use colored shadow planes and reflected light; blue/violet shadows against warm light are the signature. Avoid gray, brown or beige washes, muddy mid-tones and blown-out highlights.
- Preserve canonical character identity: face structure, hair, build, skin tone and base clothing stay recognizable, shaded and highlighted within the palette. Do not recolor characters wholesale to match the interface.
- Follow recorded time, weather and light sources. Night, dawn, dusk and indoor scenes use the same ramps at different values, with readable separation between layers. Neon styling is a lighting and palette treatment, not a setting: no cyberpunk city, no sci-fi hardware, no supernatural glow unless the story state calls for it.

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

Use 1280×720 masters for widescreen scene presentations. Review existing portrait masters with their own safe crop. Keep important clues, faces and contact poses within the central safe area, then inspect the composite on a phone and at desktop size. Trim empty margins from character sprites and scale them with normal image interpolation to retain facial detail.

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

Start each provider prompt with the shared illustrated style, layer role and framing. Follow with the subject, story state and approved continuity references. Natural proportions and recognizable materials are expressed through pixel art, never photographic rendering or a muted palette. Anime/manga influence is not categorically prohibited; it must still preserve the approved identity, proportions, world and the shared pixel-art direction. Keep chrome, sprites and atmospheric overlays separate from the scene when they need to be reused.

The API reads the exact two sections below and this document's title version into generation requests. Keep each section a concise plain-text paragraph, with its heading unchanged. Do not place UI instructions, examples or Markdown lists inside them. Missing, empty or duplicate sections and an invalid versioned title are configuration errors, not permission to fall back to an older style.

## Runtime generation style

Hand-pixelled high-color 32-bit-era console game illustration, clearly drawn fine pixel clusters, clean silhouettes and selective dark outlines. Keep facial features, hands and contact poses legible at gameplay size. Use deep indigo and violet shadows, magenta and coral sunset glow, cyan highlights and warm rim light. Reserve selective dithering for skies and smooth palette ramps. Keep background and foreground detail away from the main figures. Preserve character identity, skin tones, canonical clothing and story-specific lighting.

## Runtime style exclusions

photograph, photorealism, live action, camera portrait, photographic skin texture, studio photo, smooth digital painting, vector art, anime cel shading, glossy 3D render, plastic skin, oversized pixel blocks on characters, blurry silhouettes, mixed pixel sizes, noise or film grain, muddy gray or brown washes, washed-out color, blown-out highlights, cyberpunk megacity, sci-fi hardware, fashion-editorial posing, superhero costumes, fantasy armor, zombies, excessive gore, unexplained accessories or wardrobe changes, captions, speech balloons, user-interface frames, logos, watermarks

## Approval guidance

Before approving an asset, review it in the game frame at desktop and mobile sizes. Check for fine pixel detail, the neon-twilight palette, readable warm/cool and value separation, dithered gradients, recognizable subjects and intact shadow detail. Reject smooth painterly or vector output, mixed pixel sizes, a muddy neutral wash or an abstract/unreadable subject, not a character's canonically dark clothing. Lighting/atmosphere layers must remain lighting treatments, not unrelated objects. A physical in-world display may contain legible text only when the scene explicitly calls for it.

Check that the face, clue or action reads immediately, the layers agree on direction and scale, and character and location continuity match approved references. Use this direction for new assets and explicit versioned replacements. Preserve approved artwork and journal-pinned versions. These are visual-review requirements; technical format/alpha validation alone does not establish style compliance.
