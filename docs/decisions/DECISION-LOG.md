# Fatewake Decision Log

This file distinguishes accepted direction from exploratory ideas.

## Accepted Direction

| ID | Decision | Status |
|---|---|---|
| DEC-001 | Product name is **Fatewake**. | Accepted |
| DEC-002 | Primary tagline is **Every choice leaves a wake.** | Accepted |
| DEC-003 | Persistent narrative survival/social strategy. | Accepted |
| DEC-004 | Initial delivery target is mobile-first web/PWA; native remains possible. | Accepted direction |
| DEC-005 | Required daily play target is approximately 3–5 minutes. | Accepted direction |
| DEC-006 | Use one daily episode rather than exactly one decision per day. | Accepted direction |
| DEC-007 | Wake Effects are the signature persistent consequence mechanic. | Accepted direction |
| DEC-008 | Game engine owns outcomes; AI owns narration/dialogue/intent assistance. | Accepted direction |
| DEC-009 | Player history persists across seasons rather than routine resets. | Accepted direction |
| DEC-010 | Failure should branch the story rather than routinely produce Game Over. | Accepted direction |
| DEC-011 | Season One is **The Silence**. | Accepted direction |
| DEC-012 | First playable milestone is Days 1–7 and must create desire to return for Day 8. | Accepted direction |

## Proposed / Not Yet Locked

| ID | Proposal | Status |
|---|---|---|
| PROP-001 | Player-character death can lead to succession. | Proposed |
| PROP-002 | Primary resources: Food, Water, Medicine, Energy, Security. | Proposed |
| PROP-003 | Community metrics: Population, Morale, Trust, Influence. | Proposed |
| PROP-004 | Future seasons may include The Fracture, First Winter and The Signal. | Proposed |
| PROP-005 | Fatewake+ premium offering around $4.99/month. | Concept only |
| PROP-006 | Faction concepts: Coalition, Freeholds, Ascendants, Wildlands. | Concept only |

## Process
When a major choice is made: update this log, update the canonical document, and preserve unresolved alternatives as proposals rather than silently treating them as canon.

## Timeline Architecture Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-013 | Every new player experiences The Silence from Survivor Day 1 rather than being dropped directly into the latest shared-world day. | Accepted |
| DEC-014 | Early player history exists in a personal timeline that can intersect and later converge with other timelines. | Accepted |
| DEC-015 | Timeline convergence preserves personal history; it does not flatten players into identical state. | Accepted |
| DEC-016 | Wakes and authoritative facts use Personal, Party, Settlement, Local, Regional, or Global scope. | Accepted |
| DEC-017 | The first encounter with another real player should be treated as a meaningful narrative event. | Accepted |
| DEC-018 | Convergence occurs through in-world events such as communication, travel, settlements or other story mechanisms. | Accepted |
| DEC-019 | Early timelines are activity-driven and may pause when nobody is playing. Exact clock rules remain to be specified. | Accepted direction |
| PROP-007 | Conflicting timeline memories may eventually become part of Fatewake lore rather than only a technical implementation detail. | Concept only |

## Player Experience Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-020 | Player identity is hybrid: players may play a version of themselves or create a fictional identity. | Accepted |
| DEC-021 | Geography is fictionalized real-world geography: real cities/regions may exist while specific locations, organizations and details can be fictionalized. | Accepted |
| DEC-022 | Content intensity is age-aware: generally mature, with occasional darker/brutal consequences where appropriate for the player's age/content setting. | Accepted direction |
| DEC-023 | Real-player identity is not necessarily revealed at first contact; meaningful interaction can later reveal that a survivor represents another human player. | Accepted |
| DEC-024 | Player agency uses controlled freedom: authored boundaries remain, while plausible unscripted actions can be interpreted and resolved by deterministic systems. | Accepted |
| DEC-025 | Meaningful interactions should support both presented choices and free-form input as interfaces into the same action-resolution system. | Accepted |

## Mortality, Conflict and Relationship Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-026 | Player-character death may be permanent, but should be rare, consequential and normally emerge from a serious chain of circumstances rather than a cheap single choice. The account/world continues through succession. | Accepted direction |
| DEC-027 | Real players may attack, injure, capture or rob one another, but direct PvP should not by itself permanently kill another player's established character. | Accepted direction |
| DEC-028 | Players may deliberately betray, deceive and break promises to other players; these actions create persistent Wakes and reputation/history consequences. | Accepted |
| DEC-029 | Relationships may develop deeply, including friendship, rivalry, enmity, mentorship, family-like bonds, romance and marriage where appropriate. | Accepted direction |
| DEC-030 | Families and children may exist and be affected by survival pressures, with age/content-aware handling and without requiring graphic depiction. | Accepted direction |
| DEC-031 | Player-to-player communication uses a hybrid model: contextual/suggested dialogue and actions plus moderated free-form communication when available. | Accepted direction |

