# Fatewake Domain and PostgreSQL Model

## Design Principle

Fatewake separates three concerns:

1. **Canonical history** — immutable/consequential records of what happened.
2. **Current projections** — efficient relational state representing what is true now.
3. **Relationship graphs** — causal, social and informational connections that explain how one event affects another.

PostgreSQL is the authoritative source for all three.

## Identity and Timeline

### survivor
Represents the playable identity, separate from the authentication account.

- id uuid PK
- account_id uuid nullable
- timeline_id uuid FK
- display_name text
- identity_mode text
- broad_region text
- survivor_day integer
- status text
- created_at timestamptz
- updated_at timestamptz

### timeline
- id uuid PK
- realm_id uuid FK
- progression_state text
- convergence_state text
- world_clock_policy text
- current_survivor_day integer
- created_at timestamptz

### realm
- id uuid PK
- key text unique
- name text
- properties jsonb

## Canonical Event Ledger

### game_event
Append-oriented authoritative history.

- id uuid PK
- timeline_id uuid FK
- survivor_id uuid nullable FK
- event_type text
- aggregate_type text
- aggregate_id uuid nullable
- sequence bigint
- survivor_day integer
- occurred_at timestamptz
- causation_event_id uuid nullable FK game_event
- correlation_id uuid
- payload jsonb
- schema_version integer
- created_at timestamptz

Important rule: game_event payloads describe canonical outcomes, not merely AI prose.

Unique/index strategy:
- unique (timeline_id, sequence)
- index aggregate_type + aggregate_id + sequence
- index correlation_id
- index causation_event_id
- GIN payload only for demonstrated query patterns

## Authored Content and Runtime Episodes

### event_definition
Versioned authored rules/content definition.

- id uuid PK
- key text
- version integer
- event_type text
- prerequisites jsonb
- action_definitions jsonb
- rules jsonb
- narrative_context jsonb
- active boolean

Unique (key, version).

### event_instance
- id uuid PK
- definition_id uuid FK
- timeline_id uuid FK
- survivor_id uuid FK
- status text
- survivor_day integer
- started_at timestamptz
- resolved_at timestamptz nullable
- state jsonb

### action_attempt
Stores both authored-choice and free-form submissions.

- id uuid PK
- event_instance_id uuid FK
- survivor_id uuid FK
- input_kind text
- raw_input text nullable
- authored_action_key text nullable
- candidate_action jsonb
- interpretation_metadata jsonb nullable
- submitted_at timestamptz

### action_resolution
- id uuid PK
- action_attempt_id uuid unique FK
- outcome_type text
- resolved_action jsonb
- authoritative_effects jsonb
- narrative_facts jsonb
- resolution_version integer
- resolved_at timestamptz

The GameEngine owns action_resolution. AI may help construct a CandidateAction but cannot write authoritative_effects.

## Wakes and Causality

### wake
- id uuid PK
- timeline_id uuid FK
- origin_event_id uuid FK game_event
- wake_type text
- scope text
- actor_entity_type text
- actor_entity_id uuid nullable
- target_entity_type text nullable
- target_entity_id uuid nullable
- intent text nullable
- visibility text
- severity smallint
- emotional_impact smallint nullable
- world_impact smallint nullable
- state text
- properties jsonb
- created_day integer
- created_at timestamptz
- resolved_at timestamptz nullable

### wake_edge
Creates the causal Wake graph.

- from_wake_id uuid FK
- to_wake_id uuid FK
- edge_type text
- source_event_id uuid FK
- properties jsonb
- created_at timestamptz

PK (from_wake_id, to_wake_id, edge_type).

Examples of edge_type:
- caused
- amplified
- mitigated
- revealed
- fulfilled
- contradicted

Recursive CTEs can traverse downstream/upstream Wake history.

## Generic World Relationships

### entity_relationship
Used for relationships that benefit from graph traversal without replacing strongly typed domain tables.

- id uuid PK
- timeline_id uuid FK
- from_entity_type text
- from_entity_id uuid
- to_entity_type text
- to_entity_id uuid
- relationship_type text
- state jsonb
- source_event_id uuid FK
- valid_from timestamptz
- valid_until timestamptz nullable

Examples:
- MEMBER_OF
- ALLIED_WITH
- OWES_DEBT_TO
- ENEMY_OF
- LED_BY
- PARENT_OF
- LOCATED_IN

Indexes should cover both forward and reverse traversal.

## Characters and Relationships

