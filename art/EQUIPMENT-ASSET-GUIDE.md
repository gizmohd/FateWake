# Equipment & Wardrobe Asset Generation Guide

## Purpose
Equipment and clothing should be reusable visual layers that reflect canonical survivor state.

## Prompt recipe
Every generated wearable/carried asset prompt must include:
1. Fatewake Style Bible version.
2. survivor identity/body reference when fit/contact matters.
3. target pose key.
4. semantic visual slot.
5. exact canonical item description.
6. handedness/contact points.
7. assets it must coexist with.
8. assets/slots it occludes.
9. scene lighting reference.
10. transparent/isolatable output requirement where appropriate.

## Asset keys
Examples:
- wear-jacket-canvas-brown-01
- wear-backpack-daypack-gray-01
- tool-hatchet-basic-01
- tool-crowbar-red-worn-01
- injury-bandage-left-forearm-01
- condition-rain-wet-01

The key identifies the visual design, not ownership. A SignificantItem instance may point to a reusable visual key.

## Variants
Do not generate arbitrary variants. Add one when needed by:
- pose/contact incompatibility
- front/back/side view
- meaningful condition change
- story-established modification
- materially different lighting that cannot be handled by composition

## Weapons/tools
Render only equipment actually present in canonical state. Do not embellish with extra ammunition, holsters, blades, firearms or tactical equipment. Fatewake artwork must not turn ordinary survivors into action heroes merely because a tool is equipped.

## Historical continuity
When an item becomes visually distinctive through wear, repair or story history, create/version that visual state and associate it with the SignificantItem's history. Later replacement equipment should look different; old journal panels retain the old version.
