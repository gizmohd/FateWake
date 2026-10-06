# Player Visual State

## Principle
A survivor's appearance is a projection of canonical state at a specific point in history.

**Identity → Body → Wardrobe → Equipment → Carried Items → Condition → Injuries → Story Marks → Environment**

The presentation layer must not infer ownership/equipment from artwork. Game state decides what is equipped; presentation selects compatible approved assets.

## Stable identity
Long-lived visual anchors:
- face / identity reference
- body frame
- skin tone
- baseline hair characteristics
- distinguishing persistent features

These may evolve through explicit state (hair growth, aging, scars), but do not randomly regenerate.

## Mutable visual state
Examples:
- hair/style
- facial hair
- headwear
- upper/lower clothing
- outerwear
- footwear
- gloves
- backpack/bag
- primary carried tool
- secondary carried item
- weapon/tool holster or sling
- protective gear
- dirt/wet/blood state
- fatigue
- bandages/injuries
- scars
- season/weather layers
- faction/organization identifiers where canonically worn

## Equipment slots
Slots are semantic, not rigid RPG inventory slots. Suggested initial slots:
Head, Face, TorsoBase, TorsoOuter, Legs, Feet, Hands, Back, PrimaryCarry, SecondaryCarry, Belt, Accessory.

Items may occupy multiple slots or alter compatible poses.

## Visual snapshot
Whenever a historically important panel/render is persisted, record the visual-state snapshot or its version/hash. Journal/history art must render the survivor as they appeared then, not using today's equipment.

## Compatibility
Asset metadata can declare:
- compatible body/identity
- compatible poses
- occupied slots
- exclusions
- required companion assets
- handedness/orientation
- coverage/occlusion
- story-state prerequisites

Example: a rifle sling may require a compatible torso pose and may occlude part of a backpack strap.

## Progression
Months of survival should be visible without a cosmetic level number: repaired clothing, new equipment, scars, hair growth, weather gear, faction symbols, improved packs/tools, etc. Visual progression is evidence of lived history.

## Generation
When no compatible approved asset exists, construct a generation request from:
Style Bible + survivor identity reference + current canonical visual state + pose requirement + new item/wardrobe prompt.
Persist approved output as a reusable versioned asset when appropriate.