### character
- id uuid PK
- timeline_id uuid FK
- character_key text nullable
- display_name text
- character_type text
- status text
- current_location_id uuid nullable
- properties jsonb

### survivor_character_relationship
Projection for frequently accessed interpersonal state.

- survivor_id uuid FK
- character_id uuid FK
- trust_band text
- affinity_band text
- fear_band text
- obligation_band text
- familiarity_band text
- private_state jsonb
- last_event_id uuid FK
- updated_at timestamptz

PK (survivor_id, character_id).

Numeric internals may exist when useful to deterministic rules, but presentation remains qualitative.

## Truth, Knowledge and Belief

### canonical_fact
Objective world truth that needs explicit fact representation.

- id uuid PK
- timeline_id uuid FK
- fact_type text
- subject_entity_type text
- subject_entity_id uuid nullable
- predicate text
- object_value jsonb
- source_event_id uuid FK
- valid_from timestamptz
- valid_until timestamptz nullable

Not every state row needs a duplicate canonical_fact; use this for facts participating in knowledge/information mechanics.

### knowledge_claim
Represents what an actor has learned/heard/believes about a proposition.

- id uuid PK
- timeline_id uuid FK
- holder_entity_type text
- holder_entity_id uuid
- canonical_fact_id uuid nullable FK
- proposition jsonb
- epistemic_state text
- confidence numeric
- source_claim_id uuid nullable FK knowledge_claim
- source_entity_type text nullable
- source_entity_id uuid nullable
- acquired_event_id uuid FK
- acquired_at timestamptz

epistemic_state examples:
- observed
- told
- inferred
- suspected
- believed
- disproven

A claim may be false, uncertain or disconnected from canonical_fact.

### information_transmission
Explicit provenance edge.

- id uuid PK
- source_claim_id uuid FK
- resulting_claim_id uuid FK
- sender_entity_type text nullable
- sender_entity_id uuid nullable
- recipient_entity_type text
- recipient_entity_id uuid
- channel text
- transmission_event_id uuid FK
- properties jsonb
- transmitted_at timestamptz

This supports chains such as Darren → Maya → Jonah → Haven while allowing each retelling to alter confidence/content.

## Journal

### journal_entry
The player's original text is immutable source material.

- id uuid PK
- survivor_id uuid FK
- survivor_day integer
- body text
- created_at timestamptz
- edited_at timestamptz nullable

If editing is supported, retain revision history rather than destroying the original observation.

### journal_entry_revision
- id uuid PK
- journal_entry_id uuid FK
- revision integer
- body text
- created_at timestamptz

### journal_observation
AI-derived and non-authoritative.

- id uuid PK
- journal_entry_id uuid FK
- observation_type text
- subject_refs jsonb
- interpretation text
- confidence numeric
- candidate_fact_id uuid nullable
- status text
- model_metadata jsonb
- created_at timestamptz

No journal_observation can directly mutate canonical_fact.

## Resources and Items

### resource_balance
Projection of fungible resources.

- owner_entity_type text
- owner_entity_id uuid
- resource_type text
- quantity numeric
- updated_by_event_id uuid FK
- updated_at timestamptz

PK (owner_entity_type, owner_entity_id, resource_type).

Initial resource types:
Food, Water, Medicine, Energy, Security.

### significant_item
- id uuid PK
- timeline_id uuid FK
- item_type text
- name text
- owner_entity_type text nullable
- owner_entity_id uuid nullable
- state jsonb
- provenance jsonb
- created_by_event_id uuid FK
- updated_at timestamptz

Significant items can also participate in entity_relationship and Wakes.

## Gift Signals

### gift_signal
Hidden evidence rather than visible skill progression.

- id uuid PK
- survivor_id uuid FK
- gift_family text
- signal_type text
- strength numeric
- source_event_id uuid FK
- evidence jsonb
- created_at timestamptz

Signals do not automatically establish that a character is a Wayfinder-equivalent.

## Narrative Rendering

### narrative_render
AI/static presentation generated from authoritative NarrativeFacts.

- id uuid PK
- event_instance_id uuid nullable FK
- action_resolution_id uuid nullable FK
- render_type text
- content text
- input_facts jsonb
- provider text nullable
- model text nullable
- prompt_version text nullable
- created_at timestamptz

Narrative renders are replaceable presentation artifacts, never canonical state.

## Concurrency

Every player action is processed transactionally.

