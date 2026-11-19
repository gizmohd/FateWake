# RagRequestDiagnostics

Runs before authentication and authorization on `/api/admin/rag` only. Adds `X-Request-ID` and safe Problem Details for empty 401/403/413 responses, malformed multipart uploads, and unexpected server failures. Authentication challenges and HTTP status codes remain intact.

Logs constant structured events with the reference ID and exception type only, never raw exception messages, uploaded contents, or credentials. Records a `rag.request` operation span and marks server failures/cancellation at the handling boundary. Client disconnects propagate as cancellation. Unexpected failures are not mislabeled as successful imports or metadata errors.
