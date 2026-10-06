# SurvivorVisualResolver

`SurvivorVisualResolver` applies per-item emphasis, carry visibility, and pose compatibility to produce ordered visible visuals. It fingerprints identity, state version, scene context, and resolved assets for snapshots, then maps the projection to composition layers.

The resolver is in-memory and deterministic apart from the snapshot's capture timestamp. It has no persistence or external provider dependency.
