# AccountRoleRecord

Account roles are assigned only after a successful verified sign-in. PostgreSQL serializes initial role assignment using an advisory transaction lock and enforces one Admin using a unique partial index; every later account is User. Roles persist across sign-ins and replace token role claims. Day 1 outcomes use canonical engine keys and concrete consequences.
