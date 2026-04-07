using UnityEngine;

// Data bag for projectile-affecting upgrades on this player.
// Consulted by PlayerCombatBrain on the server when spawning a projectile.
// Each flag or value here corresponds to one upgrades that modifies projectile behaviour.
//
// Network: MonoBehaviour — only read on the server (inside ServerRpc methods),
//          so no NetworkBehaviour needed.
public class PlayerProjectileModifiers : MonoBehaviour
{
    // ── Split (Shotgun upgrade) ───────────────────────────────────────────────

    // When true, every projectile this player fires will split into 3 on enemy hit:
    // one continues forward, one deviates +SplitAngleDegrees, one deviates -SplitAngleDegrees.
    [Tooltip("Enabled by the Shotgun upgrade. Split projectiles do not split again.")]
    public bool SplitOnHit = false;

    // Angle in degrees between the forward split and each angled split.
    [Tooltip("Degrees from the original direction for the two angled split projectiles.")]
    public float SplitAngleDegrees = 45f;
}
