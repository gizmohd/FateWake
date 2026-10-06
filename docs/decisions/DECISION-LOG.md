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
