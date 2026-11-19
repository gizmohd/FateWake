# SceneSpecValidator

Validates untrusted narrator output: size limits, control characters, injection markers, beat kinds, unique snake_case action types, and the pacing rule via `SceneDecisionPolicy` (at most four consecutive decision-free scenes). Returns the new streak.
