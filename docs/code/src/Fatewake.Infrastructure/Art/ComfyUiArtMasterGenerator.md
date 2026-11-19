# ComfyUiArtMasterGenerator

Native ComfyUI implementation of `IArtMasterGenerator`. It loads an API workflow, uploads approved durable reference images through `/upload/image`, substitutes role-based reference tokens plus the canonical prompt, negative instructions and deterministic seed, and optionally substitutes the requested model filename at `__FATEWAKE_MODEL__`. Workflows without the model token retain their own model selection. It submits `/prompt`, polls `/history/{prompt_id}`, downloads the output via `/view`, and returns it to the durable Fatewake art pipeline.

Reference tokens use `__FATEWAKE_REFERENCE_<ROLE>__`, allowing character, location, style, face/body, or future role-specific workflows without coupling Fatewake to particular ComfyUI custom nodes.

When `ApiKey` is configured, each ComfyUI request carries it in `ApiKeyHeader` (bearer scheme for `Authorization`). The Worker disables automatic redirects for this client; `/view` redirects (for example Comfy Cloud signed storage URLs) are followed once without the key so credentials never leave the ComfyUI host.

See [ComfyUI Local Image Generation for Fatewake](../../../../../COMFYUI-LOCAL-SETUP.md) for setup and operations.
