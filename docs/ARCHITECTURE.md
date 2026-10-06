# Technical Architecture

## Initial Direction
Prototype Fatewake as a mobile-first web application/PWA. Evaluate native packaging later after validating the gameplay loop.

## Proposed Stack
- Blazor Web App / PWA
- ASP.NET Core APIs
- PostgreSQL
- Redis later as required
- SignalR later for realtime/social/global events
- AI provider/model abstraction
- Azure initial hosting target

## Proposed Solution

    Fatewake
    ├── Fatewake.Web
    ├── Fatewake.Api
    ├── Fatewake.GameEngine
    │   ├── Events
    │   ├── Choices
    │   ├── Consequences
    │   ├── Characters
    │   ├── Relationships
    │   ├── Resources
    │   └── WorldState
    ├── Fatewake.AI
    │   ├── Narrator
    │   ├── Dialogue
    │   └── ContentGuardrails
    ├── Fatewake.Infrastructure
    └── Fatewake.Tests

## Boundary
Game state and consequence resolution remain deterministic and testable. AI-generated text is presentation layered on authoritative state.

## Near-Term Goal
Implement only enough architecture for a playable Days 1–7 vertical slice, validate the return loop, then expand.

## Authoritative Data Platform
PostgreSQL is the authoritative datastore for Fatewake. The initial architecture deliberately favors one transactional source of canonical truth over a polyglot persistence stack.

Use conventional relational structures for well-understood domain entities and relationships; JSONB for flexible event, AI and evolving metadata; append-oriented records for consequential history; and graph-friendly edge/relationship structures plus recursive queries where causal, social, information or Wake traversal is required.

The primary graph domains are expected to include:
- world/social relationships between survivors, characters, settlements, organizations, regions and Realms;
- causal relationships between decisions, events and Wakes;
- information provenance showing how claims, secrets, rumors and discoveries move between actors.

PostgreSQL extensions such as pgvector may be adopted when semantic retrieval becomes necessary. Redis or a dedicated graph/search platform should be added only after a concrete performance or capability requirement justifies another consistency boundary. MongoDB and a dedicated graph database are not required for the MVP.