For an accepted action:
1. Lock/check the relevant timeline/event instance version.
2. Validate CandidateAction against current authoritative state.
3. Insert ActionResolution.
4. Append resulting game_event records.
5. Create/update Wakes and graph edges.
6. Update current-state projections.
7. Commit.
8. Render narration from committed NarrativeFacts.

Use optimistic concurrency/version fields on mutable projections where simultaneous actions are possible.

## C# Aggregate Boundaries

Initial GameEngine concepts:

```csharp
Survivor
Timeline
Episode
CandidateAction
ActionResolution
Wake
WakeEdge
Character
RelationshipState
CanonicalFact
KnowledgeClaim
ResourceState
SignificantItem
GiftSignal
```

The domain layer should use strongly typed IDs/value objects where practical and must not contain EF Core attributes or dependencies.

### Core contract

```csharp
public interface IGameEngine
{
    ActionResolution Resolve(
        GameSnapshot state,
        CandidateAction action);
}
```

Resolution should be deterministic for the same canonical snapshot, action and ruleset version.

## EF Core Boundary

Fatewake.Infrastructure owns:
- DbContext
- entity configurations
- migrations
- Npgsql-specific features
- JSONB mapping
- persistence repositories/query services

Fatewake.GameEngine owns domain behavior and has no reference to Infrastructure.

Avoid forcing every persistence table into a rich domain entity. Ledger, render and provenance records can be persistence/application records while core gameplay concepts remain domain types.

## First Migration Scope

The first executable Day 1 migration only needs:
- realm
- timeline
- survivor
- character
- event_definition
- event_instance
- action_attempt
- action_resolution
- game_event
- wake
- wake_edge
- survivor_character_relationship
- canonical_fact
- knowledge_claim
- journal_entry
- journal_observation
- resource_balance
- significant_item
- narrative_render

Add generalized organization/settlement and broader information-transmission structures as their gameplay enters the executable slice, while retaining this model as the target architecture.

## Generated Content Assets

### generated_content_asset
Durable reusable AI/authored content.

- id uuid PK
- asset_type text
- content_key text nullable
- content text/jsonb
- context_fingerprint text
- applicability jsonb
- prompt_version text nullable
- rules_version text nullable
- content_version integer
- provider text nullable
- model text nullable
- validation_status text
- token_usage jsonb nullable
- estimated_cost numeric nullable
- usage_count bigint
- created_at timestamptz
- last_used_at timestamptz nullable
- superseded_by_id uuid nullable

Index exact reusable lookups by asset_type + context_fingerprint + validation_status. Content with player-private inputs must include those constraints in applicability/fingerprinting and must not leak across survivors.

### generated_content_usage
Records where an asset was reused or generated so quality and savings can be measured.

- id uuid PK
- asset_id uuid FK
- survivor_id uuid nullable FK
- event_instance_id uuid nullable FK
- usage_type text
- used_at timestamptz

Narrative renders may reference generated_content_asset rather than duplicating provenance on every use.

## Accounts and External Identity

### account
- id uuid PK
- display_name text nullable
- primary_email text nullable
- status text
- created_at timestamptz
- updated_at timestamptz

### external_identity
- id uuid PK
- account_id uuid FK
- provider text
- provider_subject text
- email text nullable
- email_verified boolean nullable
- display_name text nullable
- claims_snapshot jsonb nullable
- linked_at timestamptz
- last_login_at timestamptz

Unique (provider, provider_subject). Do not use email as the external identity key. One account may link Google, Microsoft and Apple identities.

A guest Survivor may initially have account_id = null. During account preservation/linking, create or resolve the account and attach the existing Survivor to it transactionally; never discard the guest timeline merely because authentication was added.


## Survivor Visual State

Visual appearance is a projection of canonical survivor/item state, but historical visual snapshots are retained for reproducible journal/history panels.

### survivor_visual_profile
- survivor_id uuid PK/FK
- identity_asset_key text
- identity_asset_version integer
- state_version bigint
- traits jsonb
- updated_event_id uuid
- updated_at timestamptz

### survivor_visual_equipment
- survivor_id uuid FK
- slot text
- asset_key text
- asset_version integer
- item_instance_id uuid nullable
- priority integer
- source_event_id uuid
- effective_from timestamptz
- effective_to timestamptz nullable

### visual_snapshot
- id uuid PK
- survivor_id uuid FK
- source_event_id uuid nullable
- state_version bigint
- fingerprint text
- snapshot jsonb
- captured_at timestamptz

The inventory/equipment domain remains authoritative. These records are presentation projections/provenance and must not grant ownership or gameplay capabilities.
