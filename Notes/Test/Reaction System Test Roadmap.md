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

### Element Lifetime
- ✅ Element expires on its own after 8 seconds
- ✅ Re-applying an element before expiry resets the 8-second timer

### Arc Chain (Physics)
- ✅ Arc reaction chains to a nearby wet enemy within arcAoeRadius
- ⬜ Arc does NOT chain to enemies outside arcAoeRadius
- ⬜ Arc does NOT chain to objects not tagged "Enemy"
- ⬜ Arc does NOT chain to enemies that are NOT wet (different element)
- ⬜ Arc chains to multiple wet enemies at once

### Frozen Reaction (Timing)
- ⬜ Frozen: enemy movement is suspended immediately after reaction
- ⬜ Frozen: movement resumes after frozenDuration (2 seconds)
- ⬜ Frozen: a second reaction can fire normally after the freeze ends

### Knockback (Physics)
- ⬜ ThermalShock applies knockback away from source
- ⬜ Plasma applies knockback away from source

---

## Not Automated
*Requires eyes or network — validate manually in the TestReactions scene.*

- 🚫 Reaction VFX spawns at the correct position
- 🚫 VFX auto-destroys after its duration
- 🚫 Network: VFX syncs to all clients
- 🚫 Freeze animation: animator speed drops to 0 and recovers
- 🚫 Damage number color changes for crit reactions
- 🚫 Arc chain visual (lightning bolt between enemies)
