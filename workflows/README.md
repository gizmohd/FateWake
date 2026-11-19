# ComfyUI workflows

## Z-Image Turbo with shared model storage

`fatewake-z-image-turbo-api.json` and `fatewake-z-image-turbo-overlay-api.json` load the diffusion model, Qwen3 text encoder, and VAE separately. They use 8 sampling steps, CFG 1, res-multistep/simple sampling, and AuraFlow shift 3, following the [official Z-Image Turbo template](https://docs.comfy.org/tutorials/image/z-image/z-image-turbo). Output masters are 1280x720, matching the 640x360 logical scene aspect ratio without pixel-grid quantization. The overlay adds the same installed BiRefNet removal/alpha join used by the SSD-1B workflow. The distilled model uses zeroed negative conditioning; `__FATEWAKE_NEGATIVE__` is intentionally omitted.

Keep downloaded weights in the ignored repository-root `models` directory:

```text
models\diffusion_models\z_image_turbo_bf16.safetensors
models\text_encoders\qwen_3_4b.safetensors
models\vae\ae.safetensors
```

All three components are available from [Comfy-Org Z-Image Turbo](https://huggingface.co/Comfy-Org/z_image_turbo/tree/main/split_files). Verify downloads against their upstream LFS SHA-256 values. The overlay also needs `birefnet.safetensors` in ComfyUI's existing `background_removal` model directory.

Create `extra_model_paths.yaml` beside ComfyUI's `main.py`, replacing the base directory with your absolute repository model path:

```yaml
fatewake:
  base_path: C:\Code\FateWake\models
  diffusion_models: diffusion_models
  text_encoders: text_encoders
  vae: vae
```

ComfyUI loads this file at startup; restart the managed renderer after configuring it. Existing default model directories remain available. On Linux, use the actual absolute Linux model directory. Do not copy model weights into source control.

After validating both workflows and the overlay's transparent edges locally, select them through AppHost user secrets:

```powershell
$backgroundWorkflow = (Resolve-Path .\workflows\fatewake-z-image-turbo-api.json).Path
$overlayWorkflow = (Resolve-Path .\workflows\fatewake-z-image-turbo-overlay-api.json).Path
dotnet user-secrets set "ArtGeneration:ComfyUI:WorkflowPath" $backgroundWorkflow --project src\Fatewake.AppHost
dotnet user-secrets set "ArtGeneration:ComfyUI:OverlayWorkflowPath" $overlayWorkflow --project src\Fatewake.AppHost
dotnet user-secrets set "ArtGeneration:ComfyUI:Model" "z_image_turbo_bf16.safetensors" --project src\Fatewake.AppHost
```

Restart AppHost to forward updated settings to API and Worker. Previously queued jobs retain their serialized provider/model inputs; changing settings does not repair or requeue failed jobs. These are API templates, not directly importable UI graphs, and the seed token remains unquoted until runtime substitution.

Production delivery still goes through `ArtWorkJobFactory`: preserve the master, generate both WebP and optimized PNG derivatives, and validate dimensions and required alpha before publication. Raw ComfyUI output is never a web-delivery asset. Creating these files does not enable Z-Image automatically or interrupt running generation.

## Existing checkpoint workflows

The default `fatewake-ssd1b-api.json` renders opaque 768x1344 backgrounds with Segmind SSD-1B, using 18 DPM++ 2M/Karras sampling steps and CFG 9. `fatewake-ssd1b-overlay-api.json` adds native BiRefNet background removal and an alpha join for reusable overlays. This smaller SDXL derivative runs on the tested Intel Core Ultra 7 155H / Intel Arc machine with 32 GB RAM. ComfyUI's Intel XPU installation selects FP16 and manages model offloading.

The original `fatewake-api.json` and `fatewake-overlay-api.json` remain available as 512x768 Stable Diffusion 1.5 fallback workflows. Select either pair through `ArtGeneration:ComfyUI:WorkflowPath` and `OverlayWorkflowPath` in `src/Fatewake.AppHost/appsettings.json`, and update the `Model` provenance label to match.

Install `SSD-1B-A1111.safetensors` in ComfyUI's `models/checkpoints` directory and `birefnet.safetensors` in `models/background_removal`. The fallback requires `v1-5-pruned-emaonly.safetensors`. Model weights stay outside the repository. Review generated anatomy, composition, edges, and continuity before relying on the artwork.

Model sources: [Segmind SSD-1B](https://huggingface.co/segmind/SSD-1B), [Stable Diffusion 1.5](https://huggingface.co/stable-diffusion-v1-5/stable-diffusion-v1-5/blob/main/v1-5-pruned-emaonly.safetensors) and [Comfy-Org BiRefNet](https://huggingface.co/Comfy-Org/BiRefNet/blob/main/background_removal/birefnet.safetensors). Comfy Desktop can use its shared `ComfyUI-Shared/models` directory for all three.

Run the opt-in GPU smoke tests with `FATEWAKE_COMFY_SMOKE_ROOT` set to the repository's absolute path, then `dotnet test tests/Fatewake.IntegrationTests/Fatewake.IntegrationTests.csproj --filter FullyQualifiedName~ComfyUiGenerationTests`. SSD-1B is the default profile; set `FATEWAKE_COMFY_SMOKE_PROFILE=sd15` to test the fallback. Previews and measured generation times are written to ignored `data/art/smoke`. The tests check portrait dimensions and both transparent and visible pixels in the overlay.

Use `__FATEWAKE_PROMPT__`, `__FATEWAKE_NEGATIVE__`, and `__FATEWAKE_SEED__` in the prompt, negative prompt, and seed inputs. The generation adapter substitutes these before submitting the graph. Include a Save Image output node. Reusable characters, props, foreground, lighting, and effects need a workflow that can produce transparent overlays; the prompt alone cannot guarantee an alpha channel.

The seed token intentionally appears unquoted: these files are templates, and Fatewake replaces it with a JSON integer before parsing/submitting. See [the setup and reference-image guide](../docs/COMFYUI-LOCAL-SETUP.md).

Cloud default: FLUX.2 Klein 4B Distilled (`fatewake-klein4b-api.json` / `fatewake-klein4b-overlay-api.json`), 4 steps, CFG 1, Euler, 768x1344, no upscaling. Uses https://cloud.comfy.org/api/ and HistoryPath=history_v2. Configure ArtGeneration:ComfyUI:ApiKey in AppHost user secrets or environment variables. Overlay workflow requires BiRefNet background-removal nodes on the target server. SSD-1B workflow files remain available for localhost:8188 with HistoryPath=history.
