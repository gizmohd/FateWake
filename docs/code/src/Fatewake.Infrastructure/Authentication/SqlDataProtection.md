# SqlDataProtection

Registers ASP.NET Core Data Protection's EF Core XML repository against `FatewakeDbContext`. The `data_protection_keys` table stores a durable shared key ring. Web replicas use the stable `Fatewake.Web` discriminator; API replicas use `Fatewake.Api`, preventing cross-application payload unprotection even though the ring is shared.

Production requires `DataProtection:CertificateThumbprint` and the same wrapping certificate, including its private key, installed in each consuming host's certificate store. Private wrapping keys are not stored in PostgreSQL. Retain old certificates after rotation so existing key-ring entries remain readable. Development permits an unencrypted ring and the framework reports its standard warning. Framework key management handles automatic rotation and records key lifecycle/storage failures through the shared Microsoft logging pipeline. Do not log or export key XML.

This ring protects cookies and antiforgery state. It is not OpenIddict's token signing/encryption credential store.
