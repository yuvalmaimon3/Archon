using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Wraith-specific flying movement: orbits the player at a preferred distance
/// and retreats when the player gets too close.
///
/// Three zones drive behavior each frame:
///   - Too close  (distance &lt; retreatDistance)  → move directly away
///   - Orbit      (retreatDistance .. orbitDistance) → strafe laterally
///   - Approach   (distance &gt; orbitDistance)    → move toward player
///
/// Strafing direction flips on a timer so the Wraith circles rather than
/// always moving the same way. Hover height keeps it floating above the player.
///
/// Kinematic Rigidbody — no gravity, no physics solver.
/// Server-only locomotion; clients receive position via NetworkTransform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WraithMovement : EnemyMovementBase
{
    [Header("Orbit Settings")]
    [Tooltip("Distance below which the Wraith retreats from the player.")]
    [SerializeField] private float retreatDistance = 4f;

    [Tooltip("Distance above which the Wraith approaches the player. " +
             "Between retreatDistance and orbitDistance it strafes.")]
    [SerializeField] private float orbitDistance = 8f;

    [Tooltip("Height above the player's Y position the Wraith tries to maintain.")]
    [SerializeField] private float hoverHeight = 2.5f;

    [Tooltip("Seconds between strafe direction flips. Controls how tightly it circles.")]
    [Min(0.5f)]
    [SerializeField] private float strafeFlipInterval = 3f;

    // +1 or -1 — which side of the player the Wraith is strafing toward.
    private float _strafeSign = 1f;

    // Time.time when the next strafe flip occurs.
    private float _nextStrafeFlip;

    private Rigidbody _rb;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Kinematic — locomotion is via transform, not physics forces.
        _rb.useGravity     = false;
        _rb.isKinematic    = true;
        _rb.freezeRotation = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Schedule the first strafe flip immediately so timing starts on spawn.
        _nextStrafeFlip = Time.time + strafeFlipInterval;

        Debug.Log($"[WraithMovement] '{name}' spawned on server.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (EnemyData == null) return;
        if (IsKnockedBack) return;

        Transform target = FindNearestPlayer();
        if (target == null) return;

        // Tick the strafe direction flip timer.
        TickStrafeFlip();

        Vector3 targetPos = target.position;
        float   distance  = Vector3.Distance(transform.position, targetPos);

        Vector3 move = CalculateMovement(targetPos, distance);

        // Apply movement — kinematic, so we use MoveTowards for clean clamping.
        transform.position = Vector3.MoveTowards(
            transform.position,
            transform.position + move,
            EnemyData.MoveSpeed * Time.deltaTime
        );

        // Always face the player regardless of movement direction.
        FaceTarget(targetPos);
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    protected override void OnInitialized(EnemyData data)
    {
        // Speed is read from EnemyData.MoveSpeed in Update — nothing to configure.
    }

    // Knockback: switch to physical mode so forces can be applied in 3D.
    protected override void OnKnockbackStart()
    {
        _rb.isKinematic = false;
        _rb.useGravity  = false; // Keep gravity off — Wraith floats even when hit.
    }

    // Knockback recovery: return to kinematic direct-movement mode.
    protected override void OnKnockbackEnd()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic    = true;

        Debug.Log($"[WraithMovement] '{name}' recovered from knockback.");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Flips the strafe direction sign when the timer expires.
    /// This makes the Wraith circle rather than drift in one direction forever.
    /// </summary>
    private void TickStrafeFlip()
    {
        if (Time.time < _nextStrafeFlip) return;

        _strafeSign     *= -1f;
        _nextStrafeFlip  = Time.time + strafeFlipInterval;

        Debug.Log($"[WraithMovement] '{name}' strafe direction flipped to {(_strafeSign > 0 ? "right" : "left")}.");
    }

    /// <summary>
    /// Returns a normalized world-space movement direction based on the current zone.
    ///
    /// Retreat: directly away from the player on the XZ plane, with vertical hover correction.
    /// Orbit:   perpendicular to the player direction on XZ — pure lateral strafe.
    /// Approach: directly toward the player, with vertical hover correction.
    ///
    /// Hover is handled by blending toward targetY = player.y + hoverHeight on all zones
    /// except orbit (orbit is lateral only — no vertical hunting while strafing).
    /// </summary>
    private Vector3 CalculateMovement(Vector3 targetPos, float distance)
    {
        // Horizontal direction from Wraith to player.
        Vector3 toPlayer     = (targetPos - transform.position);
        Vector3 toPlayerFlat = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;

        // Target hover Y — used by retreat and approach zones.
        float   targetY      = targetPos.y + hoverHeight;
        float   yDiff        = targetY - transform.position.y;

        if (distance < retreatDistance)
        {
            // Retreat: move away horizontally + correct vertical hover.
            Vector3 horizontal = -toPlayerFlat;
            Vector3 vertical   = Vector3.up * Mathf.Sign(yDiff);
            return (horizontal + vertical * 0.5f).normalized;
        }

        if (distance > orbitDistance)
        {
            // Approach: move toward player + correct vertical hover.
            Vector3 horizontal = toPlayerFlat;
            Vector3 vertical   = Vector3.up * Mathf.Sign(yDiff);
            return (horizontal + vertical * 0.5f).normalized;
        }

        // Orbit: strafe perpendicular to the player direction on the XZ plane.
        // Cross(toPlayerFlat, up) gives a right-perpendicular; _strafeSign flips it.
        Vector3 strafe = Vector3.Cross(toPlayerFlat, Vector3.up) * _strafeSign;
        return strafe.normalized;
    }
}
