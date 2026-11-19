# RagEndpoints

Maps protected multipart RAG upload staging and corpus status metrics. Validates file size and required metadata before storing document bytes and a durable Worker import job in PostgreSQL. Text extraction happens later in the Worker, not on the upload request.

Malformed uploads and authorization failures use `RagRequestDiagnostics` for safe, correlated errors. Unexpected persistence/queue failures remain server errors and never expose raw exception messages or claim a successful import.
