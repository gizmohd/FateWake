# ComfyUiArtMasterGenerator

Native ComfyUI implementation of IArtMasterGenerator. It loads an API workflow, substitutes the canonical prompt/negative instructions/deterministic seed, submits `/prompt`, polls `/history/{prompt_id}`, downloads the output via `/view`, and returns it to the durable Fatewake art pipeline.

See [ComfyUI Local Image Generation for Fatewake](../../../../../COMFYUI-LOCAL-SETUP.md) for setup and operations.
