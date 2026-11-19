# FallbackComfyUiArtMasterGenerator

Attempts ComfyUI artwork generation first. Transport or availability failures fall back to the configured OpenAI fallback model at low quality and low complexity, then enqueue durable regeneration through ComfyUI.
