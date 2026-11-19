# Fatewake Visual Style Bible — v7

## Core look
Fatewake is an interactive graphic novel drawn in the visual language of a high-color 32-bit console game from the 1990s. Each scene reads as a drawn panel with deliberate framing, expressive poses and a clear focal point. Use fine pixel detail, selective dark outlines, and defined shadow shapes. The neon-twilight palette uses deep indigo and violet shadows, magenta and coral glow, cyan and electric-blue highlights, and warm rim light. Deliver RGB or RGBA according to the asset's role.

The reference frame is the Fatewake game-screen mockup: a wide pixel-art scene (a character seen from behind looking over a glowing dusk landscape, large ringed or banded planet and stars, layered silhouettes with a violet-to-magenta-to-orange horizon), a square pixel portrait with a dark outline, and violet-framed panels with a cyan selection bar. Use the [shared `/play` shell](../src/Fatewake.Web/Components/SiteShell.razor) as the layout reference. The mockup's space-station setting, locations and characters are interface mockup content: Season One opens in recognizable suburban America, rendered in this pixel-art style and palette.

## Scene art and portraits

- Use fine, deliberate pixel detail. Keep faces, hands and silhouettes readable at gameplay size with selective outlines and warm/cool separation. Use restrained dithering in skies and gradients and reserve large pixel blocks for effects.
- Build scenes from distinct foreground, subject and background layers: dark foreground silhouettes, a mid-ground subject, and a glowing background. Give each scene a clear focal point, strong value contrast, and keep fine detail away from important silhouettes.
- Light with a strong dominant source and colored rim light, with violet and indigo shadows against warm coral, amber or magenta glow, plus cool cyan accents. Keep skin tones believable and readable under this lighting; clothing keeps its canonical colors, shaded toward blue and violet.
- Use realistic proportions with restrained expressions, simplified into pixel form. Keep approved portrait families in a consistent square crop and scale with a legible face at small sizes and a dark pixel frame. Target 256×256 pixels or higher per portrait. Change expressions as the scene calls for them. Wardrobe or injury variants follow recorded story state.
- Generate reusable environments, characters, props and lighting layers with matching perspective, pixel size, scale and light direction. Leave clean, hard alpha edges on transparent layers and preserve negative space for other layers and the interface.

## Stage the story beat

1. Start with the reached scene and identify what the player needs to see before reading. Record the point of view, location, time, active light sources, who is in frame, what each person is doing, and any prop or clue the beat depends on. Use approved canon and reached story facts for these details.
2. Choose the panel framing to show the important action. Establish the location when it matters. Use a medium shot for a gesture, an exchange or physical contact. Give an important clue a close-up when it would be unreadable in a wide panel. Omit people, effects and props that have no role in the beat. Keep backgrounds free of people when the composition supplies the cast as separate layers.
3. Plan all visible characters on one layout before generating their layers. Give the layout a shared horizon, camera height, ground plane, light direction and scale. Set each character's feet or support point, gaze, depth order and overlap. Cast contact shadows in the same light. Put foreground occlusion where the scene calls for it and keep faces, contact points and clues readable.
4. Stage direct interactions as one action. Draw interacting poses against the same layout or author a paired layer when the asset contract supports it. For the Day 1 threshold scene, the viewpoint is the player's doorway. The injured man's arm rests across Michelle's shoulders. She braces him at the torso and visibly takes his weight. Their bodies lean together, with matching contact points and grounded feet. Eli stands behind them. The frame reads as a request for help at the doorway.
5. Review the panel with its narration hidden. The focal subject, physical relationships and main action should be legible on their own. Check the finished composite at game size on desktop and mobile.

## Cast consistency

- Use each recurring character's approved reference sheet and continuity anchors for every pose, portrait and expression. Use approved image references when the workflow supports them. Preserve face structure, apparent age, skin tone, build, hairstyle and wardrobe colors. Change poses and expressions to fit the beat. Record story-driven changes such as injuries, clothing or equipment before depicting them.
- Review the cast side by side in the same panel. Use the same pixel scale, outline weight, facial detail, anatomy treatment and shading for all characters. Match the environment's perspective and lighting. A different outfit, role or age should read through the design and silhouette within this shared visual style.
- Check every pose for credible anatomy, weight and balance. Look for duplicated or disconnected limbs and accidental overlaps that read as extra hands or feet. At points of contact, verify both sprites together.

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

## Panel dialogue and lettering

- Use a speech bubble for a short line spoken by someone visible in the scene when it helps the reader follow the action. Point the tail toward the speaker, keep the speaker's identity clear in text, and place bubbles in natural reading order. Reserve space in the panel layout so lettering leaves faces, hands, clues and the main action clear.
- Use the dialogue panel for long conversations, offscreen speech, radio traffic, or any line that would crowd a small viewport. Keep narration in caption boxes or the narration panel. Make every line available as accessible text and keep the reading order consistent across desktop and mobile.
- Render bubbles, captions and dialogue as application overlays from the scene script. Keep generated layers free of lettering and balloons so the text can reflow, scale or be translated. A story-critical in-world display may carry its canonical label as a separate authored element.

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

Draw a high-color 1990s 32-bit console graphic-novel panel or layer with fine pixel clusters, clean silhouettes, selective dark outlines and defined shadow shapes. Depict the specified story location, time, action and subject. Give characters readable expressions, grounded poses and clear contact points for the specified interaction. Across cast and environment, match perspective, scale, pixel size, line weight and light direction. Preserve approved faces, proportions, skin tones, hair and canonical clothing. Use indigo and violet shadows with warm coral or amber light and cyan accents as the scene allows. Leave room for separately rendered dialogue and keep visual clutter away from faces, hands and clues.

## Runtime style exclusions

photograph, photorealism, live action, studio photo, photographic skin texture, smooth painterly rendering, vector art, anime cel shading, glossy 3D render, plastic skin, mixed rendering styles, mixed pixel sizes, inconsistent line weight, blurry silhouettes, oversized pixel blocks on faces, detached contact poses, floating figures, extra or duplicated limbs, duplicate characters, incidental figures, unrelated props, noise or film grain, muddy gray or brown washes, washed-out color, blown-out highlights, uncalled-for sci-fi hardware or cyberpunk scenery, fashion-editorial posing, superhero costumes, fantasy armor, zombies, excessive gore, unexplained accessories or wardrobe changes, invented captions, speech balloons, interface frames, logos, watermarks

## Approval guidance

Before approving an asset, review it in the game frame at desktop and mobile sizes. Check for fine pixel detail, the neon-twilight palette, readable warm/cool and value separation, dithered gradients, recognizable subjects and intact shadow detail. Reject smooth painterly or vector output, mixed pixel sizes, a muddy neutral wash or an abstract/unreadable subject, not a character's canonically dark clothing. Lighting/atmosphere layers must remain lighting treatments, not unrelated objects. A physical in-world display may contain legible text only when the scene explicitly calls for it.

Approve the composed panel only when it depicts the reached beat, the characters match their approved reference sheets and one another's rendering style, and the scene shares one perspective and lighting setup. Check feet, shadows, gaze, overlaps and contact poses. If there is dialogue on the panel, verify the speaker, reading order, text contrast and safe placement at narrow widths. Apply these checks to new assets and explicit versioned replacements. Keep approved artwork and journal-pinned versions available. Visual approval requires a review of the finished panel in addition to technical file and alpha validation.
