# EmailDeliveryOptions

API binds the Email section. SmtpEnabled explicitly selects SMTP or private text-file delivery. Configure host, port, STARTTLS, credentials, sender, public HTTPS Web URL, and absolute fallback directory. AppHost supplies the Web endpoint and repository emails directory and forwards secret SMTP configuration. Port 465 implicit TLS is not supported by SmtpClient; use STARTTLS.
