# Fatewake Layered Art Composition System

## Goal
Build graphic-novel panels from reusable approved assets instead of generating a unique flattened image for every beat.

## Composition stack
Recommended back-to-front order:
1. Background / environment
2. Distant environment
3. Character bodies / poses
4. Character expression/head overlays when applicable
5. Props
6. Foreground framing
7. Atmosphere / weather
8. Lighting / color treatment
9. Story-specific visual effects
10. Application-rendered dialogue, captions and interaction UI

UI text is not baked into artwork.

## Asset types
- Background
- CharacterPose
- CharacterExpression
- Prop
- Foreground
- Atmosphere
- Lighting
- Effect
- HeroIllustration

Assets have stable keys and versions. A panel references keys rather than file paths.

## Coordinate system
Compositions use normalized coordinates so the same definition can scale across devices:
- X/Y: 0.0–1.0 relative to composition canvas
- width/height: normalized
- anchor: center, top-left, bottom-center, etc.
- z-index controls stacking
- opacity: 0–1
- scale, rotation and optional crop/focal point

Master art targets 9:16 portrait. Responsive layouts may crop, but each asset declares focal/safe regions.

## Character reuse
Prefer separating recurring characters into:
- base pose/body
- optional expression/head variant
- optional story-state overlay (dirt, bandage, wet clothing, etc.)

Do not force modular separation when it creates visibly artificial results. A complete CharacterPose may include the face when that is the more coherent asset; expressions can then be separate pose variants.

## Continuity
Composition definitions are versioned content. Approved assets are immutable; changes create a new version. A historical scene should remain reproducible from the asset/composition versions recorded when it was rendered.

## Rendering
MVP: CSS-positioned layered WEBP/PNG assets in Blazor.
Later: canvas/WebGL only if animation/performance requires it.

Prefer WEBP/AVIF for opaque backgrounds and WEBP/PNG with alpha for overlays depending on browser/tooling quality. Keep source masters separately from delivery derivatives.

## Hero art
Major reveals may use a purpose-generated flattened HeroIllustration. Hero images still receive stable keys, provenance and prompt files and can coexist with reusable layered panels.


## Generated Asset Persistence and Reuse

AI image generation or alteration is an **asset-creation operation**, never a transient render operation.

Whenever Fatewake pays to generate, edit, extend, restyle, composite, or otherwise alter an in-game image with AI, the resulting image and any independently reusable generated layers/assets must be persisted and registered before they are used in production gameplay.

### Reuse-first rule

Before authorizing generation, the visual pipeline must attempt, in order:

1. exact approved asset/render lookup;
2. exact composition/render lookup by canonical visual-state fingerprint;
3. reuse/recomposition of existing approved backgrounds, character poses, expressions, props, equipment, foregrounds, lighting and effects;
4. compatible existing-asset discovery for editorial review or deterministic composition;
5. new AI generation only when no approved reusable result satisfies the requested canonical state.

Semantic similarity may help discover candidates, but must never silently substitute an asset whose canonical state is incompatible.

### Persisted generation record

Every generated result should retain enough provenance to reproduce, audit, reuse and cost-account for it, including:

- stable asset/render ID and version;
- content hash;
- asset type and artwork/composition key;
- survivor/character identity-reference version(s);
- canonical visual-state and visible-projection fingerprint;
- scene, pose and camera context when applicable;
- source/reference asset IDs and versions;
- prompt/template version and Style Bible version;
- provider/model and generation/edit operation;
- generation parameters supported by the provider;
- creation timestamp;
- approval/status lifecycle;
- storage URI/object key;
- dimensions/format;
- generation cost/usage metadata where available;
- parent asset/render ID for edits and derivatives;
- continuity/editorial notes.

### Derivative assets

An AI alteration must not overwrite its source. It creates a new immutable version/derivative linked to its parent. If a useful component is produced independently (for example a Michelle pose, backpack overlay, damaged jacket, radio prop or lighting effect), it should be registered as a reusable asset rather than existing only inside a flattened scene.

Flattened final scene renders may also be cached and reused when the complete render fingerprint matches.

### Runtime requirement

Normal gameplay should resolve a persisted asset or composition reference. It should not invoke image generation simply because a player revisits a scene, reloads a page, opens historical journal artwork, or another compatible scene needs the same visual asset.

Generation is authorized only for a genuinely missing canonical visual requirement or an explicitly requested new version.

This applies equally to authored NPC artwork and future player-generated survivor artwork.


## Image Storage and Web Delivery Formats

All AI-generated or AI-altered production artwork must preserve a **full-resolution PNG archival master**.

For web/game delivery, the asset pipeline derives and stores:

1. **Master PNG** — full generated resolution, archival/source-of-truth image used for future edits, derivatives, recomposition and regeneration references. Do not destructively optimize or resize this master.
2. **WebP delivery asset** — optimized derivative used by default by supported browsers.
3. **Optimized PNG fallback** — web-optimized PNG derivative for clients that cannot render the WebP version.

The master PNG, WebP derivative and optimized PNG fallback belong to the same logical asset/version and must be linked in asset metadata rather than treated as separate canonical artwork.

Runtime presentation should prefer WebP and fall back to the optimized PNG, for example through HTML `<picture>`/source fallback or an equivalent framework abstraction. Runtime code should not normally serve the archival master.

Transparent reusable layers must retain alpha in the archival PNG and in delivery formats where required.

Derivative generation must preserve the original pixel dimensions unless a specifically registered delivery-size variant is being created. Additional responsive sizes may be generated later, but they must remain derivatives of the same immutable master asset.

The asset record should capture at minimum:
- master PNG storage location, dimensions, byte size and content hash;
- WebP storage location, dimensions, byte size, encoder/settings/version where useful, and content hash;
- optimized PNG fallback location, dimensions, byte size and content hash;
- alpha/transparency requirements;
- derivative relationship and generation timestamp.

If a delivery derivative is missing or its encoding policy changes, it should be recreated from the archival master without invoking the AI image provider again.
