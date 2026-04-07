# Enemy Reference

---

## Goblin
**Type:** Melee Rusher
**Movement:** Ground (NavMesh), chases player directly.
**Attack:** Contact damage — deals 10 dmg every 0.5s while touching player.
**Stats:** HP 50 | Speed 3 | Attack range 0.6
**Notes:** Fast and aggressive. Dangerous in packs.

---

## Slime
**Type:** Slow Tank
**Movement:** Ground (NavMesh), slow direct chase.
**Attack:** Contact damage — deals 4 dmg every 0.5s while touching player.
**Stats:** HP 60 | Speed 1.2 | Attack range 1
**Notes:** On death splits into 2 SmallSlimes. Scales with level (+12% HP, +3% speed, +8% dmg per level).

---

## Small Slime
**Type:** Swarm
**Movement:** Ground (NavMesh), slightly faster than parent.
**Attack:** Contact damage — deals 2 dmg every 0.5s while touching player.
**Stats:** HP 25 | Speed 1.8 | Attack range 1
**Notes:** Spawned by Slime death. Does not split further.

---

## Skeleton Archer
**Type:** Ranged Kiter
**Movement:** Ground (NavMesh), 3-state AI: repositions → attacks → retreats if player < 5u.
**Attack:** Projectile arrow — 12 dmg per shot, every 2.5s. Speed 8u/s. Range 10u.
**Stats:** HP 40 | Speed 2 | Preferred range 8u
**Notes:** Keeps distance. Backs away if player closes in.

---

## Wraith
**Type:** Flying Stalker
**Movement:** Aerial (ignores terrain and obstacles), direct chase.
**Attack:** Contact damage — deals 8 dmg every 0.6s while touching player.
**Stats:** HP 45 | Speed 3.5 | Attack range 1.2
**Notes:** Bypasses walls. Scales with level (+10% HP, +4% speed, +10% dmg per level).

---

## Roller
**Type:** Heavy Tank
**Movement:** Physics-based rolling ball, rams directly into player.
**Attack:** Contact damage — deals 10 dmg every 0.75s while touching player.
**Stats:** HP 120 | Speed 5 | Attack range 1.2
**Notes:** Highest HP in roster. Fast and hard to dodge. Scales with level (+10% HP, +4% speed, +8% dmg per level).

---

## Bombarder
**Type:** Stationary Artillery
**Movement:** None — never moves. Rotates to track player.
**Attack:** Arcing projectile — fires a parabolic shell to the player's position at fire time. Player can dodge the landing zone. Arc height: 6u.
**Stats:** HP / Dmg — TBD (no data asset configured yet)
**Notes:** Global range — attacks from anywhere on the map.

---

## Summoner
**Type:** Support / Ranged
**Movement:** Ground (NavMesh), 3-state AI: repositions → attacks → retreats if player < 5u. Preferred range 9u.
**Attack:** Projectile arrows (same pattern as Skeleton Archer). Also summons 2 Goblins every 5s (max 6 active at once).
**Stats:** HP / Dmg — TBD (no data asset configured yet)
**Notes:** Minions persist after the Summoner dies. Kill priority target.

---

## Artillery
**Type:** Slow Ranged Lobber
**Movement:** Ground (NavMesh), slow chase. Stops at 7u and fires.
**Attack:** ArcBlastProjectile — lobs a shell in a parabolic arc (height 4u, travel 1.8s). Explodes on landing with AOE blast (radius 2.5) dealing 8 dmg to ALL targets hit. Fires every 4s.
**Stats:** HP 30 | Speed 1.5 | Attack range 7
**Notes:** Low HP — dies fast if reached. Blast catches grouped players. Player can dodge the landing zone before impact.

---

*Last updated: 2026-04-05*
