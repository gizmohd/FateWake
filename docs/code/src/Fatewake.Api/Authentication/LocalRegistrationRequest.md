# LocalRegistrationRequest

Registration-only JSON contract for email/password plus required first name, last name, city, and free-text state/region. Login continues to use `LocalLoginRequest` without these fields. Infrastructure validates and normalizes profile input before hashing or persisting credentials. Missing/invalid details return HTTP 400 without creating a pending registration or sending mail.
