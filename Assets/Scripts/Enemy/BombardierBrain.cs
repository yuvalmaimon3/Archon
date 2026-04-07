using UnityEngine;

// AI brain for the Bombarder — a stationary artillery enemy.
//
// Behavior:
//   The Bombarder never moves. It rotates in place to track the nearest player
//   and fires arcing projectiles at that player's position on each attack cycle.
//   Its effective range covers the entire map — there is no minimum distance
//   required before it begins firing.
//
// Attack execution:
//   Spawns an ArcProjectile that follows a parabolic arc from the Bombarder's
//   position to the player's position captured at fire time. The player can
//   dodge by moving out of the landing zone before the shell arrives.
//
// Networking:
//   All logic runs server-only (NGO). Clients see the result via NetworkTransform
//   and the ArcProjectile's own client-side movement.
//   Implements IDeathHandler so targeting is stopped cleanly on death.
public class BombardierBrain : MonoBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Attack controller on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private AttackController attackController;

    [Tooltip("Stationary movement component on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private BombardierMovement movement;

    [Header("Projectile")]
    [Tooltip("The ArcProjectile prefab to instantiate on each attack. " +
             "Assign the Bombarder-specific arc shell prefab here.")]
    [SerializeField] private ArcProjectile arcProjectilePrefab;

    [Tooltip("Height of the parabolic arc above the straight-line midpoint. " +
             "Higher values create more dramatic arcs that clear taller obstacles.")]
    [Min(0f)]
    [SerializeField] private float arcHeight = 6f;

    [Header("Target")]
    [Tooltip("Unity tag used to locate the player. Must match the tag set on the player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    // ── Private state ────────────────────────────────────────────────────────

    // Cached target — refreshed every frame via PlayerFinder.
    private Transform _target;

    // Set to true by OnDeath() to freeze all updates immediately.
    private bool _isDead;

    // Used for debug logging — only logs target changes, not every frame.
    private Transform _lastLoggedTarget;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (movement == null)
            movement = GetComponent<BombardierMovement>();

        if (attackController == null)
            Debug.LogError($"[BombardierBrain] '{name}' missing AttackController.", this);

        if (arcProjectilePrefab == null)
            Debug.LogError($"[BombardierBrain] '{name}' missing ArcProjectile prefab — " +
                           "assign it in the Inspector.", this);

        if (movement == null)
            Debug.LogError($"[BombardierBrain] '{name}' missing BombardierMovement.", this);
    }

    // Runs server-only — clients see movement via NetworkTransform.
    private void Update()
    {
        if (_isDead) return;
        if (!IsServerCheck()) return;
        if (attackController == null || arcProjectilePrefab == null) return;

        // Re-scan every frame to handle multiple players and late-joins.
        _target = PlayerFinder.FindNearest(transform.position, playerTag);

        // Log only on target change — avoids console spam.
        if (_target != _lastLoggedTarget)
        {
            _lastLoggedTarget = _target;
            if (_target != null)
                Debug.Log($"[BombardierBrain] '{name}' targeting '{_target.name}'.");
            else
                Debug.Log($"[BombardierBrain] '{name}' — no players found.");
        }

        if (_target == null) return;

        // Bombarder attacks from anywhere on the map — no range gate.
        TryFireAtTarget();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Fires an arc projectile at the current target if the cooldown has elapsed.
    // The target position is captured at fire time — the player can dodge the landing.
    private void TryFireAtTarget()
    {
        if (!attackController.CanUseAttack()) return;

        // Snapshot player position — the arc travels to where they were when fired.
        // Intentional design: gives the player a window to move out of the landing zone.
        Vector3 targetPos = _target.position;

        // Spawn the arc shell at the Bombarder's world position.
        ArcProjectile projectile = Object.Instantiate(
            arcProjectilePrefab,
            transform.position,
            Quaternion.identity
        );

        // Pull element data from the AttackDefinition if one is assigned.
        // Allows the designer to add elemental effects to Bombarder attacks via EnemyData.
        ElementType elementType    = ElementType.None;
        float       elementStrength = 0f;

        if (attackController.AttackDefinition != null)
        {
            elementType     = attackController.AttackDefinition.ElementType;
            elementStrength = attackController.AttackDefinition.ElementStrength;
        }

        var elementApplication = new ElementApplication(
            element:  elementType,
            strength: elementStrength,
            source:   gameObject
        );

        // Initialize the arc — uses EffectiveDamage so level scaling is applied.
        projectile.Initialize(
            damage:             attackController.EffectiveDamage,
            source:             gameObject,
            startPosition:      transform.position,
            targetPosition:     targetPos,
            arcHeight:          arcHeight,
            elementApplication: elementApplication,
            targetTag:          "Player"
        );

        attackController.MarkAttackUsed();

        Debug.Log($"[BombardierBrain] '{name}' fired arc shell at '{_target.name}' " +
                  $"(dist:{Vector3.Distance(transform.position, targetPos):F1}, " +
                  $"damage:{attackController.EffectiveDamage}).");
    }

    // Checks the server flag through the sibling BombardierMovement (NetworkBehaviour).
    // The brain itself is a plain MonoBehaviour — this avoids extending NetworkBehaviour
    // while still respecting the server-only execution pattern used across all enemy brains.
    private bool IsServerCheck()
    {
        return movement != null && movement.IsServer;
    }

    // ── IDeathHandler ────────────────────────────────────────────────────────

    // Called by DeathController when this entity's HP reaches zero.
    // Clears the target and stops all AI updates immediately.
    public void OnDeath()
    {
        _isDead = true;
        _target = null;
        _lastLoggedTarget = null;

        movement?.SetDead();

        Debug.Log($"[BombardierBrain] '{name}' — AI stopped on death.");
    }
}
