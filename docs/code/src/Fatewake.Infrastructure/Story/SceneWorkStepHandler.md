# SceneWorkStepHandler

The worker loads the reserved scene and waits for the previous scene to be ready. It retrieves canon, authors the scene, validates it and persists the result. Disabled authoring marks the scene failed. Provider failures retry up to five attempts and invalid output up to three.

If retrieval yields no usable approved passages, the narrator uses the authored world rules and any earlier reviewed facts from that Thread. The worker records the reviewed scene summary in the RAG corpus as private Thread continuity in the same database save as the ready scene. Later scenes can retrieve it within their Thread, day, spoiler and content limits. Shared canon continues to follow the editorial approval process.