## Simulation and Pacing Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-032 | The world may continue while a player is absent, but the player's character does not autonomously make major irreversible personal decisions. | Accepted direction |
| DEC-033 | Early personal timeline progression is episode/activity-driven rather than strictly tied to one real-world day. As convergence approaches, pacing progressively synchronizes with shared-world time. | Accepted direction |
| DEC-034 | Early onboarding may be binged with intentional pacing friction; the first few days can progress quickly before stronger real-time/daily cadence emerges. | Accepted direction |
| DEC-035 | Survival simulation should be credible rather than tedious: meaningful scarcity and logistics without unnecessary micromanagement. | Accepted |
| DEC-036 | Inventory combines abstract resource pools with significant named physical items that can carry state, history and Wakes. | Accepted |
| DEC-037 | Geography follows realistic regional climate and seasons, but weather is simulation/story-driven rather than controlled by live real-world weather. | Accepted |
| DEC-038 | Players can substantially influence settlement leadership, rules, defense, priorities, alliances, membership and projects, while NPCs retain independent agency. | Accepted direction |

## Mystery and Shared-Universe Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-039 | Fatewake's designers maintain a canonical underlying truth for The Silence even while players receive incomplete and conflicting evidence. | Accepted |
| DEC-040 | The central mystery uses a science-fiction explanation that can initially appear supernatural, involving causality, consciousness, reality/timeline effects or related phenomena rather than a simple apocalypse explanation. | Accepted direction |
| DEC-041 | **Fatewake** can eventually become an in-universe term/concept, with its meaning revealed after players already know it as the title. | Accepted direction |
| DEC-042 | Multiple timelines are ontologically real within Fatewake's fiction, not merely matchmaking infrastructure. | Accepted direction |
| DEC-043 | Some missing people may exist on other branches/timelines, allowing emotionally conflicting disappearances where each side experienced the other as missing. | Accepted direction |
| DEC-044 | Familiar anomalous voices may genuinely originate from known people across another branch/reality; responding can have variable consequences. | Accepted direction |
| DEC-045 | Fatewake should not reduce its ultimate conflict to a single conventional villain; human antagonists may exist while the deeper conflict concerns humanity's interaction with a poorly understood phenomenon. | Accepted |
| DEC-046 | Fatewake may share deep cosmology with **The Wayfinder Legacy**, particularly the Echo and its ancient network/paths, while remaining independently understandable and not requiring knowledge of the novels. | Accepted direction |

## Wayfinder Realm Integration Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-047 | Fatewake explicitly shares the larger universe/cosmology of **The Wayfinder Legacy** rather than remaining only an Easter-egg connection. | Accepted direction |
| DEC-048 | Fatewake may be one of the connected Realms referenced by Wayfinder canon, allowing it to have its own history, terminology, cultures and expression of Echo-related phenomena. | Accepted direction |
| DEC-049 | Fatewake can contain Wayfinders or analogous Gift-bearing people, but should primarily introduce its own characters rather than depend on Ian, James or other novel protagonists. | Accepted direction |
| DEC-050 | Wayfinder cosmology can guide Fatewake's foundational character/archetype design, while Fatewake-specific manifestations remain possible. | Accepted direction |
| PROP-008 | Significant emergent Fatewake events may be promoted into canonical Wayfinder-universe history after editorial review. | Proposed |

## Gifts and Emergent Archetype Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-051 | Archetypes/Gifts emerge from observed player behavior and history rather than being selected as a starting class. | Accepted |
| DEC-052 | Traveler, Listener and Builder remain foundational Wayfinder Gifts, while the Fatewake Realm may recognize different expressions, combinations and terminology for them. | Accepted direction |
| DEC-053 | An ordinary player may rarely develop into the Fatewake equivalent of a Wayfinder; this must be earned through history and cannot be selected during character creation. | Accepted direction |
| DEC-054 | Most players remain ordinary humans; non-Gift paths such as leadership, medicine, engineering, trade, scouting, combat and diplomacy remain equally meaningful. | Accepted |
| DEC-055 | Early Gift development is communicated diegetically through perception, events and narrative changes rather than visible ability points or explicit labels. | Accepted |

