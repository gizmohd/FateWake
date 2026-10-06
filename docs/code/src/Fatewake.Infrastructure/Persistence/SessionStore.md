# SessionStore

Session creation/resumption and progress persistence emit fixed-name operation spans/latency, with optional typed ILogger supplied by host DI. Identifiers and presentation state are not metric labels.

Guest sessions use a browser-stored survivor ID. Authenticated sessions resolve the earliest active survivor owned by the canonical account, or create an account-owned survivor. Supplied IDs are filtered by account ownership; API routes check access before calling persistence and validate event/survivor/timeline relationships.

Signing in never attaches an arbitrary guest ID to an account. Guest progress and account progress remain separate.

Initial account-owned session creation locks the account row in a transaction, preventing concurrent first visits from creating duplicate account survivors.
