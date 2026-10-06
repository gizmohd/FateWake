# ExternalAuthenticationExtensions

Registers enabled Google/Microsoft JWT validation schemes separately from the opaque local bearer scheme. Provider exchanges accept validated Authorization ID tokens only, never caller-provided subjects or email. Signature, discovery issuer, configured audience, and expiry are enforced. Google verified email can link; Microsoft first linking sends mailbox proof. Microsoft supports consumers or a specific tenant GUID, not permissive common/organizations issuer matching.
