# LocalCredentialRecord

Local credentials are keyed by canonical account ID. The unique normalized email is used only for local login; matching external-provider email never links accounts. PasswordHash is the salted, versioned ASP.NET Identity hash. FailedAttempts and LockoutEnd persist throttling across restarts.
