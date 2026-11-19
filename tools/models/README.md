# Initial local models

Two dependency-free Python 3.10+ scripts for Windows or Linux. Run from the repository root (use python3 instead of python on Linux where needed). Both default to dry-run. These scripts configure **already installed runtimes**; they do not install drivers, CUDA/PyTorch, Ollama, ComfyUI, or custom nodes.

## Prerequisites

1. On the actual RTX Spark host, confirm OS/CPU architecture, available storage, NVIDIA driver and GPU support. 128 GB unified memory is shared with the OS, not dedicated VRAM. Hardware performance has not been validated here.
2. Install a compatible current [Ollama distribution](https://ollama.com/download) and start its local service on port 11434. Confirm that version supports Qwen3 and the think=false generate option.
3. Install [ComfyUI](https://docs.comfy.org/installation/overview) with the correct GPU runtime and start it on loopback port 8188. Do not expose either service publicly. Run setup on that host or use local SSH tunnels. URLs with remote hosts, paths, credentials, or redirects are rejected.
4. For launching Fatewake, install .NET 10 and start Docker/Podman per the repository README. The Python scripts themselves require no pip packages.

## 1. Narrative baseline: Qwen3 14B and 8B

Preview (no network or downloads):

~~~sh
python -B tools/models/setup_models.py
~~~

Download missing models, run a short sequential inference check, and save their reported digests/metadata:

~~~sh
python -B tools/models/setup_models.py --apply --smoke
~~~

For a smaller first download:

~~~sh
python -B tools/models/setup_models.py --models qwen3:8b --apply --smoke
~~~

Defaults are qwen3:14b (candidate) and qwen3:8b (baseline). Tags are mutable; installed models are not updated automatically and their actual digests are recorded in ignored data/models/ollama-inventory.json. Use --models to select an explicitly evaluated tag. Pulls are large and may take time; --timeout controls each request (default two hours). A timeout is not rollback: Ollama may retain partial/downloaded weights. Rerun after checking its state; existing models are skipped and Ollama manages partial downloads. The previous inventory file remains on failure, so do not treat it as evidence of a failed run succeeding.

Smoke requests cap context/output and unload each model after completion. This is availability validation, not a narrative-quality benchmark. Review model licensing before use. Weights remain in Ollama's configured model storage, never in Git.

**Important:** Fatewake currently uses a keyword intent interpreter, not a narrative LLM client. This prepares evaluation models only; it does not invent unused application settings or configure OpenClaw. Fine-tuning, manuscript ingestion, embeddings/pgvector and game integration are separate work.

## 2. Local ComfyUI configuration

Install the checkpoint and all nodes required by your chosen workflow through their official sources after reviewing licenses. Generate successfully in ComfyUI with both background and overlay graphs, and verify transparent overlay output. Export API templates with Fatewake's prompt/unquoted-seed tokens and its negative token where the model supports negative conditioning (distilled FLUX graphs may omit it). See [the existing guide](../../docs/COMFYUI-LOCAL-SETUP.md) and [workflow profiles](../../workflows/README.md).

Example using the existing **legacy SD1.5** pair (not the new 32-bit-era high-color art pipeline):

~~~sh
python -B tools/models/setup_comfyui.py --workflow workflows/fatewake-api.json --overlay workflows/fatewake-overlay-api.json --model v1-5-pruned-emaonly.safetensors
~~~

This parses templates without contacting ComfyUI. Then validate installed nodes/model choices and save local settings:

~~~sh
python -B tools/models/setup_comfyui.py --workflow workflows/fatewake-api.json --overlay workflows/fatewake-overlay-api.json --model v1-5-pruned-emaonly.safetensors --apply --confirm-workflows-tested
~~~

Substitute the SSD-1B pair or your validated high-color background/alpha-overlay templates and the correct model provenance label as appropriate. The current application selects background versus overlay workflows; it does not yet route three separate background/portrait/sprite workflows. No specific high-color art checkpoint, LoRA, or model download URL is assumed. The tool checks node availability and literal combo choices against /object_info, but cannot prove graph wiring, output quality, or alpha; that is why explicit prior-generation confirmation is required.

The result is ignored data/models/comfyui-environment.json. It is an environment mapping, **not an automatically loaded appsettings file**. Add --launch to the same command to start AppHost with those process-local settings. This switches that launch from the current cloud endpoint to loopback, uses history rather than history_v2, sets absolute workflow paths, and masks the inherited cloud API key with a whitespace value that the provider normalizes to null. It does not edit appsettings or user secrets. Stop this AppHost normally before launching another; ordinary subsequent launches retain the previous configuration. Existing persistent containers may remain running as documented in the main README.

The setup check never submits image generation. However, --launch starts the normal app/worker: existing queued jobs or subsequent gameplay can trigger local GPU work. Do not use --launch on a shared production database. The script assumes API/Worker run on the same host as ComfyUI; loopback is not correct for services inside separate containers.

Saved local configuration is replaced atomically on success. Run only one setup writer per output path. To select another output use --output. No user secrets, manuscripts, or hosted API credentials are collected.

## Verification

~~~sh
python -B -m unittest discover -s tools/models -p test_setup.py -v
~~~

Offline tests mock service calls and process launches. They cover dry-run safety, origin restrictions, existing-model reuse, failure handling, bounded inference, template parsing, missing nodes/models, configuration output, and process-local overrides. Real GPU/model smoke checks must still be run on the intended host.
