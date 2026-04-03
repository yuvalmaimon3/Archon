using UnityEngine;

// AI brain for the Skeleton Archer.
// Controls positioning and attack timing using a three-state machine:
//
//   Reposition — player is out of attack range; move toward player until in range.
//   Attack     — player is at a comfortable distance; stand still and shoot.
//   Retreat    — player is too close; back away to preferred range before attacking.
//
// Movement is delegated to SkeletonArcherMovement (NavMesh-based).
// Attack execution is delegated to AttackController + ProjectileAttackExecutor.
//
// All logic runs server-only (NGO). Implements IDeathHandler so targeting
// and movement are cleanly stopped when the archer dies.
public class SkeletonArcherBrain : MonoBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Attack controller on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private AttackController attackController;

    [Tooltip("Movement component on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private SkeletonArcherMovement movement;

    [Header("Positioning")]
    [Tooltip("Ideal distance the archer tries to maintain from the player. " +
             "Should be between tooCloseThreshold and attackRange.")]
    [SerializeField] private float preferredRange = 8f;

    [Tooltip("If the player gets closer than this, the archer retreats. " +
             "Must be less than preferredRange.")]
    [SerializeField] private float tooCloseThreshold = 5f;

    [Header("Target")]
    [Tooltip("Unity tag used to locate the player. " +
             "Must match the tag set on the player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    // ── Private state ────────────────────────────────────────────────────────

    // Current state of the positioning state machine.
    private ArcherState _state = ArcherState.Reposition;

    // Cached target transform — refreshed every frame via FindNearestPlayer.
    private Transform _target;

    // Set to true by OnDeath() to stop all updates immediately.
    private bool _isDead;

    // Previous state — used only for debug logging on transitions.
    private ArcherState _lastLoggedState;

    // ── State enum ───────────────────────────────────────────────────────────

    // Three-state machine that drives the archer's behavior each frame.
    private enum ArcherState
    {
        Reposition, // Moving toward the player to get into attack range
        Attack,     // Standing still, firing projectiles at the player
        Retreat,    // Backing away because the player is too close
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (movement == null)
            movement = GetComponent<SkeletonArcherMovement>();

        if (attackController == null)
            Debug.LogError($"[SkeletonArcherBrain] '{name}' missing AttackController.", this);

        if (movement == null)
            Debug.LogError($"[SkeletonArcherBrain] '{name}' missing SkeletonArcherMovement.", this);
    }

    // Runs only on the server — clients see the result via NetworkTransform.
    private void Update()
    {
        // Only the server drives AI — bail out on clients and after death.
        if (_isDead) return;
        if (!IsServerCheck()) return;
        if (attackController == null || movement == null) return;

        // Re-scan every frame to handle multiple players and late-joins.
        _target = FindNearestPlayer();

        if (_target == null)
        {
            // No player found — stop and wait.
            movement.Stop();
            return;
        }

        UpdateState();
        ExecuteState();
    }

    // ── State machine ────────────────────────────────────────────────────────

    // Evaluates the current distance to the player and transitions to the
    // appropriate state. Hysteresis is intentionally not used here — the
    // thresholds are designer-configurable and naturally create stable zones.
    private void UpdateState()
    {
        float distance  = Vector3.Distance(transform.position, _target.position);
        float atkRange  = GetAttackRange();

        ArcherState next;

        if (distance < tooCloseThreshold)
        {
            // Player inside the danger bubble — retreat immediately.
            next = ArcherState.Retreat;
        }
        else if (distance <= atkRange)
        {
            // Player within attack range and not too close — attack in place.
            next = ArcherState.Attack;
        }
        else
        {
            // Player out of range — close the distance.
            next = ArcherState.Reposition;
        }

        // Log only on state change to avoid console spam.
        if (next != _lastLoggedState)
        {
            _lastLoggedState = next;
            Debug.Log($"[SkeletonArcherBrain] '{name}' → {next} " +
                      $"(dist:{distance:F1}, atkRange:{atkRange:F1}, tooClose:{tooCloseThreshold:F1})");
        }

        _state = next;
    }

    // Runs the behavior for the current state.
    private void ExecuteState()
    {
        switch (_state)
        {
            case ArcherState.Reposition:
                ExecuteReposition();
                break;

            case ArcherState.Attack:
                ExecuteAttack();
                break;

            case ArcherState.Retreat:
                ExecuteRetreat();
                break;
        }
    }

    // Moves toward the player until within attack range.
    // The NavMeshAgent handles pathfinding around obstacles.
    private void ExecuteReposition()
    {
        movement.MoveTo(_target.position);
        movement.FaceToward(_target.position);
    }

    // Stops movement, faces the player, and fires when the cooldown is ready.
    private void ExecuteAttack()
    {
        movement.Stop();
        movement.FaceToward(_target.position);

        if (!attackController.CanUseAttack()) return;

        AttackDefinition def      = attackController.AttackDefinition;
        Vector3          direction = (_target.position - transform.position).normalized;

        // ProjectileAttackExecutor spawns the projectile — pass EffectiveDamage
        // so level scaling from EnemyInitializer is applied.
        var projectile = ProjectileAttackExecutor.Execute(
            transform, direction, def, attackController.EffectiveDamage
        );

        if (projectile != null)
            attackController.MarkAttackUsed();
    }

    // Backs away from the player toward a point at preferredRange distance.
    // Calculates a retreat destination directly behind the archer (away from the player).
    private void ExecuteRetreat()
    {
        Vector3 dirAwayFromPlayer = (transform.position - _target.position).normalized;
        // Target a position at preferredRange behind the archer — once reached,
        // distance check will switch to Attack state.
        Vector3 retreatDestination = _target.position + dirAwayFromPlayer * preferredRange;

        movement.MoveTo(retreatDestination);
        // Keep facing the player while retreating so the archer stays visually aware.
        movement.FaceToward(_target.position);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Returns the attack range from the AttackDefinition (set in the asset).
    // Falls back to preferredRange + 1 if no definition is assigned yet.
    private float GetAttackRange()
    {
        return attackController.AttackDefinition != null
            ? attackController.AttackDefinition.Range
            : preferredRange + 1f;
    }

    // Finds the nearest live player using the player tag.
    // Allocation from FindGameObjectsWithTag is acceptable — production builds
    // should replace this with a centralized player registry (same note as EnemyCombatBrain).
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

    // NGO convenience — brain works on server only but does not extend NetworkBehaviour.
    // Checks via a sibling NetworkBehaviour (EnemyMovementBase extends NetworkBehaviour).
    private bool IsServerCheck()
    {
        return movement != null && movement.IsServer;
    }

    // ── IDeathHandler ────────────────────────────────────────────────────────

    // Called by DeathController when this entity's health reaches zero.
    // Stops all movement and disables the state machine immediately.
    public void OnDeath()
    {
        _isDead = true;
        _target = null;

        movement?.Stop();

        Debug.Log($"[SkeletonArcherBrain] '{name}' — AI stopped on death.");
    }
}
