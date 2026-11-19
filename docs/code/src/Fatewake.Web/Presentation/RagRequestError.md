# RagRequestError

Immutable administrator-facing diagnostics with operation, HTTP status, safe explanation, error code, request reference, and UTC timestamp. Maps empty authorization and size-limit responses to actionable messages. Parses only bounded JSON fields, never HTML or an entire raw response. Invalid/oversized diagnostic responses are reported explicitly.
