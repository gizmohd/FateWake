# Fatewake MVP Specification — Days 1–7

## Goal
Prove that Fatewake's core loop is compelling before building the full social simulation:

**Event → Decision → Anticipation → Consequence → Share/Reflect → Return**

The MVP succeeds when a player finishes Survivor Day 7 and genuinely wants to know what happens on Day 8.

## MVP Boundaries
Build one personal timeline covering Season One Days 1–7. Support deterministic state, authored choices, constrained free-form actions, Wakes, NPC memory/relationships, significant inventory/resources, journal/history, AI narration and journal understanding.

Do not require full settlement governance, large-scale multiplayer, factions, regional convergence, live player war, or complete Wayfinder/Echo revelation for the first playable milestone. Design data structures so these can be added without rewriting canonical history.

## Client Screens

### 1. Cold Open / Episode
Primary mobile screen. Begins immediately at **6:17 AM** with fiction, contextual actions and optional **Do something else...** input.

### 2. Interaction Composer
Presented choices and free-form text are peers. Both produce a CandidateAction and go through the same resolution pipeline.

### 3. Journal
Shows known history and player-authored notes. Hidden Wakes and future triggers are not exposed.

### 4. Survivor
Lightweight identity/profile: survivor name, broad region, discovered traits and meaningful possessions. No class-selection screen.

### 5. People
Known characters with qualitative relationship context and only information the player plausibly knows.

### 6. Supplies
Countable aggregate resources plus significant named items.

### 7. Episode Recap
Short diegetic recap showing what the player knows happened, unresolved concerns and optionally a journal prompt. It must not expose hidden consequence logic.

## First Session Flow
1. Open directly on Day 1 at 6:17 AM.
2. Establish outage/silence and immediate sensory context.
3. Present Maya, Eli and the injured stranger.
4. Offer several authored actions plus free-form action.
5. Resolve deterministically and create/update Wakes.
6. Gather minimal identity context naturally during play rather than before it.
7. Introduce the anomalous radio.
8. Reveal that its timestamp is tomorrow at 6:17 AM without explanatory UI.
9. Before episode end, reflect the player's first meaningful action through dialogue/state.
10. Present recap/journal.
11. If using guest play, invite the player to preserve the survivor/account after investment.

Target first meaningful decision: roughly 60–90 seconds.

## Domain Model

### Survivor
- SurvivorId
- AccountId nullable during guest flow
- DisplayIdentity
- BroadRegion
- SurvivorDay
- TimelineId
- Status
- CreatedAt

### Timeline
- TimelineId
- RealmId
- SurvivorDay
- ProgressionState
- ConvergenceState
- WorldClockPolicy

### Character
- CharacterId
- Identity
- KnowledgeState
- RelationshipState
- LocationState
- Status

### EventDefinition
Authored event/rules definition with prerequisites, available actions, deterministic effects and narrative context.

### EventInstance
The occurrence of an EventDefinition in a timeline, including observed facts and resolved outcome.

### CandidateAction
Normalized representation produced from either a presented choice or free-form input.

### ActionResolution
Authoritative deterministic result: accepted/rejected/modified action, state changes, observations, Wake mutations and narrative facts.

### Wake
- WakeId
- Origin
- Actor
- Target
- Action
- Intent
- Visibility
- Scope
- Severity
- EmotionalImpact
- WorldImpact
- Tags
- CreatedDay
- Decay/Resolution rules

### KnowledgeClaim
Tracks information separately from objective state:
- ClaimId
- Proposition
- Holder
- Source
- ProvenanceChain
- Confidence
- TruthStatus (unknown to holder as appropriate)
- Scope

### JournalEntry
- EntryId
- SurvivorId
- SurvivorDay
- OriginalText
- CreatedAt

### JournalObservation
Machine-derived, non-authoritative interpretation:
- ObservationId
- EntryId
- Category
- Subject/Entities
- Interpretation
- Confidence
- CandidateLinks
- Review/ValidationState

### ResourcePool
Countable survival resources.

### SignificantItem
Named object with state, provenance and optional Wakes.

### Relationship
Qualitative/hidden relationship dimensions used by deterministic rules; UI renders behavior and language rather than raw scores.

### GiftSignal
Hidden evidence of Traveler/Listener/Builder or Fatewake-specific manifestations. Never a conventional visible XP bar.

## State Separation
Maintain at least:
1. **World truth** — canonical deterministic facts.
2. **Character/player knowledge** — facts the survivor has plausibly learned.
3. **Player belief** — theories/suspicions inferred from actions or journal notes.

Never promote player belief into world truth merely because an AI model inferred it.

