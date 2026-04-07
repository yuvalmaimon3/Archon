using Unity.Netcode;
using UnityEngine;

// Handles knockback on enemies hit by attacks or abilities.
//
// Works with any EnemyMovementBase subclass (ground or flying):
//   1. Calls movement.StartKnockback() — subclass disables its locomotion
//   2. Applies a Rigidbody impulse force
//   3. Waits for velocity to settle or timeout to expire
//   4. Calls movement.EndKnockback() — subclass resumes locomotion
//
// Callers only need: ApplyKnockback(direction, force)
[RequireComponent(typeof(Rigidbody))]
public class KnockbackHandler : NetworkBehaviour
{
    [Header("Settings")]
    // Maximum time in knockback before forcing recovery, regardless of velocity.
    [SerializeField] private float maxKnockbackDuration = 0.5f;

    // Enemy is considered settled when speed drops below this threshold.
    [SerializeField] private float settleSpeedThreshold = 0.5f;

    // Is the enemy currently being knocked back?
    public bool IsKnockedBack { get; private set; }

    private Rigidbody        _rb;
    private EnemyMovementBase _movement;
    private float             _knockbackEndTime;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _movement = GetComponent<EnemyMovementBase>();

        if (_movement == null)
            Debug.LogError($"[KnockbackHandler] '{name}' has no EnemyMovementBase component.", this);
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!IsKnockedBack) return;

        bool settled  = _rb.linearVelocity.sqrMagnitude <= settleSpeedThreshold * settleSpeedThreshold;
        bool timedOut = Time.time >= _knockbackEndTime;

        if (settled || timedOut)
            RecoverFromKnockback();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // Applies a knockback impulse. Server-only — clients see result via NetworkTransform.
    // direction: world-space push direction (e.g. away from the attacker).
    // force: impulse magnitude.
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (!IsServer) return;
        if (IsKnockedBack) return; // Don't stack knockbacks

        IsKnockedBack     = true;
        _knockbackEndTime = Time.time + maxKnockbackDuration;

        // Tell the movement component to hand off control to Rigidbody.
        _movement?.StartKnockback();

        // Flat impulse — no vertical launch for ground enemies.
        // FlyingEnemyMovement keeps Rigidbody gravity off, so Y stays neutral too.
        Vector3 flatDir = new Vector3(direction.x, 0f, direction.z).normalized;
        _rb.AddForce(flatDir * force, ForceMode.Impulse);

        Debug.Log($"[KnockbackHandler] '{name}' knocked back — force:{force} dir:{flatDir}");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void RecoverFromKnockback()
    {
        IsKnockedBack = false;
        _movement?.EndKnockback();

        Debug.Log($"[KnockbackHandler] '{name}' recovered from knockback.");
    }
}
