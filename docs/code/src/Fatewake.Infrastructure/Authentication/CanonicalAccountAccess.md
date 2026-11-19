# CanonicalAccountAccess

The API uses a single no-tracking PostgreSQL query to determine whether an account is active and to read its canonical role and revocation epoch. Missing or disabled accounts do not produce access state.
