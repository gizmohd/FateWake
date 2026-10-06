# LocalAuthenticationEndpoints

Browser credential/action forms emit payload-free operation spans and latency; caught transport errors/timeouts mark failure/cancellation. Raw form values and bearer tokens are never recorded.

Processes same-origin login, registration, and logout forms with antiforgery validation. Credential submission is rate-limited. On successful API authentication, Web reads canonical identity and issues an eight-hour, non-sliding HttpOnly cookie carrying the opaque API token. Production cookies require HTTPS. Redirects use fixed local paths.

Invalid credentials, duplicate registration, throttling, password confirmation mismatch, and API transport errors produce visible messages. Logout deletes the browser cookie; previously copied API bearer tokens expire independently.

Registration redirects to check-email. Verification POSTs contain mailbox token and, for pending local registration, password confirmation. Single-use proof signs in through BrowserAccountSession, which routes provider accounts without a password to setup. Resend and first-password setup forms are antiforgery protected; password setup additionally requires a browser session. Enabled provider challenge forms use POST and antiforgery.
