# OperationTelemetry

BCL ActivitySource/Meter instrumentation with optional Microsoft typed logging supplied by callers. Source/meter name is Fatewake.Operations. Scope disposal emits seconds to fatewake.operation.duration with fixed operation/outcome labels; Fail marks error/cancellation and exports exception type without message. Disposal is idempotent.

Spans preserve Activity.Current parenting. Duration-only scopes end with outcome ended, not an assertion of business success. Exceptions handled at API/worker/form boundaries are marked explicitly. Libraries must not supply user-controlled names or metric dimensions. No host, exporter, EF, or Serilog dependency; deterministic GameEngine does not reference this project.
