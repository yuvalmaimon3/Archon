using System.Collections;
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
///   - A Rigidbody (non-kinematic, gravity off — moved via linearVelocity)
///   - A NetworkObject component (required for multiplayer replication)
///
/// NOTE: The Rigidbody must NOT be kinematic. Unity 6 does not fire OnTriggerEnter
/// between two kinematic Rigidbodies. Keeping the projectile non-kinematic (gravity off,
/// velocity-driven) ensures trigger events fire against both kinematic enemies (Wraith)
/// and non-kinematic enemies (Goblin, Slime, etc.).
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

    [Tooltip("Element this projectile carries. Read-only at runtime — set by the spawner.")]
    [SerializeField]
    #pragma warning disable IDE0052 // Inspector-only display field — read by Unity, not by C# code
    private ElementType _displayElementType;
    #pragma warning restore IDE0052

    // ── Runtime state ────────────────────────────────────────────────────────

    private int                _damage;
    private GameObject         _source;
    private Vector3            _direction;
    private float              _speed;
    private ElementApplication _elementApplication;
    private bool               _isCritical;

    private bool _isInitialized;

    // Prevents double-processing if two triggers fire before Despawn completes.
    private bool _hasHit;

    // True when this projectile was spawned as a NetworkObject (multiplayer session).
    // Controls whether destruction goes through NGO (Despawn) or Unity (Destroy).
    private bool _isNetworked;

    // Reference to the lifetime coroutine so it can be cancelled precisely on hit.
    // Avoids CancelInvoke which would cancel ALL pending invocations on this object.
    private Coroutine _lifetimeCoroutine;

    private Rigidbody _rb;

    // ── Split config (Shotgun upgrade) ───────────────────────────────────────
    // Set server-side by PlayerCombatBrain after spawning. Never set on split
    // projectiles themselves — prevents infinite recursive splitting.

    private bool             _splitOnHit;
    private float            _splitAngle;
    private AttackDefinition _splitAttackDef;

    // The enemy collider this split ball was born inside — ignored until exited.
    private Collider _spawnIgnoreCollider;

    // ── Life steal (Life Steal upgrade) ──────────────────────────────────────
    // Server-side only. When true, hitting an enemy heals the source player
    // by _lifeStealFraction of their max HP.

    private bool  _hasLifeSteal;
    private float _lifeStealFraction;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Non-kinematic so OnTriggerEnter fires against kinematic enemies (e.g. Wraith).
        // Unity 6 does not generate trigger events between two kinematic Rigidbodies.
        // Gravity is off — velocity set at initialization drives the straight-line flight.
        _rb.isKinematic = false;
        _rb.useGravity  = false;
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // No Update movement needed — velocity is set once at initialization and physics carries it.

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on all machines when the NetworkObject is spawned.
    /// The server schedules the lifetime despawn here — the despawn propagates to all clients.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Mark networked immediately on all machines so the OnTriggerEnter client-guard
        // is active from the moment the object appears — before InitializeClientRpc arrives.
        _isNetworked = true;

        if (IsServer)
            _lifetimeCoroutine = StartCoroutine(LifetimeExpiry());
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
                                     string targetTag, bool isCritical = false)
    {
        _isNetworked          = true; // also set in OnNetworkSpawn, but keep here for safety
        _source               = sourceRef.TryGet(out var netObj) ? netObj.gameObject : null;
        _damage               = damage;
        _direction            = direction.normalized;
        _speed                = speed;
        _elementApplication   = new ElementApplication(elementType, elementStrength, _source);
        _targetTag            = targetTag;
        _isCritical           = isCritical;
        _displayElementType   = elementType;
        _isInitialized        = true;

        // Force non-kinematic — NGO 2.x may set kinematic on non-owner clients.
        _rb.isKinematic    = false;
        _rb.linearVelocity = _direction * _speed;

        Debug.Log($"[Projectile] InitializeClientRpc — IsServer:{IsServer} IsClient:{IsClient} " +
                  $"vel:{_rb.linearVelocity} damage:{damage} speed:{speed} " +
                  $"element:{elementType} crit:{isCritical}");
    }

    // ── Standalone initialization ─────────────────────────────────────────────

    /// <summary>
    /// Initializes the projectile for non-networked (offline / editor) use.
    /// Do NOT call this when a multiplayer session is active — use InitializeClientRpc instead.
    /// Manages lifetime locally via Destroy().
    /// </summary>
    public void Initialize(int damage, GameObject source, Vector3 direction, float speed,
                           ElementApplication elementApplication, string targetTag,
                           bool isCritical = false)
    {
        _isNetworked          = false;
        _damage               = damage;
        _source               = source;
        _direction            = direction.normalized;
        _speed                = speed;
        _elementApplication   = elementApplication;
        _targetTag            = targetTag;
        _isCritical           = isCritical;
        _displayElementType   = elementApplication.Element;
        _isInitialized        = true;

        // Drive movement via physics velocity — non-kinematic, gravity off.
        _rb.linearVelocity = _direction * _speed;

        // Standalone mode: manage lifetime with a local Destroy.
        Destroy(gameObject, lifetime);

        Debug.Log($"[Projectile] Initialized (standalone) — damage:{damage}, speed:{speed}, " +
                  $"element:{elementApplication.Element}, lifetime:{lifetime}s, crit:{isCritical}");
    }

    // ── Split API ────────────────────────────────────────────────────────────

    public void ConfigureLifeSteal(float fraction)
    {
        _hasLifeSteal      = true;
        _lifeStealFraction = fraction;
    }

    // Called on split projectiles so they skip the enemy they were born inside.
    // Cleared in OnTriggerExit — once the ball exits, bounces can re-hit freely.
    public void SetSpawnIgnoreCollider(Collider col) => _spawnIgnoreCollider = col;

    // Called server-side by PlayerCombatBrain after spawning this projectile.
    // Marks the projectile to spawn 3 split children on the next enemy hit.
    // Split projectiles never get ConfigureSplit called — no recursive splitting.
    public void ConfigureSplit(float angleDeg, AttackDefinition attackDef)
    {
        _splitOnHit    = true;
        _splitAngle    = angleDeg;
        _splitAttackDef = attackDef;
        Debug.Log($"[Projectile] Split configured — angle:{angleDeg}°, def:'{attackDef?.AttackId}'.");
    }

    // ── Collision ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        // In networked mode: only the server processes hits.
        // Non-server clients keep simulating the visual trajectory until the server despawns the object.
        if (_isNetworked && !IsServer) return;

        if (!_isInitialized || _hasHit) return;

        // Projectiles pass through each other — no friendly/enemy fire between projectiles.
        if (other.TryGetComponent<Projectile>(out _)) return;

        // Ignore the source and its children (e.g. player body-part colliders).
        if (_source != null &&
            (other.gameObject == _source || other.transform.IsChildOf(_source.transform)))
            return;

        // Skip the enemy this split ball spawned inside — cleared once we exit its collider.
        if (_spawnIgnoreCollider != null && other == _spawnIgnoreCollider) return;

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
                    elementApplication: _elementApplication,
                    isCritical:         _isCritical
                );
                damageable.TakeDamage(damageInfo);
                Debug.Log($"[Projectile] Hit '{other.gameObject.name}' for {_damage} damage " +
                          $"(element:{_elementApplication.Element}, crit:{_isCritical}).");

                // Life steal — heal the source player on the server (no RPC needed, Health is server-authoritative).
                if (_hasLifeSteal && _source != null)
                {
                    var sourceHealth = _source.GetComponent<Health>();
                    if (sourceHealth != null)
                    {
                        int healAmount = Mathf.Max(1, Mathf.RoundToInt(sourceHealth.MaxHealth * _lifeStealFraction));
                        sourceHealth.Heal(healAmount);
                        Debug.Log($"[Projectile] Life steal — healed '{_source.name}' for {healAmount} HP.");
                    }
                }
            }
            else
            {
                // Tagged as target but no IDamageable — log so testers can catch missing components.
                Debug.LogWarning($"[Projectile] Hit '{other.gameObject.name}' (tag:'{_targetTag}') " +
                                 $"but it has no IDamageable component.");
            }

            // Shotgun upgrade: spawn 3 split projectiles on enemy hit (server only).
            // Split projectiles never split again — ConfigureSplit is intentionally not called on them.
            if (_splitOnHit && _splitAttackDef != null)
                SpawnSplitProjectiles(other);
        }
        else
        {
            // Hit a hard surface (wall, floor, obstacle) — destroy without dealing damage.
            Debug.Log($"[Projectile] Hit solid object '{other.gameObject.name}' (tag:'{other.tag}') — destroyed.");
        }

        DestroyProjectile();
    }

    private void OnTriggerExit(Collider other)
    {
        // Once the split ball physically leaves the source enemy, allow re-hits (e.g. after a bounce).
        if (other == _spawnIgnoreCollider)
            _spawnIgnoreCollider = null;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Spawns 3 split projectiles at this projectile's position:
    //   - one continuing in the original direction
    //   - one rotated +_splitAngle degrees around the Y axis
    //   - one rotated -_splitAngle degrees around the Y axis
    // Server-only — called from OnTriggerEnter (which is already guarded by !IsServer return).
    private void SpawnSplitProjectiles(Collider hitCollider)
    {
        Vector3[] splitDirs =
        {
            _direction,
            Quaternion.AngleAxis( _splitAngle, Vector3.up) * _direction,
            Quaternion.AngleAxis(-_splitAngle, Vector3.up) * _direction,
        };

        // Resolve the source as a NetworkObjectReference for InitializeClientRpc.
        NetworkObjectReference sourceRef = default;
        if (_source != null && _source.TryGetComponent<NetworkObject>(out var srcNet))
            sourceRef = new NetworkObjectReference(srcNet);

        Debug.Log($"[Projectile] Shotgun split — spawning {splitDirs.Length} projectiles at ±{_splitAngle}°.");

        foreach (var dir in splitDirs)
        {
            var splitProjectile = Object.Instantiate(
                _splitAttackDef.ProjectilePrefab,
                transform.position,
                Quaternion.LookRotation(dir)
            );

            // Prevent the split ball from immediately re-hitting the enemy it was born inside.
            splitProjectile.SetSpawnIgnoreCollider(hitCollider);

            if (_isNetworked)
            {
                // Networked: spawn through NGO and broadcast trajectory to all clients.
                splitProjectile.NetworkObject.Spawn();
                splitProjectile.InitializeClientRpc(
                    damage:          _damage,
                    sourceRef:       sourceRef,
                    direction:       dir,
                    speed:           _splitAttackDef.ProjectileSpeed,
                    elementType:     _splitAttackDef.ElementType,
                    elementStrength: _splitAttackDef.ElementStrength,
                    targetTag:       _splitAttackDef.ProjectileTargetTag,
                    isCritical:      _isCritical   // inherit crit from parent
                );
            }
            else
            {
                // Standalone / offline: initialize locally.
                splitProjectile.Initialize(
                    damage:             _damage,
                    source:             _source,
                    direction:          dir,
                    speed:              _splitAttackDef.ProjectileSpeed,
                    elementApplication: _elementApplication,
                    targetTag:          _splitAttackDef.ProjectileTargetTag,
                    isCritical:         _isCritical   // inherit crit from parent
                );
            }
        }
    }

    // Server-side lifetime expiry coroutine — despawns on all clients when time runs out.
    private IEnumerator LifetimeExpiry()
    {
        yield return new WaitForSeconds(lifetime);
        if (IsSpawned)
            NetworkObject.Despawn(destroy: true);
    }

    // Routes destruction through NGO (networked) or Unity (standalone) as appropriate.
    private void DestroyProjectile()
    {
        if (_isNetworked)
        {
            // Cancel the lifetime coroutine precisely — only stops this specific timer.
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
