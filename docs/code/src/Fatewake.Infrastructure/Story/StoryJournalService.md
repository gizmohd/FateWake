# StoryJournalService

Persists reached panels and audience-safe consequences without executing choices or generating artwork. Reads and notices use only the selected Thread. Immutable checkpoint copies preserve the child's disclosure, notification and artwork-pin state even when its parent changes later.

Writers participate in a caller's transaction or own one explicitly, locking the exact survivor before the timeline. They do not infer a survivor from timeline membership: shared legacy timelines may have multiple players, and inactive personal Threads retain their historical owner. Sequence allocation is timeline-scoped; entry uniqueness is survivor/Thread/key-scoped. Stale artwork, notice and reveal mutations are rejected through the same active-Thread validation.

External impacts must already be audience-safe, remain separate from hidden causes, and make the affected timeline ineligible for personal branching. Reveal/acknowledgement state is versioned independently. Exact approved artwork versions are pinned once so later regeneration cannot replace seen history.

`story.read` spans record fixed operation names, latency and error/cancellation type only. Prompts, narration, user input and private state are not telemetry labels.
