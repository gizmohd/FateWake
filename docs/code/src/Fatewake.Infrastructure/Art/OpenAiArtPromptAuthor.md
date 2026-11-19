# OpenAiArtPromptAuthor

Uses the OpenAI Responses endpoint with a strict snake_case JSON schema, reasoning effort `none`, no tools, bounded output, and a request-time server-owned visual snapshot. The parser normalizes malformed or unsafe provider output to payload-free validation errors; usage attempts are closed on success, provider failure, cancellation, and timeout without adding prompt text or identifiers to operation telemetry.
