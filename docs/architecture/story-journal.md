# Story journal and graphic novel reader

The gameplay presentation is transient; the story journal is a durable, audience-specific record. `Fatewake.Story` owns shared authored scenes, narration, layer placements, and reader DTOs. The server records reached beats and accepted consequences in `story_panel`; the browser reports the approved artwork versions actually displayed. A layer's first nonzero version is pinned. Later critiques or regeneration do not rewrite recorded pages.

`GET /api/story/{survivorId}` checks survivor ownership and returns chronologically ordered panels through the survivor's current day. Reading never resumes a session, resolves a choice, queues artwork, or advances a game-event sequence. The `/novel/{survivorId}` page renders those pinned images as a responsive graphic novel. Guest survivor IDs retain the existing guest access-capability semantics.

Existing sessions recover earlier authored beats from saved progress when resumed. Those panels are marked reconstructed: exact historical image versions were not recorded before this migration. The reader discloses this limitation. Missing artwork remains a text panel; reading does not trigger paid generation.

## External influence contract

Authoritative game systems call `StoryJournalService.RecordInfluenceAsync` after applying an external player, NPC, group, or world effect, once for each affected survivor audience. The source must be a persisted game event in the same Realm. The observable-impact narration explains what changed for that audience; private cause narration identifies how or why only when allowed. A source event deduplicates within each audience's journal and becomes an `A turn of fate` cutscene.

`VisibleFromDay` gates the entire cutscene. `RevealCauseAsync` is an internal authoritative disclosure operation; there is no browser endpoint to reveal causes or create influence records. The reader DTO excludes private cause fields and source event IDs, and appends cause narration only after explicit revelation. Writers must supply audience-safe impact text and invoke revelation under their own narrative rules. Existing gameplay currently resolves personal Day One choices; no multiplayer influence producer exists yet. This contract is the integration point for those future systems, not fabricated multiplayer events.

Journal sequencing locks the target timeline and allocates an independent sequence. It does not change `NextEventSequence`. Canonical content comes from server-authored scenes and authoritative outcomes, never from browser-supplied captions. Presentation progress cannot jump ahead past unseen beats or an unresolved choice.

## Important story notifications

An applied external influence starts an unread notification revision. Explicitly revealing a previously hidden cause increments that revision and produces a clarity notification. Notification reads use the same owner/day/disclosure gates as the novel. Acknowledgement records only the displayed revision, so an older dismissal cannot swallow a newer revelation. Duplicate influence writes and reveal calls do not repeat notifications.

The gameplay and reader views poll every 15 seconds and show up to three accessible, persistent toasts with audience-safe impact text. “Read the story” acknowledges the update and links directly to its panel. Dismissal survives reloads; undisclosed and future-stage information never reaches the toast payload. No popups are invented for ordinary scene navigation.
