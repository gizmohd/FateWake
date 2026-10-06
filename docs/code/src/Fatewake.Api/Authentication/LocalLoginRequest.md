# LocalLoginRequest

JSON input for local registration and password login. Email and password are validated by LocalAccountService. Both routes are rate-limited. Registration returns a pending-verification response; verified password login issues an opaque ASP.NET bearer session.
