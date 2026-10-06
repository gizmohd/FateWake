# Wake System

## Purpose
The **Wake System** is Fatewake's signature mechanic and the mechanical expression of **Every choice leaves a wake.** Meaningful actions may create persistent Wake Effects that influence relationships, event eligibility, world reactions and future resolutions.

## Conceptual Model

    WakeEffect
      Id
      Origin
      Actor
      Target
      Action
      Intent
      Visibility
      Strength
      Severity
      EmotionalImpact
      WorldImpact
      Tags[]
      CreatedDay
      DecayRate
      ResolutionConditions[]
      Status

## Example

    Origin: DAY_003_MAYA
    Type: Relationship
    Target: Maya
    Strength: 0.82
    Tags: loyalty, sacrifice, family
    Created: Day 3
    Resolved: false

## Design Rules
1. Not every Wake is visible to the player.
2. Wakes can remain dormant for long periods.
3. A Wake is not inherently a reward or punishment.
4. Multiple Wakes can combine to unlock or alter an event.
5. Characters know about actions only when visibility permits it.
6. Resolved Wakes remain part of history.
7. AI can narrate Wake outcomes but cannot invent authoritative Wake state.

## Player Memory
Preserve a compressed history of major events, relationships, promises, betrayals, deaths, discoveries, faction history, behavioral patterns, secrets and unresolved Wakes. Only relevant context should be supplied to AI narration.
