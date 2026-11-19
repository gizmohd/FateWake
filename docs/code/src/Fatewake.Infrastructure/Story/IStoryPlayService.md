# IStoryPlayService

Survivor-facing scene flow. Callers authorize survivor ownership first. `GetCurrentAsync` reserves the current scene and, when it is Ready and decision-free, pre-generates the next one; scenes after a decision are generated only once it is recorded.
