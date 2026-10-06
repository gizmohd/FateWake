# ActionResolution

`ActionResolution` is the authoritative output from evaluating an action. It indicates acceptance, provides a stable outcome or rejection key, and returns direct state effects, persistent Wake effects, narrative facts, and the rules version.

The application layer decides how to persist accepted effects. Narrative systems may use `NarrativeFacts` to render the result, but must not alter the game outcome.