## Information, Journal and Player-Observation Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-056 | Expose countable physical resources numerically where appropriate, but communicate relationships, morale and similar human state primarily through qualitative/diegetic signals rather than meters. | Accepted |
| DEC-057 | The Journal/History exposes known significant events while hidden Wakes and future consequence mechanics remain undisclosed until they surface naturally. | Accepted |
| DEC-058 | Players retain a long-term browsable personal history across their Fatewake life and seasons. | Accepted |
| DEC-059 | Authoritative UI/state should not deliberately lie; characters, incomplete information, perceptions and anomalous experiences may be unreliable. | Accepted |
| DEC-060 | Notifications should be restrained and primarily diegetic/in-world rather than engagement-spam mechanics. | Accepted |
| DEC-061 | The journal is a player-owned artifact supporting personal free-form notes alongside automatically recorded history. | Accepted |
| DEC-062 | Player-authored journal entries may be analyzed for observations, theories, entities, promises, concerns and other potentially meaningful context that authored content or telemetry did not anticipate. | Accepted direction |
| DEC-063 | Journal analysis is interpretive, not authoritative: the original note is preserved, extracted interpretations retain provenance/confidence, and no inferred claim becomes canonical world state without deterministic validation or later evidence. | Accepted |

## Information Propagation and Secrets Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-064 | NPCs may remember relevant events they plausibly witnessed even when the player is not explicitly told that the event was observed. | Accepted |
| DEC-065 | Information itself can create Wakes; secrets, warnings, instructions, coordinates, rumors and misinformation can propagate and cause persistent consequences. | Accepted |
| DEC-066 | Meaningful information propagation should retain provenance so the simulation can trace who learned, transmitted or altered information and through what path. | Accepted |
| DEC-067 | Players may deliberately lie or mislead others; recipients do not receive magical truth labels. | Accepted |
| DEC-068 | NPCs may lie or mislead when consistent with their knowledge, motives, personality and circumstances; they cannot fabricate knowledge they could not plausibly possess without an explanation. | Accepted |
| DEC-069 | Fatewake should contain genuinely discoverable secrets and patterns that may be found by only a small fraction of players and can become community-scale investigations. | Accepted |

## Social Scale and Settlement Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-070 | Settlements may contain NPCs and many real players interacting asynchronously without presenting themselves as conventional MMO guilds. | Accepted direction |
| DEC-071 | Real players may become settlement leaders, but leadership remains contestable through social, political and systemic processes involving players and NPCs. | Accepted |
| DEC-072 | Governance should primarily emerge from repeated behavior and institutional decisions rather than selecting a government type from a menu. | Accepted direction |
| DEC-073 | Settlements, including player-influenced settlements, may enter meaningful conflict or war involving logistics, intelligence, diplomacy, sabotage, territory and persistent consequences rather than simple click-to-raid PvP. | Accepted direction |
| DEC-074 | Settlements can permanently fall; their history, ruins, refugees, artifacts, responsibility and downstream consequences persist in the world. | Accepted |
| DEC-075 | Players may create persistent organizations beyond settlements, including trade, defense, medical, communications, religious, research and intelligence organizations that may outlive their founders. | Accepted direction |
| DEC-076 | Fatewake's social/world scale can propagate from Person → Group → Settlement → Organization → Alliance/Faction → Region → Realm without requiring every player to directly manage every layer. | Accepted direction |

## Identity, Privacy and Social Safety Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-077 | Real-world geography may inform a player's starting region, but another player's precise real-world location is never exposed through gameplay; visible geography is appropriately fictionalized/in-world. | Accepted |
| DEC-078 | Friends and family may intentionally join one another, with the system creating plausible early timeline intersections rather than teleporting characters together without fiction. | Accepted direction |
| DEC-079 | Real names are optional; other players primarily interact with the chosen survivor identity. | Accepted |
| DEC-080 | Blocking prevents new direct player interactions where practical but does not erase already-canonical history or legitimate world consequences. | Accepted |
| DEC-081 | In-world hostility may be valid fiction, but harassment, stalking, hate/slurs, sexual harassment, real-world threats, doxxing and similar platform abuse remain prohibited regardless of role-play framing. | Accepted |
| DEC-082 | Unrestricted stranger DMs are not a default feature; free-form communication is unlocked through in-world contexts such as encounters, radio, settlements and organizations. | Accepted direction |
| DEC-083 | Players may establish private out-of-world trusted connections with friends/family so Fatewake can facilitate plausible convergence without exposing the real-world relationship to others. | Accepted direction |
| DEC-084 | Real-player social and relationship systems use age-segmented protections, with strong restrictions on adult/minor romance and private communication while fictional worlds may still contain characters of varied ages. | Accepted |

