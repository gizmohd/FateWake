# StateEffect

`StateEffect` describes a direct update to a canonical game-state value. The type and key identify the state domain and value, while `Value` carries the resulting value.

Effects are returned rather than applied by the game engine, keeping rule evaluation independent of persistence and making results straightforward to test.
