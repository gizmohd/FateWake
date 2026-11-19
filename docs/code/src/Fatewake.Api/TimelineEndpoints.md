# TimelineEndpoints

Authenticated canonical-owner endpoints for paginated branches/checkpoints, fork/fresh opening, and branch selection. Picker metadata includes current chapter/time, fork checkpoint label and parent identity; current Thread remains visible even outside the selected list page. Mutations are rate-limited and transactionally reject stale source timelines. Responses never serialize raw episode state, canonical facts, snapshots or hidden provenance.
