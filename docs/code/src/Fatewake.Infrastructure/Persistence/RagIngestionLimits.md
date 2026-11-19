# RagIngestionLimits

Defines the 100 MiB document maximum and a separate request maximum with 1 MiB for multipart boundaries and metadata. API multipart parsing still enforces the document maximum; Kestrel allows the envelope overhead so a valid document at the exact limit is not rejected prematurely.
