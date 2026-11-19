# SqlCertificateXmlDecryptor

Recovers a Data Protection key envelope using the exact historical wrapping certificate stored in PostgreSQL. The certificate is loaded ephemerally with the configured shared password and disposed after authenticated decryption. Missing certificate identifiers, unavailable material, incorrect passwords, or modified ciphertext fail explicitly.

The public constructor must accept `IServiceProvider`. Data Protection reconstructs the decryptor from the type name persisted in key XML using its own simplified activator, which recognizes only an `IServiceProvider` constructor or a parameterless constructor. It does not resolve arbitrary constructor dependencies. Resolve `SqlKeyRingCertificateStore` from the supplied provider rather than taking it as a constructor argument.

Keeping the same decryptor type and envelope format preserves existing encrypted SQL keys; the activation fix requires no key deletion, certificate regeneration, or data migration. Operational errors are reported by the framework's key-management boundary without adding payload-bearing logs to this helper. `SqlDataProtectionTests` exercises recovery through independent read-only providers for both application discriminators.
