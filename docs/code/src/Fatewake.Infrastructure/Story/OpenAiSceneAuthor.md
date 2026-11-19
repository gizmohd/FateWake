# OpenAiSceneAuthor

OpenAI Responses implementation with a strict JSON schema, no tools, and tracked usage (`TrackedAiCall`). 429/5xx are transient (retried); 4xx and refusals are terminal. Prompts and responses are never logged.
