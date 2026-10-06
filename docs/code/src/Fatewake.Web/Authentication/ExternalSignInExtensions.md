# ExternalSignInExtensions

Registers enabled Google/Microsoft OIDC handlers. Requires client ID/secret; fixed provider callback paths, authorization-code/PKCE, nonce and correlation/state validation, and strict issuer validation are enabled. Validated ID tokens are exchanged server-side with API for canonical sessions. Provider principals/tokens never become the browser's application cookie identity directly.

Mailbox proof redirects to check-email, new accounts to password setup, and logged transport/provider failures to visible login errors. Microsoft configuration selects personal consumers or one tenant GUID. AppHost forwards provider settings to both API and Web; deployed services require consistent audiences and issuer configuration.
