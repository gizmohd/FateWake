# LocalAccountInfo

Canonical account identity returned by the authenticated `/api/auth/me` route. Web obtains this from the API rather than trusting browser-supplied account identifiers.

HasPassword drives the backup-password prompt; EmailVerified describes proven canonical mailbox ownership.

`RequiresProfile` flags a new signup's incomplete profile row. It defaults to false for compatibility, including legacy accounts with no profile row. The `/api/auth/me` response does not return first/last name or city/state details.

The API returns the persisted Admin/User role; browser sign-in copies it into the protected cookie.
