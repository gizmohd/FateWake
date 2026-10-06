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
