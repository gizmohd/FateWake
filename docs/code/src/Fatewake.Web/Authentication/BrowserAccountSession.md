# BrowserAccountSession

Canonical cookie issuance is measured at web.issue_session, with no cookie/token payload collection.

Shared sign-in for password, mailbox proof, and OIDC exchange. Fetches canonical identity from API, protects the opaque API token in the HttpOnly cookie, and bounds browser expiry to the start of the API login request. New provider signups with an unfinished profile first redirect to `/complete-profile`, then backup-password setup. Accounts with complete profiles but no password redirect directly to password setup. Legacy accounts are unaffected. Private names and location are not copied into cookie or token claims.
