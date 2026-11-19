# EmailVerificationRecord

Pending local registration also stores nullable first/last name, city, and state/region, each limited to 100 characters. Resends retain the same pending profile. Successful proof transfers validated details to the canonical account profile and clears these fields from consumed/invalidated challenges. Provider display-name claims are not treated as a completed self-reported profile.

Stores a SHA-256 hash of a 256-bit random verification token, destination, expiry, consumption timestamp, and pending registration or validated provider identity. A local pending password is salted/hashed, never plaintext. Tokens expire after 24 hours and are consumed under transactional row/advisory locks. Raw tokens appear only in delivered email.