## Action Resolution Pipeline
```
Presented choice OR free-form text
        ↓
Input/safety validation
        ↓
Intent interpretation (AI allowed for free-form)
        ↓
CandidateAction
        ↓
Deterministic feasibility + rules validation
        ↓
ActionResolution
        ↓
State transaction + Wake generation
        ↓
NarrativeFacts
        ↓
AI rendering
        ↓
Persist rendered/structured history
```

AI may interpret and render. It does not commit canonical outcomes.

## API Surface
Initial logical endpoints/services:

- `POST /api/session/start`
- `GET /api/episodes/current`
- `POST /api/episodes/{id}/actions`
- `GET /api/history`
- `GET /api/people`
- `GET /api/inventory`
- `GET /api/journal`
- `POST /api/journal`
- `GET /api/survivor`
- `POST /api/account/preserve` (if guest preservation is adopted)

Internal application services should be preferred over coupling game rules to HTTP controllers.

## Persistence
PostgreSQL is authoritative. Prefer append-oriented/event-history records for consequential actions while maintaining queryable current-state projections.

Persist:
- survivors/timelines
- event instances and resolutions
- Wakes
- characters and relationships
- knowledge/provenance
- resources/items
- journal entries and derived observations
- narrative render metadata
- hidden Gift signals

AI-generated prose must never be the only record of what happened.

## AI Boundaries
AI may:
- classify free-form intent
- render deterministic outcomes
- produce contextual NPC dialogue from supplied knowledge/personality/state
- summarize known history
- analyze journal notes into candidate observations

AI may not:
- grant resources
- kill/revive characters
- alter relationships directly
- invent canonical knowledge
- bypass prerequisites
- decide whether an impossible action succeeds
- promote a theory into fact

Every AI operation should be replaceable/replayable without corrupting canonical game state.

## Days 1–7 Implementation Slice

### Day 1 — The Silence
Maya, Eli, injured stranger, outage, anomalous future-dated radio. Demonstrate choice/free-form equivalence and immediate memory.

### Day 2 — Twenty-Four Hours
Resource pooling and early trust/community behavior.

### Day 3 — 11:42 PM
Noah stealing food. Introduce morally ambiguous action and persistent person-specific Wake.

### Day 4 — The Broadcast
6:17 warning and conflict between survival strategies.

### Day 5 — The Pharmacy
Negotiation/scarcity encounter. MVP may use a deterministic simulated survivor group rather than live multiplayer while preserving an encounter contract that can later bind to real players.

### Day 6 — Someone Is Watching
Intrusion and **STOP LISTENING TO 6:17**. Journal becomes especially relevant.

### Day 7 — The First Wake
Signal: answer/silence/trace/jam/custom. For MVP, record the player's decision and simulate/seed aggregate context; production later connects this to real population aggregation.

## Architecture
Initial solution direction:

```
Fatewake.Web
Fatewake.Api
Fatewake.GameEngine
  Events
  Actions
  Wakes
  Characters
  Knowledge
  Relationships
  Resources
  Timelines
  Gifts
Fatewake.AI
  Intent
  Narrator
  Dialogue
  Journal
  Guardrails
Fatewake.Infrastructure
  Persistence
  Identity
Fatewake.Tests
```

**.NET 10 + .NET Aspire + Blazor Web App/PWA + ASP.NET Core + EF Core/Npgsql + PostgreSQL.** Aspire is the initial local orchestration foundation. Keep the system modular rather than decomposing it into microservices. Add Redis/SignalR or other infrastructure only when a validated use case requires them.

## Testing Requirements
The GameEngine must be testable without an AI provider.

Minimum automated coverage:
- authored and equivalent free-form actions converge on the same CandidateAction
- impossible free-form actions cannot bypass rules
- Wake creation and delayed trigger behavior
- NPC knowledge/perception constraints
- information provenance
- journal belief never mutates world truth
- deterministic replay of Days 1–7
- hidden Gift signals remain non-authoritative until rule thresholds/events establish them
- player history survives narrative re-rendering

## MVP Success Signals
Qualitative/product:
- player understands choices matter without seeing mechanics
- player uses or understands free-form action capability
- player notices Day 1 memory callback
- radio timestamp produces curiosity
- journal feels worth using
- Day 7 decision feels consequential
- player wants Day 8

Engineering:
- canonical state can be replayed/tested
- AI can be swapped/re-rendered safely
- Days 1–7 require no manual state repair
- event/Wake schema can support later timeline convergence and multiplayer provenance

## Explicitly Deferred
- full live multiplayer matchmaking
- settlement governance UI
- organization/faction management
- regional/global live aggregation infrastructure
- PvP implementation
- cross-Realm travel
- explicit Wayfinder terminology/reveal
- canon-promotion tooling
- native mobile client
- monetization implementation

These are architectural considerations, not MVP dependencies.
