# Fatewake Mesh Communications — Future System

## Premise
After The Silence, conventional communications are unavailable or unreliable. Survivors may eventually discover, acquire, repair, or build low-bandwidth peer-to-peer radio mesh devices inspired by real-world systems such as Meshtastic/MeshCore.

This is a fictional in-world system, not a simulation of a specific commercial/open-source protocol.

## Product principle
Social communication should become available through world progression rather than appearing as an unexplained global chat feature.

**No device → isolated survivor**
**Device → nearby text mesh**
**Better antenna/power/location → improved reach**
**Relays/repeaters → wider topology**
**Known handles → direct messaging**
**Groups/settlements → shared channels**
**Regional infrastructure → regional communications network**

## Communication modes
### Broadcast
Text message visible to reachable participants on an appropriate local/public channel.

### Direct message
Text addressed to a known in-world handle. Knowing a player's real account identity is not required and must not automatically reveal their handle.

### Group/channel
Persistent or temporary channels may emerge around parties, settlements, organizations, expeditions, emergency coordination, trade, etc.

## Reach is world state
A message is not globally delivered merely because two users are online.

Reach may depend on:
- sender/receiver device capability
- functioning relay topology
- survivor/timeline intersection and convergence state
- broad fictionalized region
- terrain/environment abstractions
- device power/state
- settlement infrastructure
- story/world disruptions
- discovered/known network routes

Do not use or expose precise real-world player location to simulate radio range. Geography must use Fatewake's privacy-safe fictionalized/broad-region model.

## Progression
Early devices should feel scarce and meaningful. Players might:
- find a working unit;
- repair a damaged unit;
- assemble one from compatible components;
- improve power/antenna capability;
- establish a fixed relay;
- discover another relay;
- exchange handles;
- join a settlement channel;
- help build a regional network.

Improved communications create gameplay capability and Wakes. A relay may make a settlement safer while also exposing its existence.

## Text-first constraint
The core mesh experience is text-only. Low bandwidth is part of the fiction and interface language. Do not casually add voice/video/media transport.

UI may show delivery states such as queued, relayed, delivered/acknowledged where supported by game rules, but should not imply certainty the simulated network cannot provide.

## NPC + player traffic
The same interface can carry authored NPC traffic, deterministic world broadcasts, delayed messages, and real-player messages. Presentation must clearly preserve provenance internally even when the player cannot know the sender's true nature.

AI may help render NPC text according to known facts/personality but may not impersonate a real player or alter a real player's message.

## Safety / identity
- Fatewake handles are distinct from account identity and real names.
- Block/mute/report controls remain available regardless of fiction.
- Safety systems are not constrained by fictional radio reach.
- Minors/adults follow Fatewake social-safety rules.
- Never expose precise player location, IP-derived location, email, provider identity or other private account data through mesh mechanics.

## Persistence and history
Messages can become historical artifacts. Canonical metadata should distinguish:
- sender actor
- sender handle at send time
- channel/destination
- sent time / survivor day
- delivery/relay events
- timeline/realm scope
- provenance (player/NPC/system)
- moderation state

Changing a handle later must not rewrite historical attribution.

## Architecture direction
Treat mesh communication as a world/game service, not SignalR global chat.

Potential conceptual entities:
- CommDevice
- CommCapability
- MeshNode
- RelayNode
- MeshRoute/ReachabilityProjection
- CommHandle
- Channel
- ChannelMembership
- Message
- MessageDelivery
- MessageRelayEvent

Real-time transport may use ordinary server infrastructure; the server enforces the fictional mesh topology and decides which actors are reachable.

## Important consequence
Communication itself can create Wakes. Revealing a location, warning another group, sharing a resource lead, broadcasting distress, establishing a relay, or remaining silent can alter later world state.
