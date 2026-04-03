using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative enemy movement.
/// Chases the nearest living player each frame and stops within attack range.
///
/// Responsibilities:
///   - Find the nearest player with the configured tag
///   - Move toward the player on the server using Rigidbody physics
///   - Stop when within AttackRange (so EnemyCombatBrain can take over)
///   - Face the movement direction
///
/// Works in tandem with EnemyCombatBrain:
///   - Movement stops at AttackRange → brain detects range met → fires attack
///
/// Requires: Rigidbody (non-kinematic on server), EnemyData assigned via EnemyInitializer.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : NetworkBehaviour
{
    [Header("Config")]
    [Tooltip("Enemy stats asset. Assigned at runtime by EnemyInitializer, " +
             "or set directly in the Inspector for standalone testing.")]
    [SerializeField] private EnemyData enemyData;

    [Tooltip("Unity tag used to locate players.")]
    [SerializeField] private string playerTag = "Player";

    // Cached Rigidbody — resolved once in Awake.
    private Rigidbody _rb;

    // The current movement target. Re-evaluated every FixedUpdate on the server.
    private Transform _target;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
    }

    /// <summary>
    /// Called by NGO when this object enters the network session.
    /// Clients receive position via NetworkTransform — their Rigidbody stays kinematic.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            _rb.isKinematic = true;

        Debug.Log($"[EnemyMovement] '{name}' spawned — IsServer:{IsServer} Kinematic:{_rb.isKinematic}");
    }

    private void FixedUpdate()
    {
        // Only the server drives physics for enemies.
        if (!IsServer) return;
        if (enemyData == null) return;

        _target = FindNearestPlayer();

        if (_target == null)
        {
            // No players alive — come to a stop.
            StopMovement();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, _target.position);

        if (distanceToTarget <= enemyData.AttackRange)
        {
            // Within attack range — stop and let EnemyCombatBrain handle the attack.
            StopMovement();
        }
        else
        {
            MoveToward(_target.position);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns the EnemyData that drives movement speed and attack range.
    /// Called by EnemyInitializer after the enemy spawns.
    /// </summary>
    public void Initialize(EnemyData data)
    {
        enemyData = data;
        Debug.Log($"[EnemyMovement] '{name}' initialized with data '{data.EnemyName}'.");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans all live players and returns the nearest Transform.
    /// Dead players are excluded because DeathController changes their tag.
    /// Returns null when no players exist.
    /// </summary>
    private Transform FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        if (players.Length == 0) return null;

        Transform nearest       = null;
        float     nearestSqDist = float.MaxValue;

        foreach (var p in players)
        {
            float sqDist = (p.transform.position - transform.position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest       = p.transform;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Moves the enemy toward the target position at the configured move speed.
    /// Also rotates the enemy to face the movement direction.
    /// </summary>
    private void MoveToward(Vector3 targetPosition)
    {
        // Flat direction — ignore Y so the enemy doesn't tilt up slopes
        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0f;
        direction.Normalize();

        // Apply velocity — preserve Y for gravity
        _rb.linearVelocity = new Vector3(
            direction.x * enemyData.MoveSpeed,
            _rb.linearVelocity.y,
            direction.z * enemyData.MoveSpeed
        );

        // Rotate to face movement direction instantly.
        // Replace with Quaternion.RotateTowards for a smooth turn if desired.
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    /// <summary>
    /// Zeroes out horizontal velocity so the enemy stops cleanly.
    /// Preserves Y velocity so gravity still applies.
    /// </summary>
    private void StopMovement()
    {
        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
    }
}
