# BrowserAccountSession

Shared sign-in for password, mailbox proof, and OIDC exchange. Fetches canonical identity from API, protects only the opaque API token in the HttpOnly cookie, and bounds browser expiry to the start of the API login request. Accounts without a password redirect to setup rather than quietly omitting backup login.
