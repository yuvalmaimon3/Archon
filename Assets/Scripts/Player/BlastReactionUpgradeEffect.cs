using UnityEngine;

// Applies an AoE explosion to all enemies within blastRadius whenever any reaction occurs.
// Added to the player GameObject server-side when the Blast Reaction upgrade is chosen.
//
// Network: MonoBehaviour — this component is only ever added on the server
//          (PlayerUpgradeHandler.ApplyEffect runs inside ApplyUpgradeServerRpc).
//          ReactionDamageHandler.OnAnyReactionDamage also fires server-side only,
//          so all damage applied here stays server-authoritative.
public class BlastReactionUpgradeEffect : MonoBehaviour
{
    [Header("Blast Settings")]
    [Tooltip("Radius in world units around the reaction position that the explosion reaches.")]
    [SerializeField] private float _blastRadius = 2f;

    [Tooltip("Tag used to identify enemy GameObjects.")]
    [SerializeField] private string _enemyTag = "Enemy";

    // ── Public API ────────────────────────────────────────────────────────────

    // Called by PlayerUpgradeHandler to set the radius from the UpgradeDefinition asset value.
    public void SetRadius(float radius) => _blastRadius = radius;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        ReactionDamageHandler.OnAnyReactionDamage += HandleBlast;
    }

    private void OnDisable()
    {
        ReactionDamageHandler.OnAnyReactionDamage -= HandleBlast;
    }

    // ── Blast logic ───────────────────────────────────────────────────────────

    // Fired when any enemy on the server triggers a reaction.
    // Deals reactionDamage to every enemy collider within _blastRadius of the reaction position.
    // Uses element-free DamageInfo so the explosion cannot trigger further reactions.
    private void HandleBlast(Vector3 reactionPosition, int reactionDamage)
    {
        Collider[] hits = Physics.OverlapSphere(reactionPosition, _blastRadius);

        int hitCount = 0;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(_enemyTag)) continue;
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;

            Vector3 dir = (hit.transform.position - reactionPosition).normalized;

            var damageInfo = new DamageInfo(
                amount:             reactionDamage,
                source:             gameObject,
                hitPoint:           hit.ClosestPoint(reactionPosition),
                hitDirection:       dir,
                elementApplication: default   // no element — prevents reaction chain
            );

            damageable.TakeDamage(damageInfo);
            hitCount++;
        }

        Debug.Log($"[BlastReactionUpgradeEffect] Blast at {reactionPosition} " +
                  $"(radius:{_blastRadius}, damage:{reactionDamage}) hit {hitCount} enemy/enemies.");
    }
}
