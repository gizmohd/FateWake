# SceneChoiceResolver

Pure engine gate for AI-authored scenes: a selection is accepted only if its action type was declared by the persisted scene, or it is bounded free text (max 300 chars, control characters stripped) and the scene allows free text. The model proposes choices; it never decides which actions are valid.
