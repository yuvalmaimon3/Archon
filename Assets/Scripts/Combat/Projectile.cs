using UnityEngine;

/// <summary>
/// Moves in a straight line and deals damage to the first valid IDamageable it hits.
/// Generic — works for both player and enemy projectiles.
///
/// Setup requirements on the prefab:
///   - A Collider set to Is Trigger
///   - A Rigidbody (Projectile forces it kinematic in Awake)
///
/// Lifetime and stats are set via Initialize() at spawn time.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile")]
    [Tooltip("Seconds before the projectile destroys itself if it hits nothing.")]
    [Min(0.1f)]
    [SerializeField] private float lifetime = 5f;

    // ── Runtime state ────────────────────────────────────────────────────────

    private int                _damage;
    private GameObject         _source;
    private Vector3            _direction;
    private float              _speed;
    private ElementApplication _elementApplication;

    private bool _isInitialized;

    // Prevents double-damage if two trigger overlaps fire before Destroy completes
    private bool _hasHit;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Kinematic: we move the projectile manually; physics must not interfere.
        // OnTriggerEnter still fires correctly with a kinematic Rigidbody.
        var rb           = GetComponent<Rigidbody>();
        rb.isKinematic   = true;
        rb.useGravity    = false;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // Straight-line movement in world space
        transform.Translate(_direction * _speed * Time.deltaTime, Space.World);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sets all runtime data and activates the projectile.
    /// Must be called once immediately after spawning.
    /// </summary>
    /// <param name="damage">Damage to deal on hit.</param>
    /// <param name="source">The GameObject that fired this projectile — ignored on collision.</param>
    /// <param name="direction">World-space travel direction (will be normalized).</param>
    /// <param name="speed">Units per second.</param>
    /// <param name="elementApplication">Elemental data forwarded to the target's ElementStatusController.</param>
    public void Initialize(int damage, GameObject source, Vector3 direction, float speed,
                           ElementApplication elementApplication)
    {
        _damage             = damage;
        _source             = source;
        _direction          = direction.normalized;
        _speed              = speed;
        _elementApplication = elementApplication;
        _isInitialized      = true;

        // Schedule lifetime destruction — no countdown needed in Update
        Destroy(gameObject, lifetime);

        Debug.Log($"[Projectile] Initialized — damage: {damage}, speed: {speed}, " +
                  $"element: {elementApplication.Element}, lifetime: {lifetime}s");
    }

    // ── Collision ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized || _hasHit) return;

        // Ignore the source — also handles multi-collider objects where the
        // hitbox is a child of the source (e.g. player body parts)
        if (_source != null &&
            (other.gameObject == _source || other.transform.IsChildOf(_source.transform)))
            return;

        // Only damage objects that implement IDamageable — no tag checks
        if (!other.TryGetComponent<IDamageable>(out var damageable)) return;

        _hasHit = true;

        // ClosestPoint gives the surface contact position for accurate VFX and knockback
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        var damageInfo = new DamageInfo(
            amount:             _damage,
            source:             _source,
            hitPoint:           hitPoint,
            hitDirection:       _direction,
            elementApplication: _elementApplication
        );

        damageable.TakeDamage(damageInfo);

        Debug.Log($"[Projectile] Hit '{other.gameObject.name}' for {_damage} damage " +
                  $"(element: {_elementApplication.Element}).");

        Destroy(gameObject);
    }
}
