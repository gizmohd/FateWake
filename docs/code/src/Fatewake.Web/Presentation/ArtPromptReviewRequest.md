# ArtPromptReviewRequest

Web-side request contract posted to the API's prompt review endpoint. The Web project does not reference `Fatewake.Api`, so it mirrors `Fatewake.Api.ArtPromptReviewRequest`.

Invariants: property names and types must stay identical to the API record. `ExpectedRevisionId` is the latest revision the editor saw; the API rejects stale values.