## First-Session and Onboarding Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-085 | Fatewake opens directly in the fiction at 6:17 AM rather than beginning with conventional character creation. | Accepted |
| DEC-086 | The first meaningful decision should occur within roughly the first 60–90 seconds of play. | Accepted direction |
| DEC-087 | Starting geography uses only broad/approximate regional location with permission and fictionalizes more precise geography; a home address is not required. | Accepted |
| DEC-088 | Hybrid/self identity is established through a small number of contextual questions and later behavior rather than a large pre-game questionnaire. | Accepted |
| DEC-089 | A free-form 'Do something else...' interaction appears very early so players immediately understand that authored options are not the complete action space. | Accepted |
| DEC-090 | The first major anomaly/reveal is the future-dated 6:17 radio timestamp and is presented without explanatory tutorial UI. | Accepted |
| DEC-091 | Day 1 should visibly but subtly reflect an earlier player action before the episode ends, demonstrating persistent memory without exposing mechanics. | Accepted |
| DEC-092 | Explore guest/low-friction play before full registration, with account creation/preservation requested after initial investment, subject to implementation/security/platform validation. | Accepted direction |
| DEC-093 | Tutorials should be minimal and contextual; new systems are introduced when they become relevant in the fiction. | Accepted |

## Data Platform Decision

| ID | Decision | Status |
|---|---|---|
| DEC-094 | PostgreSQL is Fatewake's authoritative primary datastore. The architecture will use relational modeling for known domain entities, JSONB where flexible/evolving payloads are valuable, append-oriented history for consequential state changes, and graph-friendly relationships/recursive traversal for Wakes, causality, information provenance and world relationships. | Accepted |
| DEC-095 | MongoDB, a dedicated graph database, Redis and other specialized datastores are not MVP dependencies. They may be introduced later only for demonstrated workloads while PostgreSQL remains the source of canonical truth unless a future architecture decision explicitly changes that. | Accepted |

## Application Platform Decision

| ID | Decision | Status |
|---|---|---|
| DEC-096 | Fatewake starts on .NET 10 with .NET Aspire as the local application orchestration and service-development foundation. | Accepted |
| DEC-097 | The MVP is a modular application with explicit project/domain boundaries rather than a microservice architecture. Services may be separated later only where operational or scaling requirements justify it. | Accepted |
| DEC-098 | Initial application stack is Blazor Web App/PWA, ASP.NET Core APIs, EF Core with Npgsql/PostgreSQL, and Aspire orchestration. Redis, messaging, search and Kubernetes are deferred until demonstrated requirements justify them. | Accepted |

## Domain Persistence Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-099 | Fatewake persistence separates canonical append-oriented history, current-state projections and graph-style relationship/causality structures. | Accepted |
| DEC-100 | World truth, actor knowledge and player belief are modeled separately; inferred belief cannot directly mutate canonical truth. | Accepted |
| DEC-101 | Wakes form an explicitly traversable causal graph using Wake relationships/edges while strongly typed domain entities remain relational. | Accepted |
| DEC-102 | AI/static narrative renders are replaceable presentation artifacts generated from authoritative narrative facts and are never the sole canonical record. | Accepted |
| DEC-103 | GameEngine domain types remain persistence-agnostic; EF Core/Npgsql mappings and migrations live in Infrastructure rather than defining the domain model. | Accepted |
| DEC-104 | Consequential player actions resolve transactionally: validate authoritative state, record resolution/events/Wakes, update projections, commit, then perform replaceable narrative rendering. | Accepted |

## AI Content Reuse Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-105 | AI-generated narration, dialogue, decision/action structures and other suitable outputs should be persisted and reused when their applicability is still valid, avoiding unnecessary repeated model calls. | Accepted |
| DEC-106 | Exact AI-content reuse is keyed from a normalized generation context/fingerprint that includes all state material to correctness, including canonical facts, knowledge, versions and relevant personalization. | Accepted |
| DEC-107 | AI-generated decision/action trees intended for repeated gameplay become versioned validated content assets; deterministic runtime logic consumes the stored version instead of regenerating it per player. | Accepted |
| DEC-108 | Semantic similarity may discover reuse candidates, but cannot by itself authorize reuse; current facts, privacy boundaries and content constraints must validate applicability. | Accepted |
| DEC-109 | Generated-content provenance and economics should be measurable, including model/provider, versions, usage/reuse counts and token/cost metadata when available. | Accepted |

