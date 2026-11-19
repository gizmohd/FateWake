# AuthenticatedApiClient

Creates a named service-discovery client for each call and attaches the current circuit's bearer token. `CreateForUser` provides the same policy for HTTP endpoints (account forms and the artwork proxy). Authorization headers are never put on a shared client or a pooled handler. Anonymous users retain guest play; an authenticated user without an access token is explicitly rejected as 401 before any API request is sent.

`AccessTokenClaim` names `fatewake.api_token`, a private server-side claim populated from the API token endpoint's **access_token**, not the OIDC ID token or an upstream provider token. The name is retained for existing protected sessions. OAuth authorization-code redemption and upstream identity proof retain their protocol-specific credentials.
