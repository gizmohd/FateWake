# OpenIddictEndpoints

The API seeds its first-party Web client and requires S256 PKCE for authorization codes. The authorization POST verifies local credentials, a single-use email challenge, or an independently validated provider identity before issuing a code for the canonical account. The token endpoint redeems codes using OpenIddict validation; ordinary registration does not issue tokens.
