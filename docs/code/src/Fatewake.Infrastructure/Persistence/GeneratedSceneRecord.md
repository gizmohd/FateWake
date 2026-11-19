# GeneratedSceneRecord

One AI-authored scene per survivor timeline `Sequence` (unique). Rows are reserved `Pending`, then become `Ready` once the server validated `Spec` (a JSON `SceneDefinition`) or `Failed` with an `Error`. Ready content is never edited. `HasDecision` and `DecisionFreeStreak` are computed by the server, never trusted from the model: the streak counts consecutive decision-free scenes up to and including this one, and may not exceed 4 (the next scene must then have a decision). `RagSourceId` links the private `GeneratedStory` source written back to the corpus.
