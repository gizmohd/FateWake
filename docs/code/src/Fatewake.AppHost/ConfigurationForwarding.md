# ConfigurationForwarding

AppHost extension `WithForwardedSection` copies every non-empty value in a configuration section (normally from AppHost user secrets) to a resource as environment variables. Keys whose final segment ends in `ApiKey`, `ClientSecret`, `Password`, `PrivateKey`, `Secret`, or `Token` are passed as shared secret Aspire parameters so the dashboard and manifests mask them. Empty or unset keys are skipped, so the service's tracked `appsettings.json` defaults apply. Used for `ArtGeneration` (Worker), `Authentication` (API and Web), and `Email` (API).
