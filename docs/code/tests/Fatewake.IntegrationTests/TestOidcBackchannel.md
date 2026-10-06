# TestOidcBackchannel

Emulates an OIDC token endpoint without contacting real providers. Enforces code and PKCE verifier matching and signs ID tokens containing nonce and issued-at claims. Used with static trusted test discovery metadata to exercise real Web OIDC middleware and independent API validation.
