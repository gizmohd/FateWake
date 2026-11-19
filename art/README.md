# Fatewake Art Prompt Library

Every visual placeholder in the application must have a corresponding prompt file under `art/prompts/`.

## Continuity rule
Never generate a Fatewake production image from an isolated scene prompt alone. Use:
1. `art/STYLE-BIBLE.md`
2. the relevant character sheets in `art/characters/`
3. the scene-specific prompt in `art/prompts/`
4. existing approved reference images for recurring characters/locations whenever the image generator supports references.

Scene prompts describe the delta; the Style Bible describes the universe.

The Style Bible's versioned title and concise `Runtime generation style` / `Runtime style exclusions` sections are the scene catalog's shared generation contract. Keep global rendering and palette policy there, not in independently maintained prompt sentences. The current direction uses bright, richly saturated late-1990s high-color 2D console art with strong color/value separation. Neutral clothing and story-specific darkness remain continuity constraints, not a global desaturation filter. Revised style requests may produce new versioned artwork; never overwrite approved assets or pinned history.

## Naming
Application artwork keys map directly to prompt files:
`day1-maya-street` → `art/prompts/day1-maya-street.md`

Approved generated assets should retain the same artwork key and a version suffix when needed.

## Consistency priorities
1. Character identity/silhouette
2. Fatewake visual language
3. recurring location continuity
4. lighting/color logic
5. wardrobe/injury/prop continuity
6. scene composition
7. novelty

Never sacrifice continuity merely to make a panel more dramatic.
