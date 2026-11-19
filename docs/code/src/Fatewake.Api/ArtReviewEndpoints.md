# ArtReviewEndpoints

Administrator-only prompt review shows the exact recorded positive/negative image submission and provider provenance for the selected immutable asset version. Historic missing submissions are visibly unavailable rather than reconstructed. New positive/negative edits create immutable SQL revisions with optimistic conflict checks. Save-only revisions update the draft head but do not change serving bindings; explicit selected regeneration pins a revision and durable replacement request. The prior approved asset remains available until validated replacement finalization; only the newest requested revision can become the current prompt binding.

