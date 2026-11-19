# ArtPromptAuthoringWorkStepHandlerTests

Uses an in-memory context and fake author/job factory to verify that delayed work consumes the request-time canonical/style snapshot, pins the resulting immutable revision into its successor image request, and safely reuses the saved revision on recovery without a second provider call or duplicate job. No live provider or database is used.
