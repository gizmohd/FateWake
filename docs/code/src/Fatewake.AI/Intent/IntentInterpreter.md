# IIntentInterpreter

`IIntentInterpreter` converts player-provided text into a normalized `CandidateAction`. Implementations may use deterministic mapping or an AI provider, but the returned action is only an intent proposal; `Fatewake.GameEngine` remains authoritative for acceptance and consequences.
