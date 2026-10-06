# CharacterAppearanceFingerprint

## Purpose
Deterministically hashes the base identity key/version plus every material normalized appearance trait. Exact equality is the authorization boundary for zero-cost asset reuse; semantic similarity is not sufficient.

## Usage
Use this type before requesting character-art generation. Normalize the desired appearance, calculate its fingerprint, and query approved persisted assets first. Only a cache miss may proceed to background generation.

## Reuse behavior
Changing appearance creates or selects another immutable combination. It does not delete earlier combinations. Returning to a prior combination resolves its approved asset immediately and does not invoke an AI provider.

## Composition
Appearance is below equipment/loadout and scene projection in the visual-state hierarchy. Consequently the same approved appearance may be reused across clothing, equipment, injuries, poses, cameras, and environments without changing identity.
