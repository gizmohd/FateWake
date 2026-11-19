# VerifiedAccountService

Local registration receives validated first/last name and city/state details. Development saves them with the canonical account immediately; other environments retain them on the pending email challenge, preserve them across resends, and transfer them only after mailbox/password proof succeeds. Consumed challenges have profile fields cleared, including sibling challenges invalidated during verification. An external mailbox owner never inherits an attacker's unproven pending profile.

Every newly created canonical account gets a profile row. New external-provider accounts begin with an empty row and complete their own details after authentication. Existing accounts without a row stay valid and are not backfilled with guessed personal data.

Local registration automatically creates a verified account only when the server hosting environment is `Development`. It sends no email or challenge in that environment. The registration endpoint signs the account in through the normal role-assignment flow. Production, Staging, and a missing environment retain mailbox verification. External provider linking continues to require validated ownership claims or mailbox proof.

Pending registration, external resolution, resend and verification each emit a payload-free fixed-name operation span/duration. ILogger remains typed; hashes, email addresses and proof tokens never enter telemetry dimensions.

Coordinates pending registration, mailbox verification, resend, and provider account linking. Only validated provider claims may enter ExternalAsync. Google email_verified proves ownership; Microsoft email claims always require mailbox proof for first linking. Known linked subjects retain their canonical account even if provider email claims change.

Email/subject PostgreSQL advisory locks serialize registration and linking across replicas. Canonical normalized email is unique. Matching proven addresses link to the same account and retain its verified local password. Unverified legacy passwords are removed before an external email owner claims the account; unsolicited pending registrations cannot overwrite a subsequently created account. Confirming pending local registration requires its chosen password as well as the email token, preventing pre-registration password activation by an unsuspecting recipient.

Challenges use 256-bit randomness and store only hashes. Confirmation locks and consumes the token and invalidates other pending proofs for that email. Expiry is 24 hours. Resend avoids account-existence disclosure, has a one-minute cooldown, and persists before delivery so transport failures can be retried. Verification never trusts Host headers for link origin and never attaches guest survivors.
