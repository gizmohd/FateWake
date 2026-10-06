# CandidateAction

`CandidateAction` is the normalized input to game-rule evaluation. `ActionType` selects the rule, `Arguments` carries structured values, and `RawInput` optionally retains the player's original free-form text for downstream context.

The engine evaluates the normalized fields; it should not perform transport-level parsing or call an AI provider.
