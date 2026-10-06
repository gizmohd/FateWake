# VerificationEmailSender

Sends a plain-text verification message with a configured-origin link via SMTP when enabled. Otherwise writes a unique `.txt` file to the configured emails directory, outside Web static files. Uses owner-only permissions on Unix; restrict the directory's ACL on Windows. Files contain private bearer links and must not be committed or publicly served.

SMTP errors propagate to logged HTTP 503 failures; there is no automatic fallback after SMTP failure. Durable pending challenges permit explicit resend. Multiple instances need access to their configured SMTP server or an appropriately protected shared development mail directory.
