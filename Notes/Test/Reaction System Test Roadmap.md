# Reaction System — Test Roadmap

Legend: ✅ done | ⬜ to do | 🚫 not automated (manual/VFX/network)

---

## EditMode Tests
*Pure logic — no game loop, runs instantly.*
*Files: `ReactionResolverTests.cs`, `ElementStatusControllerTests.cs`, `ElementDamagePipelineTests.cs`, `ReactionResultTests.cs`*

### ReactionResolver (pure logic)
- ✅ All 6 reactions resolve to the correct ReactionType (both element orderings)
- ✅ All 6 reactions produce ClearAll outcome
- ✅ None + any element → no reaction
- ✅ Same element + same element → no reaction
- ✅ Non-reacting pairs (Wind, Earth) → no reaction
- ✅ Different strengths still react
- ✅ Resolver does not set BaseDamage or Source (those come from Health)

### ElementStatusController
- ✅ Single element stored correctly (all 6 types, correct strength)
- ✅ Stores the Source (who applied it)
- ✅ Same element re-applied → replaces without reaction
- ✅ Non-reacting pair → replaces element
- ✅ Reacting pair → triggers correct reaction (all 6, both orderings)
- ✅ After reaction → element state is cleared (ClearAll)
- ✅ OnReactionTriggered event fires with correct ReactionType, BaseDamage, Source, IsCritical
- ✅ No reaction → OnReactionTriggered does NOT fire
- ✅ OnElementChanged fires on every application
- ✅ OnElementChanged fires with post-reaction state (None after ClearAll)
- ✅ ClearElement() resets to None + 0 strength
- ✅ ClearElement() fires OnElementChanged
- ✅ WouldReact() returns true for a valid pair
- ✅ WouldReact() returns false for non-reacting pair
- ✅ WouldReact() returns false when current element is None
- ✅ WouldReact() returns false when incoming is None
- ✅ WouldReact() does NOT mutate state
- ✅ Full cycle: apply → react → state cleared → apply new → react again

### Damage Pipeline (Health → ElementStatusController → ReactionDamageHandler)
- ✅ Normal hit: damage applied + element stored
- ✅ Normal hit with no element: only damage applied
- ✅ Reaction hit: direct damage suppressed, reaction damage = baseDamage × multiplier
- ✅ All 6 reactions apply correct reaction damage through the full pipeline
- ✅ Zero-damage reaction: reaction skipped (multiplier of 0 = nothing)
- ✅ Reaction damage can kill
- ✅ OnDeath fires when killed by reaction
- ✅ After reaction: element state is cleared
- ✅ Multi-reaction cycle (multiple reaction rounds in sequence)
- ✅ OnAnyReactionDamage static event fires with correct damage and source
- ✅ Normal hit fires OnDamageTaken with crit flag
- ✅ Reaction hit: suppressed direct damage does NOT fire OnDamageTaken twice

---

## PlayMode Tests
*Time and physics — needs the real game loop.*
*File: `ReactionPlayModeTests.cs`*

### Element Lifetime (shared by all reactions)
- ✅ Element expires on its own after 8 seconds
- ✅ Re-applying an element before expiry resets the 8-second timer

### Boiling — Water + Fire (2× damage)
*No special behavior beyond damage. Fully covered by EditMode pipeline tests.*
- ✅ Damage = baseDamage × 2 (EditMode)
- ✅ Both elements consumed after reaction (EditMode)

### Frozen — Water + Ice (1.4× damage + timed freeze)
*EnemyMovementBase is a NetworkBehaviour — tested via Animator.speed as proxy instead.*
- ✅ Damage = baseDamage × 1.4 (EditMode)
- ✅ Both elements consumed after reaction (EditMode)
- ✅ Animator.speed drops to 0 immediately when freeze fires
- ✅ Animator.speed restores to 1 after frozenDuration (2 seconds)
- 🚫 Movement and attack suspended (requires EnemyMovementBase / NetworkBehaviour)

### ThermalShock — Ice + Fire (1.6× damage + knockback)
*KnockbackHandler is a NetworkBehaviour. ApplyKnockback() has an IsServer guard — returns immediately without a running NetworkManager.*
- ✅ Damage = baseDamage × 1.6 (EditMode)
- ✅ Both elements consumed after reaction (EditMode)
- 🚫 Enemy is knocked back away from source (KnockbackHandler / NetworkBehaviour)
- 🚫 Knockback direction is random when source is unknown (KnockbackHandler / NetworkBehaviour)

### Arc — Water + Lightning (1.5× damage + chain to nearby wet enemies)
- ✅ Damage = baseDamage × 1.5 (EditMode)
- ✅ Both elements consumed after reaction (EditMode)
- ✅ Arc chains to a nearby wet enemy within arcAoeRadius
- ✅ Arc does NOT chain to enemies outside arcAoeRadius
- ✅ Arc does NOT chain to objects not tagged "Enemy"
- ✅ Arc does NOT chain to enemies that have a different element (not Water)
- ✅ Arc chains to multiple wet enemies at once

### Crack — Ice + Lightning (1.5× damage)
*No special behavior beyond damage. Fully covered by EditMode pipeline tests.*
- ✅ Damage = baseDamage × 1.5 (EditMode)
- ✅ Both elements consumed after reaction (EditMode)

### Plasma — Fire + Lightning (1.75× damage + knockback)
*Same limitation as ThermalShock — KnockbackHandler is a NetworkBehaviour.*
- ✅ Damage = baseDamage × 1.75 (EditMode)
- ✅ Both elements consumed after reaction (EditMode)
- 🚫 Enemy is knocked back away from source (KnockbackHandler / NetworkBehaviour)
- 🚫 Knockback direction is random when source is unknown (KnockbackHandler / NetworkBehaviour)

---

## Not Automated
*Requires eyes or network — validate manually in the TestReactions scene.*

- 🚫 Reaction VFX spawns at the correct position
- 🚫 VFX auto-destroys after its duration
- 🚫 Network: VFX syncs to all clients
- 🚫 Damage number color changes for crit reactions
- 🚫 Arc chain visual (lightning bolt between enemies)
