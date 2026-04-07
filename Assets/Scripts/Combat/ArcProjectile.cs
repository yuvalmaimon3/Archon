using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A projectile that travels in a parabolic arc from its spawn point to a captured
/// target position. Designed for artillery-style attacks (e.g. Bombarder enemy) that
/// arc over walls and obstacles.
///
/// Arc formula:
///   position = Lerp(start, end, t) + up * arcHeight * sin(t * PI)
///   where t goes 0 → 1 over travelTime seconds.
///
/// Damage is applied either by OnTriggerEnter (player walked into the path)
/// or by an OverlapSphere at the landing position when the arc completes.
///
/// ── Networked mode (multiplayer) ──
///   Spawned as NetworkObject by the server; all clients receive InitializeClientRpc
///   which sets trajectory data and starts arc movement.
///
/// ── Standalone mode (offline / test) ──
///   Spawned with Instantiate; initialized via Initialize().
///   Lifetime managed locally with Destroy().
///
/// Setup requirements on the prefab:
///   - A Collider set to Is Trigger
///   - A Rigidbody (forced kinematic in Awake)
///   - A NetworkObject component (required for multiplayer)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ArcProjectile : NetworkBehaviour
{
    [Header("Arc Settings")]
    [Tooltip("Time in seconds for the projectile to complete its arc from start to target.")]
    [Min(0.1f)]
    [SerializeField] private float travelTime = 2.0f;

    [Tooltip("Splash radius around the landing position to check for damageable targets " +
             "when the arc completes. Handles cases where the player moved slightly.")]
    [Min(0f)]
    [SerializeField] private float landingSplashRadius = 1.2f;

    // Inspector-only display — written at runtime for debugging.
    [Tooltip("Read-only at runtime. Target tag set by the spawner.")]
    [SerializeField] private string _targetTag;

    // ── Runtime state ────────────────────────────────────────────────────────

    private int                _damage;
    private GameObject         _source;
    private Vector3            _startPosition;
    private Vector3            _targetPosition;
    private float              _arcHeight;
    private ElementApplication _elementApplication;

    private bool _isInitialized;
    // Guard against double-processing if two triggers fire before despawn finishes.
    private bool _hasHit;
    // True in a networked session — routes destruction through NGO Despawn.
    private bool _isNetworked;

    // Elapsed time since initialization — drives arc interpolation.
    private float _elapsed;

    // Reference to lifetime coroutine so it can be cancelled precisely on impact.
    private Coroutine _lifetimeCoroutine;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Kinematic so we drive position manually; OnTriggerEnter still fires correctly.
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    private void Update()
    {
        if (!_isInitialized || _hasHit) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / travelTime);

        // Parabolic arc: horizontal position is linearly interpolated,
        // vertical offset is a sine curve that peaks at the midpoint.
        Vector3 flatPos = Vector3.Lerp(_startPosition, _targetPosition, t);
        float   height  = _arcHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = flatPos + Vector3.up * height;

        // Tilt the projectile to follow the arc tangent for realistic visuals.
        TiltAlongArc(t);

        // Arc complete — trigger landing impact.
        if (t >= 1f)
            TriggerImpact(null);
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Server schedules the lifetime despawn here; it propagates to all clients automatically.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            _lifetimeCoroutine = StartCoroutine(LifetimeExpiry(travelTime + 0.5f));
    }

    // ── Networked initialization ─────────────────────────────────────────────

    /// <summary>
    /// Initializes this arc projectile on every connected client after the server spawns it.
    /// Call this immediately after NetworkObject.Spawn() on the server side.
    /// </summary>
    [ClientRpc]
    public void InitializeClientRpc(int damage, NetworkObjectReference sourceRef,
                                    Vector3 startPosition, Vector3 targetPosition, float arcHeight,
                                    ElementType elementType, float elementStrength, string targetTag)
    {
        _isNetworked      = true;
        _source           = sourceRef.TryGet(out var netObj) ? netObj.gameObject : null;
        _damage           = damage;
        _startPosition    = startPosition;
        _targetPosition   = targetPosition;
        _arcHeight        = arcHeight;
        _elementApplication = new ElementApplication(elementType, elementStrength, _source);
        _targetTag        = targetTag;
        _elapsed          = 0f;
        _isInitialized    = true;

        transform.position = startPosition;

        Debug.Log($"[ArcProjectile] Initialized (networked) — damage:{damage}, " +
                  $"arcHeight:{arcHeight}, travelTime:{travelTime}s");
    }

    // ── Standalone initialization ─────────────────────────────────────────────

    /// <summary>
    /// Initializes the arc projectile for offline / test use.
    /// Do NOT call this when a multiplayer session is active — use InitializeClientRpc instead.
    /// </summary>
    public void Initialize(int damage, GameObject source,
                           Vector3 startPosition, Vector3 targetPosition,
                           float arcHeight, ElementApplication elementApplication, string targetTag)
    {
        _isNetworked        = false;
        _source             = source;
        _damage             = damage;
        _startPosition      = startPosition;
        _targetPosition     = targetPosition;
        _arcHeight          = arcHeight;
        _elementApplication = elementApplication;
        _targetTag          = targetTag;
        _elapsed            = 0f;
        _isInitialized      = true;

        transform.position = startPosition;

        // Standalone: self-destruct after the arc completes with a small buffer.
        Destroy(gameObject, travelTime + 0.5f);

        Debug.Log($"[ArcProjectile] Initialized (standalone) — damage:{damage}, " +
                  $"arcHeight:{arcHeight}, travelTime:{travelTime}s");
    }

    // ── Collision ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        // In networked mode only the server applies damage.
        if (_isNetworked && !IsServer) return;
        if (!_isInitialized || _hasHit) return;

        // Ignore other arc projectiles and regular projectiles (no friendly fire between shots).
        if (other.TryGetComponent<ArcProjectile>(out _)) return;
        if (other.TryGetComponent<Projectile>(out _)) return;

        // Ignore the firing source and its children (enemy body colliders).
        if (_source != null &&
            (other.gameObject == _source || other.transform.IsChildOf(_source.transform)))
            return;

        // Arc projectiles fly overhead — only explode on the intended target tag.
        // Walls and terrain are ignored so the shot travels over obstacles.
        if (!other.CompareTag(_targetTag)) return;

        TriggerImpact(other);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Rotates the projectile to face along the arc tangent for accurate visual tilt.
    // Uses a short look-ahead step to estimate the current travel direction.
    private void TiltAlongArc(float t)
    {
        const float lookAheadDelta = 0.05f;
        float tNext = Mathf.Clamp01((t * travelTime + lookAheadDelta) / travelTime);

        Vector3 nextFlat = Vector3.Lerp(_startPosition, _targetPosition, tNext);
        float   nextH    = _arcHeight * Mathf.Sin(tNext * Mathf.PI);
        Vector3 nextPos  = nextFlat + Vector3.up * nextH;

        Vector3 dir = (nextPos - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // Applies damage at the impact point — either a direct trigger hit or the landing splash.
    // hitCollider is null when called from the arc-complete landing check.
    private void TriggerImpact(Collider hitCollider)
    {
        if (_hasHit) return;
        _hasHit = true;

        if (hitCollider != null)
        {
            // Direct hit: the player physically moved into the arc's path.
            ApplyDamage(hitCollider);
            Debug.Log($"[ArcProjectile] Direct hit on '{hitCollider.gameObject.name}'.");
        }
        else
        {
            // Arc complete: splash-check at the captured landing position.
            // This catches the player even if they did not move into the collider directly.
            Collider[] nearby = Physics.OverlapSphere(_targetPosition, landingSplashRadius);
            foreach (Collider col in nearby)
            {
                if (!col.CompareTag(_targetTag)) continue;
                ApplyDamage(col);
                Debug.Log($"[ArcProjectile] Landing hit on '{col.gameObject.name}'.");
                break; // One hit per shot — no chain damage.
            }

            if (nearby.Length == 0 || !HasHitTarget(nearby))
                Debug.Log($"[ArcProjectile] Landed at {_targetPosition} — no target in splash radius.");
        }

        DestroyProjectile();
    }

    // Returns true if at least one of the nearby colliders carries the target tag.
    private bool HasHitTarget(Collider[] colliders)
    {
        foreach (Collider col in colliders)
            if (col.CompareTag(_targetTag)) return true;
        return false;
    }

    // Builds a DamageInfo and sends it to the target's IDamageable.
    private void ApplyDamage(Collider col)
    {
        if (!col.TryGetComponent<IDamageable>(out var damageable))
        {
            Debug.LogWarning($"[ArcProjectile] Hit '{col.gameObject.name}' (tag:'{_targetTag}') " +
                             $"but it has no IDamageable component.");
            return;
        }

        Vector3 hitDirection = (_targetPosition - _startPosition).normalized;
        var damageInfo = new DamageInfo(
            amount:             _damage,
            source:             _source,
            hitPoint:           col.ClosestPoint(transform.position),
            hitDirection:       hitDirection,
            elementApplication: _elementApplication
        );

        damageable.TakeDamage(damageInfo);
        Debug.Log($"[ArcProjectile] Dealt {_damage} damage to '{col.gameObject.name}'.");
    }

    // Server-side timer — despawns on all clients when the arc lifetime expires.
    private IEnumerator LifetimeExpiry(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsSpawned)
            NetworkObject.Despawn(destroy: true);
    }

    // Routes destruction through NGO (networked) or Unity (standalone).
    private void DestroyProjectile()
    {
        if (_isNetworked)
        {
            if (_lifetimeCoroutine != null)
                StopCoroutine(_lifetimeCoroutine);

            if (IsSpawned)
                NetworkObject.Despawn(destroy: true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
