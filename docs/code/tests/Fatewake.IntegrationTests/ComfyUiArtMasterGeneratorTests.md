# ComfyUiArtMasterGeneratorTests

Verifies optional ComfyUI API-key support without a real ComfyUI: the key is sent in the configured header (or as a bearer token for `Authorization`) on every ComfyUI request, never forwarded to the `/view` redirect target, and omitted entirely when empty.
