using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Decides when the player attacks and dispatches to the correct executor.
/// Auto-fires toward the nearest tagged enemy each cooldown cycle.
/// Stops attacking automatically when no enemies are present in the scene.
///
/// Extends NetworkBehaviour so IsOwner is available — only the machine that
/// owns this player runs the attack logic. This prevents both machines from
/// independently simulating the same player's attacks (desync).
///
/// For projectile attacks, the owner sends a SpawnProjectileServerRpc to the server.
/// The server instantiates and spawns the NetworkObject, then calls InitializeClientRpc
/// on the projectile so all clients receive the trajectory data and simulate it locally.
/// This ensures all players see all projectiles — one source of truth.
///
/// Responsibilities:
///   - Scan for nearest enemy each frame via Unity tag lookup (owner only)
///   - Ask AttackController whether the attack cooldown is ready
///   - Route to the correct executor based on AttackType
///   - For projectiles: request server-side networked spawn via ServerRpc
///   - Mark the cooldown after a successful execution
/// </summary>
public class PlayerCombatBrain : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("The attack controller that owns the cooldown and AttackDefinition. " +
             "Auto-resolved from this GameObject if left empty.")]
    [SerializeField] private AttackController attackController;

    [Header("Targeting")]
    [Tooltip("Unity tag that marks enemy GameObjects. Must match the tag applied to enemy prefabs.")]
    [SerializeField] private string enemyTag = "Enemy";

    // The nearest live enemy this frame — null when no enemies exist.
    private Transform _currentTarget;

    // Track the previous target to avoid spamming the log every frame.
    private Transform _lastLoggedTarget;

    // Cached reference to random element/shape setup. Null if not attached — falls back to AttackDefinition.
    private PlayerElementSetup _elementSetup;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve so the component works when the reference is not set manually.
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
            Debug.LogError($"[PlayerCombatBrain] {gameObject.name} has no AttackController assigned or found.", this);

        // Cache optional element setup for random element/shape override
        TryGetComponent(out _elementSetup);
    }

    private void Update()
    {
        // Only the owning machine runs attack logic.
        // Without this guard both host and client independently fire projectiles
        // for every player in the scene, causing desynchronized local simulations.
        if (!IsOwner) return;

        // Re-scan each frame so we always aim at the current nearest enemy.
        _currentTarget = FindNearestEnemy();

        if (_currentTarget != null)
            TryAttack();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to fire the current attack toward the active target.
    /// Can be called externally (UI buttons, test scripts).
    /// Does nothing if the cooldown is not ready or no target is set.
    /// </summary>
    public void TryAttack()
    {
        if (attackController == null || _currentTarget == null) return;
        if (!attackController.CanUseAttack()) return;

        AttackDefinition def = attackController.AttackDefinition;
        bool success = ExecuteAttack(def);

        // Only spend the cooldown when the attack actually fired —
        // prevents a missing prefab from wasting the player's cooldown.
        if (success)
            attackController.MarkAttackUsed();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Routes to the correct executor based on the AttackDefinition's AttackType.
    /// Returns true when an attack was successfully dispatched.
    /// </summary>
    private bool ExecuteAttack(AttackDefinition def)
    {
        switch (def.AttackType)
        {
            case AttackType.Projectile:
                // Pre-validate on the owner before sending the RPC — avoids a server round-trip
                // for configuration errors like a missing prefab.
                Vector3 dir = GetAttackDirection();
                if (def.ProjectilePrefab == null || dir == Vector3.zero) return false;
                SpawnProjectileServerRpc(dir);
                return true;

            case AttackType.Melee:
                MeleeAttackExecutor.Execute(transform, def);
                return true;

            case AttackType.Contact:
                // ContactDamageDealer handles this automatically — brain must not trigger it.
                Debug.LogWarning("[PlayerCombatBrain] AttackType.Contact is driven by ContactDamageDealer, not the brain.");
                return false;

            default:
                Debug.LogWarning($"[PlayerCombatBrain] Unhandled AttackType: {def.AttackType}");
                return false;
        }
    }

    /// <summary>
    /// Sends a spawn request to the server.
    /// The server instantiates the projectile prefab, spawns it as a NetworkObject (replicating
    /// it to all clients), then immediately calls InitializeClientRpc so every client sets the
    /// same direction and speed and starts simulating movement locally.
    ///
    /// Called only from the owner (IsOwner guard in Update).
    /// RequireOwnership = true (default) enforces this on the server side as well.
    /// </summary>
    [ServerRpc]
    private void SpawnProjectileServerRpc(Vector3 direction)
    {
        if (attackController == null) return;
        AttackDefinition def = attackController.AttackDefinition;

        if (def == null)
        {
            Debug.LogError("[PlayerCombatBrain] SpawnProjectileServerRpc: AttackDefinition is null on server.");
            return;
        }

        if (def.ProjectilePrefab == null)
        {
            Debug.LogError($"[PlayerCombatBrain] '{def.AttackId}' has no ProjectilePrefab assigned.");
            return;
        }

        if (direction == Vector3.zero)
        {
            Debug.LogWarning("[PlayerCombatBrain] SpawnProjectileServerRpc: direction is zero — skipping spawn.");
            return;
        }

        // Use player's randomly assigned element/shape if available, else fall back to definition
        ElementType elementToUse = def.ElementType;
        float strengthToUse = def.ElementStrength;
        ProjectileShape shapeToUse = ProjectileShape.Orb;

        if (_elementSetup != null && _elementSetup.AssignedElement != ElementType.None)
        {
            elementToUse = _elementSetup.AssignedElement;
            shapeToUse   = _elementSetup.AssignedShape;
        }

        // Server instantiates the projectile and spawns it as a NetworkObject.
        // NGO replicates the spawn to all connected clients automatically.
        Projectile projectile = Instantiate(
            def.ProjectilePrefab,
            transform.position,
            Quaternion.LookRotation(direction)
        );

        projectile.NetworkObject.Spawn();

        // Send trajectory, stats, element, and shape to all clients.
        projectile.InitializeClientRpc(
            damage:          def.Damage,
            sourceRef:       NetworkObject,
            direction:       direction,
            speed:           def.ProjectileSpeed,
            elementType:     elementToUse,
            elementStrength: strengthToUse,
            targetTag:       def.ProjectileTargetTag,
            shape:           shapeToUse
        );

        Debug.Log($"[PlayerCombatBrain] Server spawned '{def.AttackId}' for '{name}' " +
                  $"(element:{elementToUse}, shape:{shapeToUse}).");
    }

    /// <summary>
    /// Returns the normalized world-space direction from the player toward the current target.
    /// Y component is zeroed to keep projectiles flying on the horizontal plane.
    /// Falls back to transform.forward when no target is set.
    /// </summary>
    private Vector3 GetAttackDirection()
    {
        if (_currentTarget == null)
            return transform.forward;

        Vector3 dir = _currentTarget.position - transform.position;
        dir.y = 0f; // Stay on the horizontal plane — avoids lobbing projectiles upward.
        return dir == Vector3.zero ? transform.forward : dir.normalized;
    }

    /// <summary>
    /// Scans all GameObjects tagged with enemyTag and returns the Transform of the closest one.
    /// Returns null when no enemies are present in the scene.
    ///
    /// Note: FindGameObjectsWithTag allocates each call — acceptable for a test scene.
    /// Production builds should replace this with a centrally-managed enemy registry.
    /// </summary>
    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies.Length == 0) return null;

        Transform nearest = null;
        float nearestSqDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            float sqDist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest = enemy.transform;
            }
        }

        // Only log when the target changes — avoids console spam every frame.
        if (nearest != _lastLoggedTarget)
        {
            _lastLoggedTarget = nearest;
            if (nearest != null)
                Debug.Log($"[PlayerCombatBrain] New target: '{nearest.name}' at {Mathf.Sqrt(nearestSqDist):F1}m");
            else
                Debug.Log("[PlayerCombatBrain] No targets — attack stopped.");
        }

        return nearest;
    }
}
