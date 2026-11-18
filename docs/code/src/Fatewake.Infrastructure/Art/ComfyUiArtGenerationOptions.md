# ComfyUiArtGenerationOptions

Configuration for Fatewake's native ComfyUI provider: endpoint, optional API key and header, API workflow path, substitution tokens, polling interval, and generation timeout.

`ApiKey` is optional: null, empty, or whitespace means not configured (normalized to null) and no authentication header is sent, which is correct for a private local ComfyUI. Set it (as a user secret) for Comfy Cloud (`BaseUrl` `https://cloud.comfy.org/api/`, default `X-API-Key` header) or an authenticating reverse proxy; `ApiKeyHeader` `Authorization` sends a bearer token. The key is sent only to ComfyUI requests, never to redirect targets.

See [ComfyUI Local Image Generation for Fatewake](../../../../../COMFYUI-LOCAL-SETUP.md) for setup and operations.
