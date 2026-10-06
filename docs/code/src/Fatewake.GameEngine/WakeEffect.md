# WakeEffect

`WakeEffect` represents a persistent consequence of a player action. Its category, scope, optional target, severity, and properties allow downstream application logic to record and apply the consequence to future events or relationships.

The game engine returns Wake effects as part of an `ActionResolution`; persistence and later traversal are handled outside the deterministic core.
