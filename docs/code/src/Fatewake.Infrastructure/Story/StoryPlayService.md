# StoryPlayService

Current scene is the lowest sequence without `CompletedAt` (or max+1). `AdvanceAsync` completes decision-free scenes; `ChooseAsync` validates through `SceneChoiceResolver`, stores the choice (free text only for freeform) and completes the scene. Completed scenes are never reopened; invalid transitions throw `InvalidOperationException` (HTTP 409).
