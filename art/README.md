# Fatewake Art Prompt Library

Every visual placeholder in the application must have a corresponding prompt file under `art/prompts/`.

## Continuity rule
Never generate a Fatewake production image from an isolated scene prompt alone. Use:
1. `art/STYLE-BIBLE.md`
2. the relevant character sheets in `art/characters/`
3. the scene-specific prompt in `art/prompts/`
4. existing approved reference images for recurring characters/locations whenever the image generator supports references.

Scene prompts describe the delta; the Style Bible describes the universe.

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
