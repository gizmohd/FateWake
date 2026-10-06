# IGameEngine

`IGameEngine` is the deterministic boundary for evaluating a normalized `CandidateAction` against a `GameSnapshot`. It returns an `ActionResolution`; it does not persist state, call external services, or generate narrative prose.

Implementations should depend only on the supplied snapshot and action so outcomes remain reproducible and testable. State changes and persistent Wake consequences are returned as data for the application layer to apply and for AI presentation to interpret.
