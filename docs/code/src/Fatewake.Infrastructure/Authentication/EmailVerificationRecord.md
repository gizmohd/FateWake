# EmailVerificationRecord

Stores a SHA-256 hash of a 256-bit random verification token, destination, expiry, consumption timestamp, and pending registration or validated provider identity. A local pending password is salted/hashed, never plaintext. Tokens expire after 24 hours and are consumed under transactional row/advisory locks. Raw tokens appear only in delivered email.
