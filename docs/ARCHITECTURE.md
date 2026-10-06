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
