# AccountProfileRecord

Maps `account_profile`, owned one-to-one by the canonical account. Fields are nullable only to represent a newly created external-provider account awaiting profile completion. Completed profiles contain all four validated fields and `CompletedAt`. Profile details are private and separate from account display names and gameplay identity.

Existing accounts have no row and are not forced through new registration. A blank row created by the verified-account creation workflow marks a new signup requiring completion. Profile saves lock the owning account to serialize inserts/updates. Soft account deactivation preserves this data; physical account deletion cascades to it.
