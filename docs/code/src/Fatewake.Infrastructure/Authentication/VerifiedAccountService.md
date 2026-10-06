# VerifiedAccountService

Coordinates pending registration, mailbox verification, resend, and provider account linking. Only validated provider claims may enter ExternalAsync. Google email_verified proves ownership; Microsoft email claims always require mailbox proof for first linking. Known linked subjects retain their canonical account even if provider email claims change.

Email/subject PostgreSQL advisory locks serialize registration and linking across replicas. Canonical normalized email is unique. Matching proven addresses link to the same account and retain its verified local password. Unverified legacy passwords are removed before an external email owner claims the account; unsolicited pending registrations cannot overwrite a subsequently created account. Confirming pending local registration requires its chosen password as well as the email token, preventing pre-registration password activation by an unsuspecting recipient.

Challenges use 256-bit randomness and store only hashes. Confirmation locks and consumes the token and invalidates other pending proofs for that email. Expiry is 24 hours. Resend avoids account-existence disclosure, has a one-minute cooldown, and persists before delivery so transport failures can be retried. Verification never trusts Host headers for link origin and never attaches guest survivors.
