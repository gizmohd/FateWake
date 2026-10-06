# GameSnapshot

`GameSnapshot` carries the survivor, timeline, day, active event key, and known facts needed to resolve an action. It is an input value for deterministic game rules and does not load or mutate persisted state itself.

The application layer is responsible for constructing a snapshot from canonical persisted data before calling `IGameEngine.Resolve`.
