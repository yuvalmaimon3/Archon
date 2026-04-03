using Unity.Netcode;
using UnityEngine;

// Handles knockback on enemies hit by attacks or abilities.
//
// When ApplyKnockback is called (server-side):
//   1. Tells EnemyMovement to disable the NavMeshAgent
//   2. Sets Rigidbody non-kinematic and applies a force impulse
//   3. Waits for velocity to drop below threshold (or timeout)
//   4. Tells EnemyMovement to re-enable the NavMeshAgent
//
// Callers just call ApplyKnockback(direction, force) — no knowledge of
// NavMesh or Rigidbody internals required.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyMovement))]
public class KnockbackHandler : NetworkBehaviour
{
    [Header("Settings")]
    // Maximum time in knockback state before forcing recovery, regardless of velocity.
    [SerializeField] private float maxKnockbackDuration = 0.5f;

    // When velocity drops below this speed the enemy is considered settled.
    [SerializeField] private float settleSpeedThreshold = 0.5f;

    // Is the enemy currently in knockback state?
    public bool IsKnockedBack { get; private set; }

    private Rigidbody      _rb;
    private EnemyMovement  _movement;
    private float          _knockbackEndTime;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _movement = GetComponent<EnemyMovement>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!IsKnockedBack) return;

        // Recover when velocity drops below threshold OR timeout expires.
        bool settled = _rb.linearVelocity.sqrMagnitude <= settleSpeedThreshold * settleSpeedThreshold;
        bool timedOut = Time.time >= _knockbackEndTime;

        if (settled || timedOut)
            RecoverFromKnockback();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // Applies a knockback impulse in the given world-space direction.
    // Only runs on the server — clients see the result via NetworkTransform.
    // direction: normalized world-space push direction (e.g. away from attacker).
    // force: impulse magnitude.
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (!IsServer) return;
        if (IsKnockedBack) return; // Already knocked back — ignore stacking

        IsKnockedBack    = true;
        _knockbackEndTime = Time.time + maxKnockbackDuration;

        // Hand off to EnemyMovement so it disables the agent and makes Rigidbody physical.
        _movement.DisableAgentForKnockback();

        // Apply the impulse — flat direction only, no vertical launch.
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
        _rb.AddForce(flatDirection * force, ForceMode.Impulse);

        Debug.Log($"[KnockbackHandler] '{name}' knocked back — force:{force} dir:{flatDirection}");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Called once the enemy has settled after knockback.
    private void RecoverFromKnockback()
    {
        IsKnockedBack = false;
        _movement.ReEnableAgent();

        Debug.Log($"[KnockbackHandler] '{name}' recovered from knockback.");
    }
}
