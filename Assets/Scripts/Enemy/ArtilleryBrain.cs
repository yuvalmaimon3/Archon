using UnityEngine;

// AI brain for the Artillery enemy — a slow ground unit that lobs blast shells
// in parabolic arcs from medium distance.
//
// Behavior:
//   Movement is fully delegated to GroundEnemyMovement (NavMesh-based).
//   That component already handles approaching the player and stopping at AttackRange.
//   This brain only needs to fire once the enemy is in range.
//
//   Two-state machine:
//     Approach — GroundEnemyMovement is moving; brain waits for target to enter range.
//     Attack   — Target is within range; stand still and fire blast shells on cooldown.
//
// Attack execution:
//   Spawns an ArcBlastProjectile that follows a parabolic arc to the player's
//   captured position. The blast damages ALL targets in the explosion radius —
//   the player can dodge by leaving the landing zone before impact.
//
// Networking:
//   All logic runs server-only (NGO). Movement and projectiles replicate via
//   NetworkTransform and NGO-spawned NetworkObjects respectively.
//   Implements IDeathHandler so firing stops immediately on death.
public class ArtilleryBrain : MonoBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Attack controller on this GameObject. Auto-resolved from this GO if left empty.")]
    [SerializeField] private AttackController attackController;

    [Tooltip("Ground movement component. Auto-resolved from this GO if left empty.")]
    [SerializeField] private GroundEnemyMovement movement;

    [Header("Projectile")]
    [Tooltip("The ArcBlastProjectile prefab to fire. Must have NetworkObject if using multiplayer.")]
    [SerializeField] private ArcBlastProjectile blastProjectilePrefab;

    [Tooltip("Height of the parabolic arc above the straight-line midpoint. " +
             "Higher = more dramatic arc; lower = flatter, faster-to-read trajectory.")]
    [Min(0f)]
    [SerializeField] private float arcHeight = 4f;

    [Header("Target")]
    [Tooltip("Unity tag used to locate the player. Must match the tag on player GameObjects.")]
    [SerializeField] private string playerTag = "Player";

    // ── Private state ────────────────────────────────────────────────────────

    // Cached target — refreshed every frame via PlayerFinder.
    private Transform _target;

    // Set to true by OnDeath() — stops all firing immediately.
    private bool _isDead;

    // Used for debug logging — prevents log spam on every frame.
    private Transform _lastLoggedTarget;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (movement == null)
            movement = GetComponent<GroundEnemyMovement>();

        if (attackController == null)
            Debug.LogError($"[ArtilleryBrain] '{name}' missing AttackController.", this);

        if (movement == null)
            Debug.LogError($"[ArtilleryBrain] '{name}' missing GroundEnemyMovement.", this);

        if (blastProjectilePrefab == null)
            Debug.LogError($"[ArtilleryBrain] '{name}' missing ArcBlastProjectile prefab — " +
                           "assign it in the Inspector.", this);
    }

    // Runs server-only — clients see results via NetworkTransform and spawned projectiles.
    private void Update()
    {
        if (_isDead) return;
        if (!IsServerCheck()) return;
        if (attackController == null || blastProjectilePrefab == null) return;

        // Re-scan every frame so the artillery always targets the nearest live player.
        _target = PlayerFinder.FindNearest(transform.position, playerTag);

        // Log only on target change to avoid console spam.
        if (_target != _lastLoggedTarget)
        {
            _lastLoggedTarget = _target;
            Debug.Log(_target != null
                ? $"[ArtilleryBrain] '{name}' targeting '{_target.name}'."
                : $"[ArtilleryBrain] '{name}' — no players found.");
        }

        if (_target == null) return;

        // GroundEnemyMovement handles approaching automatically via NavMesh.
        // This brain only acts when the target is already within attack range.
        float distance    = Vector3.Distance(transform.position, _target.position);
        float attackRange = attackController.AttackDefinition?.Range ?? 7f;

        if (distance <= attackRange)
            TryFireBlastShell();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Fires an arc blast shell at the player's current position if the cooldown is ready.
    // The target position is snapshotted at fire time — the player can dodge the landing.
    private void TryFireBlastShell()
    {
        if (!attackController.CanUseAttack()) return;

        // Snapshot the player's position: the shell will land where they were when fired.
        // Intentional design: gives the player a reaction window to move out of the blast zone.
        Vector3 targetPos = _target.position;

        ArcBlastProjectile projectile = Object.Instantiate(
            blastProjectilePrefab,
            transform.position,
            Quaternion.identity
        );

        // Pull element data from the AttackDefinition so designers can add elemental effects
        // to Artillery attacks simply by changing the definition asset.
        ElementType elementType     = ElementType.None;
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

        // Initialize the blast shell. Uses EffectiveDamage so level scaling is applied.
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

        Debug.Log($"[ArtilleryBrain] '{name}' fired blast shell at '{_target.name}' " +
                  $"(dist:{Vector3.Distance(transform.position, targetPos):F1}, " +
                  $"damage:{attackController.EffectiveDamage}, arcHeight:{arcHeight}).");
    }

    // Artillery brain is a plain MonoBehaviour; check server status via the sibling
    // GroundEnemyMovement (which extends NetworkBehaviour).
    private bool IsServerCheck() => movement != null && movement.IsServer;

    // ── IDeathHandler ────────────────────────────────────────────────────────

    // Called by DeathController the moment this entity's HP reaches zero.
    // Stops all firing immediately — movement cleanup is handled by GroundEnemyMovement.
    public void OnDeath()
    {
        _isDead = true;
        _target = null;
        _lastLoggedTarget = null;

        Debug.Log($"[ArtilleryBrain] '{name}' — AI stopped on death.");
    }
}
