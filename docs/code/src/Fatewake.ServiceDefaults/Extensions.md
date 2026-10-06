# Extensions

`Extensions.AddServiceDefaults` is the shared Aspire host configuration entry point. It adds service discovery to the service collection and configures default `HttpClient` instances to use service discovery.

Call it from each hosted application's builder setup before building the host. The extension returns the same builder to support the standard Aspire configuration chain.
