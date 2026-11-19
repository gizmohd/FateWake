# ComfyUiGeneratorRegistration

Selects the primary ComfyUI generator or explicitly configured OpenAI fallback. The interface registration must resolve the typed ComfyUI client, rather than constructing the generator with the unconfigured default `HttpClient`. That invariant preserves the provider base URL and redirect policy.
