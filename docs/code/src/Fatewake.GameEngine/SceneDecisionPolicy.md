# SceneDecisionPolicy

Pure, deterministic pacing rule: at most `MaxDecisionFreeScenes` (4) consecutive scenes may omit a decision, so the next scene must offer one. `TryAccept` is the server-side gate used by scene generation; model output is never trusted to enforce it. A decision resets the streak to zero.
