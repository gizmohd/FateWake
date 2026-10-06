# LocalAccountService

Hashes pending registration passwords, then requests mailbox verification before creating canonical account/credential records. Email is trimmed and invariant-case-normalized, validated, and limited to 254 characters; passwords are 12-128 characters. The database enforces unique canonical email ownership.

Login locks the credential row during verification, persists consecutive failures, locks for 15 minutes after five failures, clears failures on success or expiry, and upgrades old password hashes when needed. Unknown accounts perform password-hash work and receive the same error as wrong passwords or locked accounts. Logs omit email, passwords, hashes, and tokens.

Existing unverified credentials require email proof before login. Authenticated verified external accounts can set their first local password; account row locks serialize this operation. Existing passwords cannot be replaced through this endpoint.
