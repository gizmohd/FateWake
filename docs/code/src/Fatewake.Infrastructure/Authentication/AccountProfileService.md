# AccountProfileService

Validates and saves the authenticated canonical account's first name, last name, city, and state/region. The account identifier comes from validated authentication, never the request body. PostgreSQL account-row locking serializes creation and updates, including concurrent deactivation. Inactive/missing accounts and invalid fields produce safe errors; infrastructure failures propagate.

`auth.complete_profile` records bounded operation timing and error type. Structured lifecycle logs contain only account ID, never submitted names or location. Profile details do not become token claims, public survivor names, or game-world location.
