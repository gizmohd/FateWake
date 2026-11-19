# GeneratedSceneModel

EF mapping for `generated_scene`: restrictive foreign keys to survivor and timeline, `Spec` as jsonb, and a unique `(SurvivorId, TimelineId, Sequence)` index so concurrent workers cannot reserve the same scene twice.
