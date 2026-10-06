# EmailDeliveryTests

Exercises real loopback SMTP protocol delivery and connection failure, asserting enabled SMTP never writes file fallback. Disabled SMTP must write a text email with the configured verification link and owner-only Unix permissions. Tests own and clean up their mailbox directories/listeners.
