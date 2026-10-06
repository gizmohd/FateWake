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

## .NET and Aspire Foundation
The MVP targets **.NET 10** and uses **.NET Aspire** for local orchestration, service discovery, configuration, health/telemetry integration and PostgreSQL development resources.

The initial system is deliberately a modular application rather than a microservice estate. Logical boundaries remain explicit so components can be separated later without paying the operational cost prematurely.

Initial projects:

```
Fatewake.AppHost
Fatewake.ServiceDefaults
Fatewake.Web
Fatewake.Api
Fatewake.GameEngine
Fatewake.AI
Fatewake.Infrastructure
Fatewake.Tests
```

Dependency direction should preserve the GameEngine as a deterministic core with no dependency on Aspire, EF Core, PostgreSQL, HTTP or an AI provider. Infrastructure implements persistence and external integrations; AI interprets/renders around authoritative GameEngine results; API/application orchestration coordinates use cases; Web presents them.

Aspire initially orchestrates the web/API application and PostgreSQL. Additional resources such as Redis should be introduced only when their use case is implemented and measured.


## Cross-platform deployment baseline

Fatewake server applications and supporting tools must support both **Linux** and **Windows** as first-class runtime environments. Production workloads may run in Linux containers or directly on Windows hosts.

Implementation rules:
- Do not introduce OS-specific filesystem separators, shell assumptions, executable paths or native dependencies without a cross-platform implementation.
- Native libraries must include/test the required Linux and Windows runtime assets.
- File/object storage keys use portable logical paths; local filesystem adapters translate them using platform APIs.
- Image generation, processing, persistence and asset validation must operate on both Linux and Windows.
- CI should validate both Linux and Windows when changes touch runtime/platform-sensitive code.
- Container support must not make direct Windows execution a second-class configuration, and Windows support must not require Windows containers.


## Source code documentation and file structure

Fatewake uses a strict source-documentation standard for all new and modified C# code.

- **One declared type per C# source file.** Classes, interfaces, records, structs, and enums each receive their own file. Nested private implementation types are discouraged and should be extracted when they have independent behavior. Compiler-generated/partial framework patterns may be exceptions only when the framework requires them.
- File names match the declared type name.
- Every public/internal type has XML documentation explaining its responsibility and architectural role.
- Public/internal constructors, properties, methods, parameters, return values, exceptions, concurrency/idempotency behavior, and important side effects are documented with XML comments where applicable.
- Comments explain intent, invariants, lifecycle and non-obvious behavior rather than restating syntax.
- Every C# source file has a companion Markdown document dedicated to that type. The canonical convention is `docs/code/<project-relative-source-path-without-.cs>.md`. Example: `src/Fatewake.Infrastructure/Work/WorkJobBuilder.cs` is documented by `docs/code/src/Fatewake.Infrastructure/Work/WorkJobBuilder.md`.
- The type-level XML documentation in each C# file must include a relative link to its companion Markdown document using an XML `<see href="...">` reference. The path is relative from the C# source file to the companion document so it remains usable in repository/source views. Example: `<see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkJobBuilder.md">WorkJobBuilder documentation</see>` (the exact number of `../` segments must match the source location).
- Companion documentation describes purpose, usage, dependencies, inputs/outputs, lifecycle, thread/concurrency behavior, distributed/Kubernetes considerations, failure/retry/idempotency semantics, configuration and a usage example when meaningful.
- Documentation changes ship in the same commit/change set as source behavior changes.
- Generated code, EF migrations and framework-generated artifacts may be exempt from one-type-per-file/companion-document requirements. Conventional top-level `Program.cs` application entry points are also explicitly exempt: keep idiomatic .NET/Aspire top-level hosting code rather than introducing a synthetic `Program` class solely for documentation compliance. Types declared alongside a `Program.cs` entry point are not exempt and must be moved to their own documented files.

Existing code predating this decision is technical debt. When an existing file is materially modified, it should be brought toward this standard rather than adding more undocumented/multi-type structure.


## Reusable Character Appearance Assets

Character appearance customization is modeled as durable canonical state, not as an instruction to regenerate artwork on every use.

The visual identity pipeline is:

`Base Identity → Appearance Configuration → Equipment/Loadout → Condition/Injuries → Scene/Pose/Camera → Rendered Asset`

Appearance configuration includes stable normalized values such as hairstyle, hair color, eye color, facial hair style/color and other supported mutable identity features. Each normalized combination receives a deterministic fingerprint that includes the base identity/reference version and every appearance property material to the resulting artwork.

Before any AI generation, Fatewake must perform an exact approved-asset lookup by this fingerprint. Once a combination has been generated, validated and approved, that asset remains reusable. Switching away from a configuration does not supersede or delete it; switching back should resolve the previously approved asset without another provider generation call.

Appearance assets and scene renders are separate cache/reuse layers. A reusable appearance asset can feed many equipment states and scenes. A fully rendered scene additionally fingerprints the canonical appearance version, loadout, condition, injuries, pose, camera, environment and other material visual state.

Generated variants retain immutable provenance including their parent/base identity, normalized appearance state, source/reference assets, prompt/provider/model details and generation cost. Semantic similarity may locate candidates but cannot substitute a different appearance combination.

Default male/female player identities are valid base identities and remain active while requested custom appearance generation runs asynchronously. A completed and approved requested identity/appearance becomes active automatically. A successor character may explicitly reuse an approved visual identity from a deceased prior character; death does not invalidate or delete reusable artwork.
