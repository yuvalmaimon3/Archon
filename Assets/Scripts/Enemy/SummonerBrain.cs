using UnityEngine;

// AI brain for the Summoner enemy.
//
// Controls positioning and arrow-projectile attack timing using a three-state machine:
//
//   Reposition — player is out of attack range; move toward player until in range.
//   Attack     — player is at a comfortable distance; stand still and shoot.
//   Retreat    — player is too close; back away to preferred range before attacking.
//
// Minion summons are handled by the separate MinionSummoner component — this brain
// does NOT control summoning. Separation ensures summons happen on their own timer
// regardless of combat state.
//
// Movement is delegated to SummonerMovement (NavMesh-based, brain-controlled).
// Attack execution is delegated to AttackController + ProjectileAttackExecutor.
//
// All logic runs server-only (NGO). Implements IDeathHandler so targeting and
// movement are cleanly stopped when the summoner dies.
public class SummonerBrain : MonoBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Attack controller on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private AttackController attackController;

    [Tooltip("Movement component on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private SummonerMovement movement;

    [Header("Positioning")]
    [Tooltip("Ideal distance the summoner tries to maintain from the player. " +
             "Should sit between tooCloseThreshold and the attack range in the AttackDefinition.")]
    [SerializeField] private float preferredRange = 9f;

    [Tooltip("If the player gets closer than this, the summoner retreats. " +
             "Must be less than preferredRange.")]
    [SerializeField] private float tooCloseThreshold = 5f;

    [Header("Target")]
    [Tooltip("Unity tag used to locate the player. Must match the tag on the player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    // ── Private state ────────────────────────────────────────────────────────

    // Current positioning state.
    private SummonerState _state = SummonerState.Reposition;

    // Cached target — refreshed each frame via PlayerFinder.
    private Transform _target;

    // Set to true by OnDeath() to freeze all updates immediately.
    private bool _isDead;

    // Previous state — used only for debug logging on transitions, avoids spam.
    private SummonerState _lastLoggedState;

    // ── State enum ───────────────────────────────────────────────────────────

    // Drives the summoner's frame-to-frame behavior.
    private enum SummonerState
    {
        Reposition, // Moving toward the player to enter attack range
        Attack,     // Standing still, firing arrow projectiles at the player
        Retreat,    // Backing away because the player is dangerously close
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (movement == null)
            movement = GetComponent<SummonerMovement>();

        if (attackController == null)
            Debug.LogError($"[SummonerBrain] '{name}' missing AttackController.", this);

        if (movement == null)
            Debug.LogError($"[SummonerBrain] '{name}' missing SummonerMovement.", this);
    }

    // Runs only on the server — clients see the result via NetworkTransform.
    private void Update()
    {
        if (_isDead) return;
        if (!IsServerCheck()) return;
        if (attackController == null || movement == null) return;

        // Re-scan every frame to handle multiple players and late-joins.
        _target = PlayerFinder.FindNearest(transform.position, playerTag);

        if (_target == null)
        {
            movement.Stop();
            return;
        }

        UpdateState();
        ExecuteState();
    }

    // ── State machine ────────────────────────────────────────────────────────

    // Evaluates distance to the player and transitions to the appropriate state.
    // Thresholds are designer-configurable — no hysteresis intentionally.
    private void UpdateState()
    {
        float distance = Vector3.Distance(transform.position, _target.position);
        float atkRange = GetAttackRange();

        SummonerState next;

        if (distance < tooCloseThreshold)
        {
            // Player is inside the danger zone — retreat immediately.
            next = SummonerState.Retreat;
        }
        else if (distance <= atkRange)
        {
            // Player is within attack range and not too close — fire in place.
            next = SummonerState.Attack;
        }
        else
        {
            // Player is out of range — close the distance.
            next = SummonerState.Reposition;
        }

        // Log only on state change to prevent console spam.
        if (next != _lastLoggedState)
        {
            _lastLoggedState = next;
            Debug.Log($"[SummonerBrain] '{name}' → {next} " +
                      $"(dist:{distance:F1}, atkRange:{atkRange:F1}, tooClose:{tooCloseThreshold:F1})");
        }

        _state = next;
    }

    // Dispatches to the correct behavior for the current state.
    private void ExecuteState()
    {
        switch (_state)
        {
            case SummonerState.Reposition:
                ExecuteReposition();
                break;

            case SummonerState.Attack:
                ExecuteAttack();
                break;

            case SummonerState.Retreat:
                ExecuteRetreat();
                break;
        }
    }

    // Moves toward the player until within attack range.
    private void ExecuteReposition()
    {
        movement.MoveTo(_target.position);
        movement.FaceToward(_target.position);
    }

    // Stops movement, faces the player, and fires an arrow when the cooldown is ready.
    private void ExecuteAttack()
    {
        movement.Stop();
        movement.FaceToward(_target.position);

        if (!attackController.CanUseAttack()) return;

        AttackDefinition def       = attackController.AttackDefinition;
        Vector3          direction = (_target.position - transform.position).normalized;

        // Pass EffectiveDamage so level scaling from EnemyInitializer is applied.
        var projectile = ProjectileAttackExecutor.Execute(
            transform, direction, def, attackController.EffectiveDamage
        );

        if (projectile != null)
        {
            attackController.MarkAttackUsed();
            Debug.Log($"[SummonerBrain] '{name}' fired arrow at '{_target.name}'.");
        }
    }

    // Backs away from the player toward a point at preferredRange distance.
    // Keeps facing the player while retreating.
    private void ExecuteRetreat()
    {
        Vector3 dirAway            = (transform.position - _target.position).normalized;
        Vector3 retreatDestination = _target.position + dirAway * preferredRange;

        movement.MoveTo(retreatDestination);
        // Summoner keeps looking at the player while retreating — maintains visual threat.
        movement.FaceToward(_target.position);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Returns the attack range from the AttackDefinition.
    // Falls back to preferredRange + 1 if no definition is assigned yet.
    private float GetAttackRange()
    {
        return attackController.AttackDefinition != null
            ? attackController.AttackDefinition.Range
            : preferredRange + 1f;
    }

    // Brain runs on server-only but doesn't extend NetworkBehaviour.
    // Checks via the sibling SummonerMovement (which extends NetworkBehaviour).
    private bool IsServerCheck()
    {
        return movement != null && movement.IsServer;
    }

    // ── IDeathHandler ────────────────────────────────────────────────────────

    // Called by DeathController when this entity's HP reaches zero.
    // Stops all movement and disables the state machine immediately.
    public void OnDeath()
    {
        _isDead = true;
        _target = null;

        movement?.Stop();

        Debug.Log($"[SummonerBrain] '{name}' — AI stopped on death.");
    }
}
