# DayOneGameEngine

`DayOneGameEngine` implements the day-one rule set for the `day-001-injured-stranger` event. It accepts the supported actions (`help_injured_stranger`, `call_from_safety`, `stay_inside`, and `investigate_radio`) only when the snapshot identifies day one and that event.

Unsupported events and action types return a rejected `ActionResolution`. Supported choices produce deterministic state effects, Wake effects, and narrative facts; the implementation has no persistence or provider dependencies.
