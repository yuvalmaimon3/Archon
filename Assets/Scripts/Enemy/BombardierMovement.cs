using UnityEngine;

// Stationary movement implementation for the Bombarder enemy.
// The Bombarder never translates — it only rotates in place to face the nearest player.
// Implements EnemyMovementBase so EnemyInitializer can initialize it without special-casing.
//
// Knockback is intentionally ignored: a heavy artillery emplacement should not be
// pushed around by regular knockback forces.
public class BombardierMovement : EnemyMovementBase
{
    [Header("Bombarder Movement")]
    [Tooltip("If true, the Bombarder rotates to face the nearest player each frame. " +
             "Disable if the model should stay fixed in its placed orientation.")]
    [SerializeField] private bool facePlayerEachFrame = true;

    // Set to true by SetDead() when the Bombarder dies — stops all rotation updates.
    private bool _isDead;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Update()
    {
        if (_isDead) return;
        if (!IsServer) return;
        if (!facePlayerEachFrame) return;

        // Rotate toward the nearest live player so attacks visually aim at the target.
        Transform player = FindNearestPlayer();
        if (player != null)
            FaceTarget(player.position);
    }

    // ── EnemyMovementBase implementation ────────────────────────────────────

    // Called by EnemyInitializer after EnemyData is assigned.
    // Bombarder is stationary — no NavMesh or velocity setup required.
    protected override void OnInitialized(EnemyData data)
    {
        Debug.Log($"[BombardierMovement] '{name}' initialized as '{data.EnemyName}' — stationary.");
    }

    // Bombarder is rooted — disable rotation so it does not spin while being hit.
    protected override void OnKnockbackStart()
    {
        // Intentionally empty: heavy emplacement ignores knockback locomotion.
    }

    // Re-enable facing after a knockback window ends.
    protected override void OnKnockbackEnd()
    {
        // Intentionally empty: nothing was disabled, nothing to restore.
    }

    // Speed has no meaning for a stationary enemy — silently ignored.
    public override void SetMoveSpeed(float speed) { }

    // ── Public API ───────────────────────────────────────────────────────────

    // Called by BombardierBrain on death to stop all AI-driven rotation.
    public void SetDead()
    {
        _isDead = true;
    }
}
