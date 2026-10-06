# ComfyUI Local Image Generation for Fatewake

## Purpose
Fatewake can use ComfyUI as a local `IArtMasterGenerator`. Fatewake owns canonical prompts, visual fingerprints, durable work state and final assets. ComfyUI owns the rendering graph and local model execution.

## 1. Install ComfyUI
Install ComfyUI using the current official ComfyUI distribution appropriate for the host. A GPU installation is strongly recommended. On Windows, the desktop/portable distribution is the simplest development option. On Linux, install ComfyUI with the appropriate NVIDIA/AMD/PyTorch runtime for the machine.

Start ComfyUI and verify its web interface is reachable. Fatewake defaults to:
`http://127.0.0.1:8188/`

Do not expose an unauthenticated ComfyUI instance directly to the public Internet.

## 2. Install a model
Install a checkpoint/workflow appropriate for the available GPU. Fatewake deliberately does not hard-code FLUX, SDXL, or another model. Put model files in the locations expected by ComfyUI and verify a normal generation succeeds in the ComfyUI UI first.

## 3. Build the Fatewake workflow
Create a normal text-to-image workflow in ComfyUI. It must end in a Save Image/output node.

Use these literal placeholder values in the workflow:
- positive prompt: `__FATEWAKE_PROMPT__`
- negative prompt: `__FATEWAKE_NEGATIVE__`
- sampler seed: `__FATEWAKE_SEED__`

The seed placeholder must be a JSON number in the exported API workflow, not a quoted string. Fatewake derives a stable seed from the durable job idempotency key.

Export/save the workflow in ComfyUI's API JSON format, not merely the UI graph format. Store it where the Worker can read it, for example:
`workflows/fatewake-api.json`

For containers/Kubernetes, mount this file through configuration or a read-only volume.

## 4. Configure Fatewake
Example configuration:

```json
{
  "ArtGeneration": {
    "Provider": "ComfyUI",
    "ComfyUI": {
      "Enabled": true,
      "BaseUrl": "http://host.docker.internal:8188/",
      "WorkflowPath": "workflows/fatewake-api.json",
      "PromptToken": "__FATEWAKE_PROMPT__",
      "NegativePromptToken": "__FATEWAKE_NEGATIVE__",
      "SeedToken": "__FATEWAKE_SEED__",
      "PollIntervalMilliseconds": 1000,
      "TimeoutSeconds": 300
    }
  }
}
```

Use `127.0.0.1` when Worker and ComfyUI run on the same host without container isolation. `host.docker.internal` is commonly useful when the Worker is containerized and ComfyUI runs on the host. In Kubernetes, use a private Service/DNS name instead.

## 5. API lifecycle
The native provider:
1. loads the configured API workflow;
2. substitutes the canonical Fatewake prompt, negative instructions and deterministic seed;
3. POSTs the graph to ComfyUI `/prompt`;
4. retains the returned `prompt_id` as provider provenance;
5. polls `/history/{prompt_id}`;
6. reads the first produced image descriptor;
7. downloads it through `/view`;
8. returns those bytes as the master to Fatewake.

Fatewake then performs its normal durable storage, WebP, optimized PNG, metadata, validation and finalization stages.

## 6. Reference images / character continuity
For character/location reference workflows, add ComfyUI Load Image plus IP-Adapter/ControlNet/reference nodes and use the role tokens documented below. Fatewake uploads approved references and substitutes their ComfyUI input filenames automatically.

Do not encode character identity solely into free-form prompts when an approved reference asset exists.

### Native reference-image tokens

Fatewake now uploads each approved `ArtReferenceImage` through ComfyUI `/upload/image` before submitting the workflow. Each uploaded image gets a deterministic job/role/hash name.

Place a literal token in the filename field of the appropriate ComfyUI Load Image node:

- character identity: `__FATEWAKE_REFERENCE_CHARACTER__`
- location continuity: `__FATEWAKE_REFERENCE_LOCATION__`
- style reference: `__FATEWAKE_REFERENCE_STYLE__`
- any custom role follows `__FATEWAKE_REFERENCE_<NORMALIZED_ROLE>__`

For example, connect a Load Image node containing `__FATEWAKE_REFERENCE_CHARACTER__` to the image input of the IP-Adapter/reference-conditioning nodes used by your installed workflow. Fatewake replaces the token with the uploaded ComfyUI input filename before `/prompt` is called.

Roles are normalized to uppercase letters/numbers with punctuation converted to underscores. Keep one reference per role in a workflow unless the workflow intentionally defines distinct role names such as `CHARACTER_FACE` and `CHARACTER_BODY`.

The exact IP-Adapter/ControlNet/custom-node graph is intentionally not hard-coded into Fatewake. Build and validate that graph in ComfyUI, export it in API format, and retain the Fatewake tokens in the exported JSON.

## 7. Smoke test
Before enabling Fatewake:
1. generate one image successfully in ComfyUI;
2. export its API workflow;
3. replace positive/negative/seed values with the Fatewake tokens;
4. verify `POST /prompt` accepts that graph;
5. verify `/history/{prompt_id}` reports the completed image;
6. verify `/view` returns the image;
7. enable `ArtGeneration:Provider=ComfyUI`;
8. submit one non-production Fatewake artwork job and verify master + WebP + optimized PNG artifacts.

## 8. Operations
Treat ComfyUI as GPU infrastructure. Keep it private, persist model caches separately from Fatewake assets, monitor VRAM/RAM/disk, and use a bounded Fatewake queue concurrency appropriate for GPU capacity. Multiple Fatewake worker pods may share one ComfyUI endpoint because PostgreSQL remains authoritative for Fatewake work state.

The ComfyUI output directory is not Fatewake's canonical asset store. Fatewake downloads the completed master and persists it through `IArtBinaryStorage`.

## 9. Troubleshooting
- HTTP connection failure: verify BaseUrl/network/firewall.
- `/prompt` 400: API workflow is invalid or contains unavailable nodes/models.
- timeout: increase TimeoutSeconds or inspect the ComfyUI queue/GPU.
- no output image: ensure the workflow terminates in an image output/Save Image node.
- malformed JSON after substitution: ensure prompt tokens occur inside JSON string values and the seed token occurs as a numeric value.
- container cannot reach localhost: localhost is the container itself; use the host gateway or a private service name.
- missing custom node: install the workflow's required custom node and restart ComfyUI.

## Security
Keep ComfyUI on a trusted/private network. Do not place secrets in workflow JSON. Fatewake does not require a ComfyUI API key by default; put authentication in a private reverse proxy if remote access is required.
