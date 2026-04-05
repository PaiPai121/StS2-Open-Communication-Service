# SOCS 1.0 MVP Snapshot Acceptance Contract

This document defines the official acceptance standard for the SOCS 1.0 MVP (minimum playable agent) information snapshot.

It is the working contract for upcoming SOCS development.

## Priority Levels

- **P0 / MUST HAVE**: required for a combat decision loop. Missing data here means the agent cannot reliably play cards.
- **P1 / SHOULD HAVE**: required for a run progression loop. Missing data here means the agent cannot reliably advance floors.
- **P2 / COULD HAVE**: strategic enrichment. Missing data here means the agent can play, but cannot optimize like a strong human player.

---

## P0 / MUST HAVE

### 1. Enemy detailed state
Required per enemy:
- `intentType`
- `intentDamage`
- `intentMulti`
- `block`

This is required so the agent can reason about incoming damage, multi-hit pressure, and defensive thresholds.

### 2. Buff / Debuff system
Required for both player and enemies:
- `powers[]`
  - `id`
  - `amount`

This is required so the agent can reason about temporary and persistent combat modifiers.

### 3. Card deep semantics
Required per playable card:
- `upgraded`
- `type`
- `baseDamage`
- `baseBlock`
- `cost`
- `description`

This is required so the agent can estimate tactical value instead of playing from name-only information.

### 4. Potion state
Required:
- potion slot capacity
- current potions
- whether each potion is usable in combat

This is required so the agent can include potion actions in combat planning.

### 5. Combat phase and targeting state
Required:
- explicit `pendingState`
  - examples: single-target selection, discard selection
- whether end turn is currently legal

This is required so the agent knows whether it should play, target, choose, discard, or end turn.

---

## P1 / SHOULD HAVE

### 1. Master Deck
Required:
- full current deck state

This is required for shop decisions, reward evaluation, and long-horizon pathing.

### 2. Rewards
Required:
- reward kinds
  - gold / card / relic / potion
- legal actions
  - claim / skip / replace

This is required so the agent can resolve reward screens correctly.

### 3. Map and nodes
Required:
- current node type
  - event / elite / boss / shop
- legal next nodes

This is required so the agent can progress through the map instead of only handling the current room.

### 4. Rest site
Required:
- legal campfire actions
  - rest / smith / and other available operations

This is required so the agent can resolve campfire screens.

---

## P2 / COULD HAVE

### 1. Relic dynamic semantics
Required enrichment:
- `counter`
- whether the relic has already triggered or become inactive

This improves long-horizon strategy quality.

### 2. Shop services
Required enrichment:
- remove-card service
- current removal price

This improves economic decisions in shops.

### 3. Event option semantics
Required enrichment:
- mapping option text to expected resource gain / loss outcomes

This improves event planning and risk evaluation.

---

## Current Sprint Scope

Even though this contract is the long-term target, the current sprint is intentionally narrower.

### Locked sprint scope
Current sprint is strictly limited to the first three combat-critical items from **P0 / MUST HAVE**:
- enemy detailed state
- buff / debuff powers
- card deep semantics

### Current implementation target
The current coding focus is:
1. upgrade combat DTOs in `SocsModels.cs`
2. implement best-effort reflection probes in `SocsRuntime.cs`
3. preserve snapshot stability

### Hard constraint
All reflection-based extraction must be best-effort:
- if a field cannot be found, return `null` or an empty collection
- do not throw uncaught exceptions
- do not block full snapshot generation because one property could not be probed

---

## Acceptance Rule

SOCS 1.0 MVP is considered acceptable only when:
1. P0 data is available with stable structure
2. missing reflective fields degrade to nullable / empty values safely
3. snapshot generation remains resilient even when some runtime members are absent or renamed

Until then, SOCS is still a partial decision surface rather than a minimum playable agent surface.
