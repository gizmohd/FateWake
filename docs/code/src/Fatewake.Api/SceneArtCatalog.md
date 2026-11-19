# SceneArtCatalog

Builds canonical image-generation requests from bundled authored prompts, character/location continuity, and the active visual style. The catalog is an allowlist: client requests cannot supply prompts or keys outside the shipped set.

`SceneArt/STYLE-BIBLE.md` is copied from `art/STYLE-BIBLE.md` during build/publish. Its `Runtime generation style` and `Runtime style exclusions` sections supply shared positive and negative instructions for every authored asset role. The versioned title supplies `StyleBibleVersion`; there is no separate hard-coded palette or style-version fallback. Required sections must be unique and nonempty. Invalid/missing style content fails explicitly through existing request error boundaries without logging prompt bodies.

The subject remains first, followed by shared style, role framing and applicable character/location continuity. Role-specific exclusions still prevent characters in environment layers and scenery in isolated props. Template version 4 fingerprints include the declared style revision, resolved prompt and negative instructions. Style or revision changes therefore create a new request identity, while identical inputs remain deterministic.

This affects future normal resolution requests; the catalog does not enqueue bulk regeneration, overwrite approved assets, mutate pinned history or alter provider workflows. Technical finalization still does not perform semantic visual-style review.