## Player Authentication Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-110 | Fatewake uses external identity as the primary account model, initially supporting Google, Microsoft and Apple sign-in/sign-up rather than requiring a Fatewake password. | Accepted |
| DEC-111 | Guest play remains supported for low-friction onboarding; linking an external identity upgrades/preserves the existing survivor and timeline rather than creating a new survivor. | Accepted |
| DEC-112 | A Fatewake account is distinct from a provider identity and may have multiple external login identities linked to the same account. | Accepted |
| DEC-113 | Provider email addresses are attributes, not stable identity keys. External identities are keyed by provider + provider subject identifier; Apple private-relay email is supported. | Accepted |
| DEC-114 | Authentication secrets/client credentials are external configuration/secrets and never stored in source control. | Accepted |


## Visual Progression Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-115 | Survivor appearance is state-driven and may change over time based on canonical wardrobe, equipment, injuries, condition and persistent story marks. | Accepted |
| DEC-116 | Stable visual identity is separated from mutable visual layers so equipment/clothing can change without regenerating the survivor's identity. | Accepted |
| DEC-117 | Historical scenes/journal entries retain visual snapshots or version fingerprints so old artwork does not retroactively adopt current equipment or appearance. | Accepted |
| DEC-118 | Visual equipment is a presentation projection of authoritative inventory/equipment state and can never grant gameplay ownership or capability. | Accepted |
| DEC-119 | Approved survivor/equipment visual assets are versioned and reusable; missing combinations may trigger generation using the Style Bible, identity reference and canonical visual state. | Accepted |


## Mesh Communications Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-120 | Post-Silence player communications are introduced diegetically through discoverable/buildable low-bandwidth mesh communication capability rather than unexplained global chat. | Accepted |
| DEC-121 | Mesh communications are text-first and support reachable broadcast, known-handle direct messaging, and group/channel communication. | Accepted |
| DEC-122 | Communication reach is determined by fictional world topology/capabilities and privacy-safe broad geography; precise real-world player location is never exposed or required. | Accepted |
| DEC-123 | Relay/repeater and device progression can expand communication reach and create gameplay consequences/Wakes. | Accepted |
| DEC-124 | Fatewake communication handles are separate from account identity; historical messages retain sender-handle/provenance-at-send-time. | Accepted |
| DEC-125 | Real-player messages are never rewritten or impersonated by AI; NPC/system/player provenance remains authoritative even when not necessarily visible to the receiving character. | Accepted |


## Source Documentation Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-131 | Hand-authored C# source uses one declared type per file; classes, interfaces, records, structs and enums are split into individually named files. | Accepted |
| DEC-132 | Public/internal Fatewake types and meaningful members are documented with XML documentation covering purpose, usage, contracts, side effects and concurrency/idempotency behavior where relevant. | Accepted |
| DEC-133 | Every hand-authored C# source file has an individual companion Markdown document under `docs/code/` mirroring its project-relative source path and describing usage and architectural behavior. | Accepted |
| DEC-134 | Source and companion documentation are updated together; materially modified legacy code should be migrated toward the documentation/file-structure standard. | Accepted |
| DEC-135 | Each documented C# type includes a type-level XML `<see href="...">` relative link to its companion Markdown file, allowing direct navigation from source/IDE documentation to the deeper reference document. | Accepted |


## Character Appearance Reuse Decisions

| ID | Decision | Status |
|---|---|---|
| DEC-136 | Character customization is normalized durable appearance state separated from base identity, equipment/loadout, condition and scene projection. | Accepted |
| DEC-137 | Every material base-identity + appearance combination receives a deterministic fingerprint; exact approved matches are reused before any AI generation. | Accepted |
| DEC-138 | Previously generated/approved appearance combinations remain reusable when a player changes away from them; reverting to the same combination must not incur another AI generation call. | Accepted |
| DEC-139 | Appearance assets and complete scene renders use separate reuse layers so one approved appearance can be projected into many loadouts, conditions, poses and scenes. | Accepted |
| DEC-140 | New survivors use approved default male/female visual identities until custom identity/appearance generation completes and is approved, at which point the requested visual identity is automatically activated without blocking gameplay. | Accepted |
| DEC-141 | Approved visual identity artwork survives character death and may be explicitly reused for a successor/new character without regeneration. | Accepted |
