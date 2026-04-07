using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Self-contained explosion spawned by BlastReactionUpgradeEffect when a reaction occurs.
// Owns all explosion behaviour: damage, (future) knockback, and (future) VFX lifetime.
//
// Lifecycle:
//   1. Server instantiates the prefab and calls InitializeServer() to set damage/radius.
//   2. Server calls NetworkObject.Spawn() — NGO replicates the object to all clients.
//      Clients see the explosion VFX (particle systems on the prefab) automatically.
//   3. OnNetworkSpawn on the server: applies AoE damage to nearby enemies, then starts
//      a lifetime coroutine that despawns the object after the VFX finishes.
//
// Network: NetworkBehaviour — must be seen by all clients for VFX.
//          Damage is server-only (IsServer guard), matching the rest of the combat system.
//
// Setup requirements on the prefab:
//   - A NetworkObject component
//   - A SphereCollider (can be a trigger or non-trigger; only used as a radius reference)
//   - Particle systems as children for the explosion VFX (add later)
public class ReactionExplosion : NetworkBehaviour
{
    [Header("Explosion")]
    [Tooltip("How long (seconds) the explosion GameObject stays alive for VFX to finish playing. " +
             "Set this to match the longest particle system duration on the prefab.")]
    [SerializeField] private float _lifetime = 1f;

    [Header("Future — Knockback")]
    [Tooltip("Force applied outward to enemy Rigidbodies on hit. 0 = no knockback (not yet implemented).")]
    [SerializeField] private float _knockbackForce = 0f;

    [Header("Targeting")]
    [Tooltip("Tag that marks enemy GameObjects.")]
    [SerializeField] private string _enemyTag = "Enemy";

    // ── Server-only data ──────────────────────────────────────────────────────
    // Set before Spawn() via InitializeServer(). Not synced to clients —
    // clients only need to play VFX, not know damage values.

    private int   _damage;
    private float _blastRadius;

    // ── Public API ────────────────────────────────────────────────────────────

    // Called by BlastReactionUpgradeEffect on the server before NetworkObject.Spawn().
    // Stores the explosion parameters so OnNetworkSpawn can apply damage immediately.
    public void InitializeServer(int damage, float blastRadius)
    {
        _damage      = damage;
        _blastRadius = blastRadius;
    }

    // ── NGO lifecycle ─────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Apply damage immediately on spawn — instant AoE, not physics-tick-dependent
        ApplyDamage();

        // Despawn after VFX lifetime so clients see the full effect before it disappears
        StartCoroutine(LifetimeExpiry());
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    // Finds all enemy colliders within _blastRadius and deals _damage to each.
    // Uses element-free DamageInfo — explosion cannot chain-trigger another reaction.
    private void ApplyDamage()
    {
        if (_damage <= 0) return;

        Collider[] hits   = Physics.OverlapSphere(transform.position, _blastRadius);
        int        hitCount = 0;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag(_enemyTag)) continue;
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;

            Vector3 dir = (hit.transform.position - transform.position);

            // TODO: apply _knockbackForce here once knockback is implemented on enemies
            // e.g. if (hit.TryGetComponent<Rigidbody>(out var rb))
            //          rb.AddForce(dir.normalized * _knockbackForce, ForceMode.Impulse);

            var damageInfo = new DamageInfo(
                amount:             _damage,
                source:             gameObject,
                hitPoint:           hit.ClosestPoint(transform.position),
                hitDirection:       dir.normalized,
                elementApplication: default   // no element — prevents reaction chain
            );

            damageable.TakeDamage(damageInfo);
            hitCount++;
        }

        Debug.Log($"[ReactionExplosion] Detonated at {transform.position} " +
                  $"(radius:{_blastRadius}, damage:{_damage}, knockback:{_knockbackForce}) " +
                  $"— hit {hitCount} enemy/enemies.");
    }

    // ── Lifetime ──────────────────────────────────────────────────────────────

    // Waits for VFX to finish, then despawns on all clients.
    private IEnumerator LifetimeExpiry()
    {
        yield return new WaitForSeconds(_lifetime);

        if (IsSpawned)
            NetworkObject.Despawn(destroy: true);
    }
}
