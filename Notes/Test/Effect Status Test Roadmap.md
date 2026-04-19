# Effect Status — Test Roadmap

Legend: ✅ done | ⬜ to do | 🚫 not automated (NetworkBehaviour / manual)

---

## Overview

Two layers of effects exist beyond the reaction damage system:

1. **Element status effects** — ongoing effects while an element sits on an enemy (`ElementStatusEffects.cs`)
2. **Reaction side-effects** — one-shot effects triggered by a reaction (`ReactionDamageHandler.cs`)

Reaction damage and element lifetime are already covered in `Reaction System Test Roadmap.md`.
This roadmap covers what happens *around* the damage: freezes, slows, stuns, and attack blocking.

---

## Batch 1 — Frozen: Attack Blocking (PlayMode)

*`AttackController` is a `MonoBehaviour` — testable without networking.*
*File: `ReactionPlayModeTests.cs` (extend existing) or new `EffectPlayModeTests.cs`*

- ⬜ `AttackController.IsAttackBlocked` is `true` immediately when Frozen reaction fires
- ⬜ `AttackController.IsAttackBlocked` returns to `false` after `frozenDuration` (2 seconds)

**Setup:** enemy with `Health + ElementStatusController + ReactionDamageHandler + AttackController + Animator`

---

## Batch 2 — Element Status Effects: Ice Slow

*`ElementStatusEffects` has a single `if (!IsServer) return;` guard (line 39).*
*`EnemyMovementBase` (target of `SetMoveSpeed`) is a `NetworkBehaviour`.*

- 🚫 Move speed reduced to 50% while Ice element is active (`ElementStatusEffects / NetworkBehaviour`)
- 🚫 Move speed restores to original on element clear (`EnemyMovementBase / NetworkBehaviour`)

---

## Batch 3 — Element Status Effects: Fire DoT

*`Health.TakeDamage` is a `MonoBehaviour` method, but the caller (`ElementStatusEffects`) is server-guarded.*

- 🚫 HP decreases by 10% of source base damage per second while Fire element is active (`ElementStatusEffects / IsServer guard`)
- 🚫 DoT stops when element is cleared or expires (`ElementStatusEffects / IsServer guard`)

---

## Batch 4 — Element Status Effects: Lightning Stun

*`EnemyMovementBase.SuspendMovement()` is a `NetworkBehaviour` method.*

- 🚫 Movement suspended for 0.5s every 2s while Lightning element is active (`EnemyMovementBase / NetworkBehaviour`)
- 🚫 Stun does not block knockback from applying (`EnemyMovementBase / NetworkBehaviour`)

---

## Not Automated — Validate Manually

*Requires network session, physics, or eyes.*

- 🚫 Ice Slow — enemy visibly slows when Ice applied (needs running network session)
- 🚫 Fire DoT — enemy HP ticks down on server while Fire is active
- 🚫 Lightning Stun — enemy pauses movement at 2-second intervals
- 🚫 ThermalShock / Plasma knockback — `KnockbackHandler` is `NetworkBehaviour` (noted in Reaction roadmap)
- 🚫 VFX for each element visible on host and client
