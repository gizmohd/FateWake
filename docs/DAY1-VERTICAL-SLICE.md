# Day 1 Vertical Slice — 6:17–6:43

Runtime scene: `day1-0617-0643`.

Beat order:
1. phone-0617
2. house-flicker
3. street-reveal
4. first-choice — authoritative GameEngine interaction
5. choice-wake
6. radio-fragment
7. timestamp
8. shutdown-0643
9. silence

The scene runtime owns presentation progression only. The GameEngine owns the outcome of the first choice.

Every allowlisted scene layer must have SQL prompt coverage or a valid generate-on-miss path. `art/prompt-seeds/day1-v1.json` imports the existing authored Day 1 prompts once; operational prompt revisions live in PostgreSQL. Reusable recurring environments/characters must additionally follow their continuity/reference packs.
