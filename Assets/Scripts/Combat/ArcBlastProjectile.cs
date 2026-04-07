using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Parabolic arc projectile that detonates on landing with AOE blast damage.
// Damages ALL IDamageable targets within blastRadius at the impact point — unlike
// ArcProjectile which hits a single target. Used by the Artillery enemy.
//
// Arc formula (same as ArcProjectile):
//   position = Lerp(start, end, t) + up * arcHeight * sin(t * PI)
//   where t goes 0 → 1 over travelTime seconds.
//
// Networking:
//   Networked: spawned as NetworkObject by the server; clients receive InitializeClientRpc.
//   Standalone: spawned with Instantiate; initialized via Initialize() for offline/test.
//
// Prefab requirements:
//   - Collider set to Is Trigger
//   - Rigidbody (forced kinematic in Awake)
//   - NetworkObject component (for multiplayer)
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ArcBlastProjectile : NetworkBehaviour
{
    [Header("Arc Settings")]
    [Tooltip("Time in seconds the shell takes to travel from launch to landing.")]
    [Min(0.1f)]
    [SerializeField] private float travelTime = 1.8f;

    [Header("Blast Settings")]
    [Tooltip("Radius of the AOE explosion on landing. All IDamageable targets within this range are hit.")]
    [Min(0f)]
    [SerializeField] private float blastRadius = 2.5f;

    // Inspector-only display for debugging — written at runtime.
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
    // Guard: prevents double-blast if Update and lifetime expiry overlap.
    private bool _hasExploded;
    private bool _isNetworked;

    // Elapsed time drives arc interpolation.
    private float _elapsed;

    // Lifetime coroutine reference — cancelled precisely when the shell lands.
    private Coroutine _lifetimeCoroutine;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Kinematic: we drive position manually via arc formula each Update.
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    private void Update()
    {
        if (!_isInitialized || _hasExploded) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / travelTime);

        // Horizontal position linearly interpolated; vertical follows a sine curve
        // that peaks at the midpoint and returns to zero at landing.
        Vector3 flatPos = Vector3.Lerp(_startPosition, _targetPosition, t);
        float   height  = _arcHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = flatPos + Vector3.up * height;

        // Tilt the shell to follow the arc tangent for realistic visual rotation.
        TiltAlongArc(t);

        // Arc complete — detonate at the landing position.
        if (t >= 1f)
            Detonate();
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    // Server schedules the lifetime failsafe — propagates despawn to all clients.
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            _lifetimeCoroutine = StartCoroutine(LifetimeExpiry(travelTime + 0.5f));
    }

    // ── Networked initialization ─────────────────────────────────────────────

    // Called on all clients immediately after the server spawns this object.
    // Must be called right after NetworkObject.Spawn() on the server.
    [ClientRpc]
    public void InitializeClientRpc(int damage, NetworkObjectReference sourceRef,
                                    Vector3 startPosition, Vector3 targetPosition, float arcHeight,
                                    ElementType elementType, float elementStrength, string targetTag)
    {
        _isNetworked        = true;
        _source             = sourceRef.TryGet(out var netObj) ? netObj.gameObject : null;
        _damage             = damage;
        _startPosition      = startPosition;
        _targetPosition     = targetPosition;
        _arcHeight          = arcHeight;
        _elementApplication = new ElementApplication(elementType, elementStrength, _source);
        _targetTag          = targetTag;
        _elapsed            = 0f;
        _isInitialized      = true;

        transform.position = startPosition;

        Debug.Log($"[ArcBlastProjectile] Initialized (networked) — damage:{damage}, " +
                  $"blastRadius:{blastRadius}, arcHeight:{arcHeight}, travelTime:{travelTime}s");
    }

    // ── Standalone initialization ────────────────────────────────────────────

    // Initializes the projectile for offline / test use.
    // Do NOT call this in a live multiplayer session — use InitializeClientRpc instead.
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

        // Standalone: self-destruct after arc completes with a small buffer.
        Destroy(gameObject, travelTime + 0.5f);

        Debug.Log($"[ArcBlastProjectile] Initialized (standalone) — damage:{damage}, " +
                  $"blastRadius:{blastRadius}, arcHeight:{arcHeight}, travelTime:{travelTime}s");
    }

    // ── Blast logic ──────────────────────────────────────────────────────────

    // Explodes at the captured target position, dealing damage to every IDamageable
    // with the target tag within blastRadius. Unlike ArcProjectile, no early exit
    // after the first hit — all targets in range take damage.
    private void Detonate()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        // In networked mode only the server applies damage to avoid duplicate hits.
        if (_isNetworked && !IsServer)
        {
            DestroyProjectile();
            return;
        }

        Collider[] nearby = Physics.OverlapSphere(_targetPosition, blastRadius);
        int hitCount = 0;

        foreach (Collider col in nearby)
        {
            // Only damage the intended faction (Player or Enemy).
            if (!col.CompareTag(_targetTag)) continue;

            // Skip the firing enemy and its children.
            if (_source != null &&
                (col.gameObject == _source || col.transform.IsChildOf(_source.transform)))
                continue;

            if (!col.TryGetComponent<IDamageable>(out var damageable)) continue;

            // Blast direction radiates outward from the impact center.
            Vector3 hitDir = (col.transform.position - _targetPosition).normalized;

            var damageInfo = new DamageInfo(
                amount:             _damage,
                source:             _source,
                hitPoint:           col.ClosestPoint(_targetPosition),
                hitDirection:       hitDir,
                elementApplication: _elementApplication
            );

            damageable.TakeDamage(damageInfo);
            hitCount++;
        }

        Debug.Log($"[ArcBlastProjectile] Blast at {_targetPosition} — hit {hitCount} target(s) " +
                  $"within radius {blastRadius}.");

        DestroyProjectile();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Rotates the shell to follow the arc tangent — makes the shell visually tilt
    // nose-down as it descends toward the landing zone.
    private void TiltAlongArc(float t)
    {
        const float lookAheadDelta = 0.05f;
        float tNext = Mathf.Clamp01((t * travelTime + lookAheadDelta) / travelTime);

        Vector3 nextFlat = Vector3.Lerp(_startPosition, _targetPosition, tNext);
        float   nextH    = _arcHeight * Mathf.Sin(tNext * Mathf.PI);
        Vector3 nextPos  = nextFlat + Vector3.up * nextH;

        Vector3 dir = nextPos - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // Failsafe: despawn on all clients if the shell somehow never triggers Detonate.
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
