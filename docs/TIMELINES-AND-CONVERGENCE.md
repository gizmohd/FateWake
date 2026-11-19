# Timelines and Convergence

## Player-facing terminology and personal Threads

Pathweaver calls alternate timelines **Threads**, their players **Pathweavers**, daily episodes **Chapters**, major arcs **Acts**, and timeline convergence events **Convergences**. Existing database/API `survivor` and `timeline` identifiers remain stable compatibility names.

Signed-in Pathweavers may weave a new isolated personal Thread from a reached chapter-opening or pre-decision checkpoint, preserving the original Thread, then explicitly resume either Thread. This is not a destructive reset and has no fixed gameplay branch cap; creation is rate-limited and discovery paginated.

Checkpoints store versioned canonical facts/episode/Wake state and freeze reached journal disclosure and artwork pins. Later parent revelations and outcomes do not become knowledge on the child. Restoration does not replay old decisions or emit their effects into the world again. Account/profile identity remains shared and is never rewound.

Existing saves without exact historical snapshots may start a separately labelled fresh Chapter 1 Thread at the canonical opening. Do not invent older restore points from reconstructed journal prose. Unsupported rules/content versions and shared-world/cross-timeline dependencies are explicit conflicts. Shared or converged history cannot be rewound by this personal feature.

Thread selection is durable across reload/sign-in. All progression, action, history, notice and artwork-pinning operations validate their branch context; stale tabs cannot write through a newly selected Thread. The Development reset remains a separate destructive debugging operation and cannot invalidate preserved descendants.

## Core Principle

Pathweaver does not place every new player directly into the latest shared-world day.

Every player begins by experiencing **The Silence** personally. Their early story exists in a personal timeline that can later intersect with other player timelines and eventually converge into larger shared history.

**Shared Origin → Personal Timeline → Timeline Intersection → Regional Convergence → Shared World**

This allows every player to experience the foundational story while preserving a persistent multiplayer world.

## Personal Timelines

A new Pathweaver begins with Chapter 1 on story Day 1 regardless of when they join.

Early play is primarily personal and NPC-driven. This establishes the player's relationships, behavioral identity, resources, secrets, discoveries and Wakes before multiplayer relationships become important.

Personal history is never discarded when timelines converge.

## Timeline Intersection

Player timelines may intersect when the fiction makes contact plausible.

A first encounter with another real player should be a significant narrative moment rather than a routine lobby interaction.

An intersection does not merge all state. It creates shared facts and Wakes between the participating actors while preserving their personal histories.

## Convergence

As communication, travel, settlements and factions expand, formerly isolated timelines can share progressively larger scopes of world state.

Convergence should occur through fiction: radio contact, travel, trade routes, settlements, relays, factions, the 6:17 phenomenon, or other story events. Players should not experience convergence as a technical server-transfer mechanic.

Multiple unconverged timelines may coexist simultaneously.

## World Scope

Authoritative facts and Wakes have scope.

- **Personal** — known/relevant to one player.
- **Party** — shared by a temporary or persistent small group.
- **Settlement** — affects a community.
- **Local** — affects nearby communities/zones.
- **Regional** — becomes part of a region's history.
- **Global** — canonical shared-world state.

Convergence propagates only state appropriate to its scope.

Example: Maya trusting one player remains Personal. A settlement attacking another can become Regional history. Humanity collectively answering a signal can become Global history.

## Conflicting Personal Decisions

Convergence does not erase individual choices.

If one player answered the Day 7 signal and another remained silent, both personal histories remain true. A later aggregate result may establish that humanity collectively answered, but the player who opposed answering retains that history and any Wakes created by it.

## Player-to-Player Wakes

Wakes may connect:

    Player → Player
    Player → Settlement
    Settlement → Settlement
    Player → Region
    Region → Global World

These Wakes allow real player behavior to create future story.

A rescue, betrayal, trade, refusal, alliance or attack can become historical state that resurfaces much later.

## Activity and Time

Early personal Threads need not advance simply because real-world time passes. Pathweaver is activity-driven: if nobody is playing within a Thread, meaningful game time can pause.

Exact advancement and synchronization rules require a dedicated clock specification.

## Design Constraint

Timeline architecture must support deterministic resolution and remain understandable enough to test. Narrative complexity must not require AI to decide canonical truth.

## Lore Opportunity — Not Canon

It may eventually be useful for Pathweaver's fiction to acknowledge apparently conflicting memories or histories between Threads. This could connect the technical timeline model to the deeper mystery of The Silence.

This is deliberately **not canonical** yet and should remain an available story direction rather than an assumed explanation.
