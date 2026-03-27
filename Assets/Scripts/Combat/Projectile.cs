using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Moves in a straight line, deals damage to objects with a matching target tag,
/// and destroys itself on any solid surface hit (walls, floors, obstacles, etc.).
/// Deals damage only if the tagged target also implements IDamageable.
///
/// ── Networked mode (multiplayer) ──
///   Spawned as a NetworkObject by the server via PlayerCombatBrain.SpawnProjectileServerRpc.
///   All clients receive InitializeClientRpc which sets the trajectory data and starts movement.
///   Movement is simulated locally on every client (deterministic: same direction + speed = same result).
///   Only the server applies damage and despawns on hit or lifetime expiry — single source of truth.
///
/// ── Standalone mode (offline / test) ──
///   Spawned locally with Object.Instantiate and initialized via Initialize().
///   Lifetime and hit destruction are managed with Destroy() on the local machine.
///
/// Setup requirements on the prefab:
///   - A Collider set to Is Trigger
///   - A Rigidbody (forced kinematic in Awake)
///   - A NetworkObject component (required for multiplayer replication)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Projectile : NetworkBehaviour
{
    [Header("Projectile")]
    [Tooltip("Seconds before the projectile self-destructs if it hits nothing.")]
    [Min(0.1f)]
    [SerializeField] private float lifetime = 5f;

    // Visible in the Inspector for live debugging — written at initialization, do not set on the prefab.
    [Tooltip("Read-only at runtime. Set by the spawner at initialization time.")]
    [SerializeField] private string _targetTag;

    // ── Runtime state ────────────────────────────────────────────────────────

    private int                _damage;
    private GameObject         _source;
    private Vector3            _direction;
    private float              _speed;
    private ElementApplication _elementApplication;

    private bool _isInitialized;

    // Prevents double-processing if two triggers fire before Despawn completes.
    private bool _hasHit;

    // True when this projectile was spawned as a NetworkObject (multiplayer session).
    // Controls whether destruction goes through NGO (Despawn) or Unity (Destroy).
    private bool _isNetworked;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Kinematic: we move the projectile manually via Transform.Translate.
        // OnTriggerEnter still fires correctly with a kinematic Rigidbody.
        var rb         = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // Straight-line movement in world space — identical on every client.
        transform.Translate(_direction * _speed * Time.deltaTime, Space.World);
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on all machines when the NetworkObject is spawned.
    /// The server schedules the lifetime despawn here — the despawn propagates to all clients.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Only the server owns the lifetime timer so there is a single authority.
        if (IsServer)
            Invoke(nameof(ServerDespawn), lifetime);
    }

    // ── Networked initialization ─────────────────────────────────────────────

    /// <summary>
    /// Initializes the projectile on all connected clients after the server spawns it.
    /// Called by the server immediately after NetworkObject.Spawn().
    /// Each client starts simulating the same deterministic trajectory.
    /// </summary>
    [ClientRpc]
    public void InitializeClientRpc(int damage, NetworkObjectReference sourceRef,
                                     Vector3 direction, float speed,
                                     ElementType elementType, float elementStrength,
                                     string targetTag)
    {
        _isNetworked        = true;
        _source             = sourceRef.TryGet(out var netObj) ? netObj.gameObject : null;
        _damage             = damage;
        _direction          = direction.normalized;
        _speed              = speed;
        _elementApplication = new ElementApplication(elementType, elementStrength, _source);
        _targetTag          = targetTag;
        _isInitialized      = true;

        Debug.Log($"[Projectile] Initialized (networked) — damage:{damage}, speed:{speed}, " +
                  $"element:{elementType}, lifetime:{lifetime}s");
    }

    // ── Standalone initialization ─────────────────────────────────────────────

    /// <summary>
    /// Initializes the projectile for non-networked (offline / editor) use.
    /// Do NOT call this when a multiplayer session is active — use InitializeClientRpc instead.
    /// Manages lifetime locally via Destroy().
    /// </summary>
    public void Initialize(int damage, GameObject source, Vector3 direction, float speed,
                           ElementApplication elementApplication, string targetTag)
    {
        _isNetworked        = false;
        _damage             = damage;
        _source             = source;
        _direction          = direction.normalized;
        _speed              = speed;
        _elementApplication = elementApplication;
        _targetTag          = targetTag;
        _isInitialized      = true;

        // Standalone mode: manage lifetime with a local Destroy.
        Destroy(gameObject, lifetime);

        Debug.Log($"[Projectile] Initialized (standalone) — damage:{damage}, speed:{speed}, " +
                  $"element:{elementApplication.Element}, lifetime:{lifetime}s");
    }

    // ── Collision ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        // In networked mode: only the server processes hits.
        // Non-server clients keep simulating the visual trajectory until the server despawns the object.
        if (_isNetworked && !IsServer) return;

        if (!_isInitialized || _hasHit) return;

        // Ignore the source and its children (e.g. player body-part colliders).
        if (_source != null &&
            (other.gameObject == _source || other.transform.IsChildOf(_source.transform)))
            return;

        _hasHit = true;

        // Deal damage only when the hit object carries the expected target tag.
        if (other.CompareTag(_targetTag))
        {
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
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
                          $"(element:{_elementApplication.Element}).");
            }
            else
            {
                // Tagged as target but no IDamageable — log so testers can catch missing components.
                Debug.LogWarning($"[Projectile] Hit '{other.gameObject.name}' (tag:'{_targetTag}') " +
                                 $"but it has no IDamageable component.");
            }
        }
        else
        {
            // Hit a hard surface (wall, floor, obstacle) — destroy without dealing damage.
            Debug.Log($"[Projectile] Hit solid object '{other.gameObject.name}' (tag:'{other.tag}') — destroyed.");
        }

        DestroyProjectile();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Server-side lifetime expiry — despawns on all clients when the lifetime timer fires.
    /// </summary>
    private void ServerDespawn()
    {
        if (IsSpawned)
            NetworkObject.Despawn(destroy: true);
    }

    /// <summary>
    /// Routes destruction through NGO (networked) or Unity (standalone) as appropriate.
    /// </summary>
    private void DestroyProjectile()
    {
        if (_isNetworked)
        {
            // Cancel the scheduled lifetime despawn — this hit-despawn takes over.
            CancelInvoke(nameof(ServerDespawn));

            if (IsSpawned)
                NetworkObject.Despawn(destroy: true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
