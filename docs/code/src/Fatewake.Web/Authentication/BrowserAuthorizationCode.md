# BrowserAuthorizationCode

The Web server generates per-request state and a PKCE verifier after accepting a same-origin account form or a validated provider callback. It checks the API's exact callback and state without following the redirect, then redeems the code on the back channel. Account tokens are not passed through the browser redirect; `BrowserAccountSession` protects the access token in the HttpOnly cookie.
