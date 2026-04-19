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
*File: `ReactionPlayModeTests.cs`*

- ✅ `AttackController.IsAttackBlocked` is `true` immediately when Frozen reaction fires
- ✅ `AttackController.IsAttackBlocked` returns to `false` after `frozenDuration` (2 seconds)

---

## Batch 2 — Element Status Effects: Ice Slow

*`ElementStatusEffects.OnNetworkSpawn()` never fires without a running NetworkManager — no components are cached and `OnElementChanged` is never subscribed. `EnemyMovementBase` (target of `SetMoveSpeed`) is also a `NetworkBehaviour`, so instantiating it in a test is blocked too (double barrier).*

- 🚫 Move speed reduced to 50% while Ice element is active (`ElementStatusEffects.OnNetworkSpawn` never called / `EnemyMovementBase` is `NetworkBehaviour`)
- 🚫 Move speed restores to original on element clear (same barrier)

---

## Batch 3 — Element Status Effects: Fire DoT

*`ElementStatusEffects.OnNetworkSpawn()` never fires without a running NetworkManager, so `OnElementChanged` is never subscribed and `FireDoTCoroutine` can never start. `Health.TakeDamage` is a testable `MonoBehaviour`, but the caller is completely blocked.*

- 🚫 HP decreases by 10% of source base damage per second while Fire element is active (`ElementStatusEffects.OnNetworkSpawn` never called)
- 🚫 DoT stops when element is cleared or expires (same barrier)

---

## Batch 4 — Element Status Effects: Lightning Stun

*`ElementStatusEffects.OnNetworkSpawn()` never fires without a running NetworkManager. `EnemyMovementBase.SuspendMovement()` is also a `NetworkBehaviour` method (double barrier).*

- 🚫 Movement suspended for 0.5s every 2s while Lightning element is active (`ElementStatusEffects.OnNetworkSpawn` never called / `EnemyMovementBase` is `NetworkBehaviour`)
- 🚫 Stun does not block knockback from applying (same barrier)

---

## Not Automated — Validate Manually

*Requires network session, physics, or eyes.*

- 🚫 Ice Slow — enemy visibly slows when Ice applied (needs running network session)
- 🚫 Fire DoT — enemy HP ticks down on server while Fire is active
- 🚫 Lightning Stun — enemy pauses movement at 2-second intervals
- 🚫 ThermalShock / Plasma knockback — `KnockbackHandler` is `NetworkBehaviour` (noted in Reaction roadmap)
- 🚫 VFX for each element visible on host and client
