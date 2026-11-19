# SessionStore

Session creation/resumption and progress persistence emit fixed-name operation spans/latency, with optional typed ILogger supplied by host DI. Identifiers and presentation state are not metric labels.

Guest sessions use a browser-stored survivor ID. Authenticated sessions resolve the existing account-owned Pathweaver and their explicitly selected Thread, or create an account-owned identity. Supplied IDs are filtered by account ownership; API routes check access before calling persistence and validate event/survivor/timeline relationships.

Signing in never attaches an arbitrary guest ID to an account. Guest progress and account progress remain separate.

Initial account-owned session creation locks the account row in a transaction, preventing concurrent first visits from creating duplicate account survivors.

Progress, reached opening/pre-decision checkpoints and seen journal records share one transaction and survivor/timeline locking protocol. Only an event on the selected Thread can advance; an older tab cannot write through a branch change.

Development-only `ResetChapterAsync` rewinds the active chapter only when no preserved descendants depend on it. In one transaction it discards unreferenced chapter checkpoints, attempts/resolutions, correlated events/Wakes, then clears outcome/facts and records a new opening. Stable identity, account ownership and base artwork remain. Sequence numbers are not reused. Production players use non-destructive Threads instead.
