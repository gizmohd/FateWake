# AdminApplicationEndpoints

Identity administrators list, inspect, register, update, rotate secrets for, and remove OpenIddict clients. On the seeded Web application, only callback URL lists can change, and they must retain the callbacks required by the running services. Successful changes are audited; stored secrets are not projected into API reads. Removal revokes existing grants and credentials in the same transaction.
