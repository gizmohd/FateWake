# ComfyUiGenerationTests

Uses synthetic HTTP handlers to test background/alpha workflow selection, prompt/model/seed substitution and explicit invalid-model handling without GPU execution.

The separate opt-in smoke test requires `FATEWAKE_COMFY_SMOKE_ROOT`, a running local ComfyUI and installed workflow models. It now obtains the authored bedroom/radio requests from `SceneArtCatalog`, including the repository's shared Style Bible instructions/version, rather than maintaining an independent muted test palette. It does not change workflow graphs or generation settings. Do not enable this live test for ordinary style-contract validation.
