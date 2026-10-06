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


## Visibility pipeline
The authoritative flow is:
**Inventory → Loadout → Scene/Pose/Camera Projection → Visible Assets → Composition**

Owning an item never implies it is shown. Equipping/carrying an item still does not guarantee it is shown. Stored and concealed items are omitted by default. Scene actions can explicitly reveal, suppress, or place an item in-hand. Pose compatibility can also suppress an otherwise visible layer.

Historical visual snapshots capture the projected appearance, while canonical inventory/history separately records actual possession.


## Later Work — Player-Generated Survivor Reference Sheet

**Status: Deferred / post-MVP.**

Players should eventually be offered an optional workflow to generate and approve a canonical visual reference sheet for their own survivor. Once approved, that reference becomes the stable visual-identity input for future generated artwork depicting that survivor.

The workflow should support:
- creating a survivor visually from player-selected appearance attributes and/or other supported user-provided visual inputs;
- generating a consistent reference sheet with useful face angles, full-body proportions and baseline expressions;
- allowing the player to regenerate/edit candidates before approval;
- explicit player approval before a generated candidate becomes the survivor's canonical visual reference;
- versioning so a later approved identity update does not silently rewrite historical artwork;
- use of the approved reference as an image-reference input whenever the artwork provider supports reference images.

The reference sheet establishes **identity**, not gameplay state. It must not independently decide what the survivor owns, carries, wears, has suffered or is doing.

The rendering pipeline remains:

**Approved Survivor Identity Reference → Canonical Inventory → Loadout → Scene/Pose/Camera Projection → Visible Assets → Composition**

Therefore, a reference sheet can preserve face, body type, hair and other stable identity characteristics while authoritative state determines clothing, equipment, injuries, condition and story-specific visible details.

Historical artwork should retain the identity-reference version and visual-state/projection fingerprint used when it was created.

This feature should remain optional. Players who do not create a custom reference sheet must still receive a coherent default survivor presentation.
