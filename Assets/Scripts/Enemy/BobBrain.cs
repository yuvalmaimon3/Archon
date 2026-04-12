using UnityEngine;

// AI brain for Bob — a stationary artillery enemy that covers the entire map.
//
// Behavior:
//   Bob never moves. He rotates in place to track the nearest player
//   and fires arc-blast shells at that player's position on each attack cycle.
//   No range gate — Bob fires from anywhere on the map.
//   The shell lands with AOE blast damage; the player can dodge by moving out
//   of the landing zone before impact.
//
// Networking:
//   All logic runs server-only (NGO). Clients see rotation via NetworkTransform
//   and the ArcBlastProjectile's own client-side movement.
//   Implements IDeathHandler so targeting stops cleanly on death.
public class BobBrain : MonoBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Attack controller on this GameObject. Auto-resolved in Awake if left empty.")]
    [SerializeField] private AttackController attackController;

    [Tooltip("Stationary movement component. Auto-resolved in Awake if left empty.")]
    [SerializeField] private BombardierMovement movement;

    [Header("Projectile")]
    [Tooltip("The ArcBlastProjectile prefab to launch on each attack.")]
    [SerializeField] private ArcBlastProjectile blastProjectilePrefab;

    [Tooltip("Height of the parabolic arc above the straight-line midpoint.")]
    [Min(0f)]
    [SerializeField] private float arcHeight = 5f;

    [Header("Target")]
    [Tooltip("Unity tag used to locate the player.")]
    [SerializeField] private string playerTag = "Player";

    // ── Private state ────────────────────────────────────────────────────────

    private Transform _target;
    private bool      _isDead;
    private Transform _lastLoggedTarget;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (movement == null)
            movement = GetComponent<BombardierMovement>();

        if (attackController == null)
            Debug.LogError($"[BobBrain] '{name}' missing AttackController.", this);

        if (blastProjectilePrefab == null)
            Debug.LogError($"[BobBrain] '{name}' missing ArcBlastProjectile prefab — " +
                           "assign it in the Inspector.", this);

        if (movement == null)
            Debug.LogError($"[BobBrain] '{name}' missing BombardierMovement.", this);
    }

    // Runs server-only — clients see rotation via NetworkTransform.
    private void Update()
    {
        if (_isDead) return;
        if (!IsServerCheck()) return;
        if (attackController == null || blastProjectilePrefab == null) return;

        _target = PlayerFinder.FindNearest(transform.position, playerTag);

        if (_target != _lastLoggedTarget)
        {
            _lastLoggedTarget = _target;
            if (_target != null)
                Debug.Log($"[BobBrain] '{name}' targeting '{_target.name}'.");
            else
                Debug.Log($"[BobBrain] '{name}' — no players found.");
        }

        if (_target == null) return;

        // Bob covers the entire map — no range gate.
        TryFireBlastShell();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Fires an arc-blast shell at the current target if the cooldown has elapsed.
    // Target position is captured at fire time — the player can dodge the landing.
    private void TryFireBlastShell()
    {
        if (!attackController.CanUseAttack()) return;

        Vector3 targetPos = _target.position;

        ArcBlastProjectile projectile = Object.Instantiate(
            blastProjectilePrefab,
            transform.position,
            Quaternion.identity
        );

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

        // Uses EffectiveDamage so level scaling applies.
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

        Debug.Log($"[BobBrain] '{name}' fired blast shell at '{_target.name}' " +
                  $"(dist:{Vector3.Distance(transform.position, targetPos):F1}, " +
                  $"damage:{attackController.EffectiveDamage}).");
    }

    // Server check delegated to the sibling NetworkBehaviour (BombardierMovement).
    private bool IsServerCheck() => movement != null && movement.IsServer;

    // ── IDeathHandler ────────────────────────────────────────────────────────

    public void OnDeath()
    {
        _isDead = true;
        _target = null;
        _lastLoggedTarget = null;

        movement?.SetDead();

        Debug.Log($"[BobBrain] '{name}' — AI stopped on death.");
    }
}
