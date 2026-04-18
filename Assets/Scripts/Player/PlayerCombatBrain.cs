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

    // Holds active projectile-modifying upgrades (e.g. Shotgun split).
    // Consulted on the server inside SpawnProjectileServerRpc.
    private PlayerProjectileModifiers _projectileModifiers;

    // Crit stats — server reads this when rolling crit for projectile spawns.
    private PlayerCritHandler _critHandler;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve so the component works when the reference is not set manually.
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
            Debug.LogError($"[PlayerCombatBrain] {gameObject.name} has no AttackController assigned or found.", this);

        // Optional — only present when a projectile-modifying upgrade has been applied.
        _projectileModifiers = GetComponent<PlayerProjectileModifiers>();

        _critHandler = GetComponent<PlayerCritHandler>();
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
                // Roll crit on the owner — bake multiplier into damage so reactions inherit it.
                if (_critHandler == null)
                    _critHandler = GetComponent<PlayerCritHandler>();

                bool meleeCrit    = _critHandler != null && _critHandler.RollCrit();
                int  meleeDamage  = attackController.RollDamage();
                if (meleeCrit)
                {
                    int preCrit = meleeDamage;
                    meleeDamage = Mathf.RoundToInt(meleeDamage * _critHandler.CritMultiplier);
                    Debug.Log($"[PlayerCombatBrain] MELEE CRITICAL! {preCrit} → {meleeDamage} damage.");
                }

                MeleeAttackExecutor.Execute(transform, def, meleeDamage, meleeCrit);
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

        // Server instantiates the projectile and spawns it as a NetworkObject.
        // NGO replicates the spawn to all connected clients automatically.
        // +1 Y offset: spawns at chest height so the ball clears the floor/player collider
        // immediately — without this the sphere sits at ground level for 1-3 frames on clients
        // before InitializeClientRpc arrives, making it invisible inside the player model.
        Projectile projectile = Instantiate(
            def.ProjectilePrefab,
            transform.position + Vector3.up * 1f,
            Quaternion.LookRotation(direction)
        );

        projectile.NetworkObject.Spawn();

        // Roll crit on the server — bake the multiplier into damage so the entire
        // attack chain (reactions, upgrades) automatically benefits from the crit.
        if (_critHandler == null)
            _critHandler = GetComponent<PlayerCritHandler>();

        bool isCritical   = _critHandler != null && _critHandler.RollCrit();
        int  baseDamage   = attackController.RollDamage();
        int  finalDamage  = isCritical
            ? Mathf.RoundToInt(baseDamage * _critHandler.CritMultiplier)
            : baseDamage;

        if (isCritical)
            Debug.Log($"[PlayerCombatBrain] CRITICAL HIT! {baseDamage} → {finalDamage} damage.");

        // Send trajectory and stats to all clients so each can simulate locally.
        // The deterministic straight-line movement guarantees identical results everywhere.
        projectile.InitializeClientRpc(
            damage:          finalDamage,
            sourceRef:       NetworkObject,
            direction:       direction,
            speed:           def.ProjectileSpeed,
            elementType:     def.ElementType,
            elementStrength: def.ElementStrength,
            targetTag:       def.ProjectileTargetTag,
            isCritical:      isCritical
        );

        // Apply projectile modifiers from active upgrades (e.g. Shotgun split).
        // ConfigureSplit is server-only — no RPC needed, the split spawning also runs on the server.
        // Re-read _projectileModifiers in case it was added after Awake (upgrade applied mid-session).
        if (_projectileModifiers == null)
            _projectileModifiers = GetComponent<PlayerProjectileModifiers>();

        if (_projectileModifiers != null && _projectileModifiers.SplitOnHit)
            projectile.ConfigureSplit(_projectileModifiers.SplitAngleDegrees, def);

        if (_projectileModifiers != null && _projectileModifiers.LifeSteal)
            projectile.ConfigureLifeSteal(_projectileModifiers.LifeStealFraction);

        Debug.Log($"[PlayerCombatBrain] Server spawned '{def.AttackId}' for '{name}' (networked).");
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
