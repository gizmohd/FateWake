# AdminRoleClaimsTransformation
Canonical roles come from account_role. Incoming role claims are replaced by the persisted Admin/User role. Accounts without a completed sign-in role assignment are User. This request transformation never bootstraps an administrator.
