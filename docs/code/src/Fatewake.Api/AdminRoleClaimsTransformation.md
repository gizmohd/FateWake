# AdminRoleClaimsTransformation
Canonical roles come from `account_role`. Incoming role claims are replaced by the persisted role for an active account with a current access epoch. Third-party OAuth clients never receive the administrator role. This transformation never bootstraps an administrator.
