# ComfyUI workflows

The default `fatewake-ssd1b-api.json` renders opaque 768x1344 backgrounds with Segmind SSD-1B, using 18 DPM++ 2M/Karras sampling steps and CFG 9. `fatewake-ssd1b-overlay-api.json` adds native BiRefNet background removal and an alpha join for reusable overlays. This smaller SDXL derivative runs on the tested Intel Core Ultra 7 155H / Intel Arc machine with 32 GB RAM. ComfyUI's Intel XPU installation selects FP16 and manages model offloading.

The original `fatewake-api.json` and `fatewake-overlay-api.json` remain available as 512x768 Stable Diffusion 1.5 fallback workflows. Select either pair through `ArtGeneration:ComfyUI:WorkflowPath` and `OverlayWorkflowPath` in `src/Fatewake.AppHost/appsettings.json`, and update the `Model` provenance label to match.

Install `SSD-1B-A1111.safetensors` in ComfyUI's `models/checkpoints` directory and `birefnet.safetensors` in `models/background_removal`. The fallback requires `v1-5-pruned-emaonly.safetensors`. Model weights stay outside the repository. Review generated anatomy, composition, edges, and continuity before relying on the artwork.

Model sources: [Segmind SSD-1B](https://huggingface.co/segmind/SSD-1B), [Stable Diffusion 1.5](https://huggingface.co/stable-diffusion-v1-5/stable-diffusion-v1-5/blob/main/v1-5-pruned-emaonly.safetensors) and [Comfy-Org BiRefNet](https://huggingface.co/Comfy-Org/BiRefNet/blob/main/background_removal/birefnet.safetensors). Comfy Desktop can use its shared `ComfyUI-Shared/models` directory for all three.

Run the opt-in GPU smoke tests with `FATEWAKE_COMFY_SMOKE_ROOT` set to the repository's absolute path, then `dotnet test tests/Fatewake.IntegrationTests/Fatewake.IntegrationTests.csproj --filter FullyQualifiedName~ComfyUiGenerationTests`. SSD-1B is the default profile; set `FATEWAKE_COMFY_SMOKE_PROFILE=sd15` to test the fallback. Previews and measured generation times are written to ignored `data/art/smoke`. The tests check portrait dimensions and both transparent and visible pixels in the overlay.

Use `__FATEWAKE_PROMPT__`, `__FATEWAKE_NEGATIVE__`, and `__FATEWAKE_SEED__` in the prompt, negative prompt, and seed inputs. The generation adapter substitutes these before submitting the graph. Include a Save Image output node. Reusable characters, props, foreground, lighting, and effects need a workflow that can produce transparent overlays; the prompt alone cannot guarantee an alpha channel.

The seed token intentionally appears unquoted: these files are templates, and Fatewake replaces it with a JSON integer before parsing/submitting. See [the setup and reference-image guide](../docs/COMFYUI-LOCAL-SETUP.md).

Cloud default: FLUX.2 Klein 4B Distilled (`fatewake-klein4b-api.json` / `fatewake-klein4b-overlay-api.json`), 4 steps, CFG 1, Euler, 768x1344, no upscaling. Uses https://cloud.comfy.org/api/ and HistoryPath=history_v2. Configure ArtGeneration:ComfyUI:ApiKey in AppHost user secrets or environment variables. Overlay workflow requires BiRefNet background-removal nodes on the target server. SSD-1B workflow files remain available for localhost:8188 with HistoryPath=history.
