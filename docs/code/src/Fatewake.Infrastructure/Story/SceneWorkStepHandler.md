# SceneWorkStepHandler

Worker step: load the reserved scene, require the previous scene Ready (retry otherwise), retrieve canon via `IRagRetriever`, author, validate and persist. No canon or disabled authoring marks the scene Failed with an explicit error. Provider failures retry up to five attempts; invalid output up to three.
