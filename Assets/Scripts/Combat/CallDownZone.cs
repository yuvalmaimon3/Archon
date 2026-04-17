using System.Collections;
using UnityEngine;

// Spawned at a strike location by CallDownAttackExecutor.
// Shows a warning indicator for warnDuration seconds, then detonates with AOE damage.
//
// Networking: MonoBehaviour — local-only for now.
// When multiplayer is added: convert to NetworkBehaviour, gate Strike() to IsServer,
// and sync spawn via ClientRpc (same pattern as ArcBlastProjectile).
public class CallDownZone : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Child object shown during the warning phase. Scaled at runtime to match AOE radius.")]
    [SerializeField] private GameObject warningIndicator;

    // ── Runtime state ────────────────────────────────────────────────────────

    private int                _damage;
    private GameObject         _source;
    private float              _aoeRadius;
    private string             _targetTag;
    private ElementApplication _elementApplication;

    // ── Public API ───────────────────────────────────────────────────────────

    // Call immediately after Instantiate to arm the zone.
    public void Initialize(int damage, GameObject source, float warnDuration, float aoeRadius,
                           string targetTag, ElementApplication elementApplication)
    {
        _damage             = damage;
        _source             = source;
        _aoeRadius          = aoeRadius;
        _targetTag          = targetTag;
        _elementApplication = elementApplication;

        if (warningIndicator != null)
        {
            // Scale the indicator diameter to match the damage radius so it visually matches the hit area.
            warningIndicator.transform.localScale = Vector3.one * (aoeRadius * 2f);
            warningIndicator.SetActive(true);
        }

        Debug.Log($"[CallDownZone] Warning started at {transform.position} — " +
                  $"striking in {warnDuration:F1}s, radius:{aoeRadius}, tag:'{targetTag}'.");

        StartCoroutine(StrikeAfterDelay(warnDuration));
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private IEnumerator StrikeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Strike();
    }

    // Applies AOE damage to all matching IDamageable targets within aoeRadius, then destroys self.
    private void Strike()
    {
        if (warningIndicator != null)
            warningIndicator.SetActive(false);

        Collider[] nearby = Physics.OverlapSphere(transform.position, _aoeRadius);
        int hitCount = 0;

        foreach (Collider col in nearby)
        {
            if (!col.CompareTag(_targetTag)) continue;

            // Skip the source object and its children
            if (_source != null &&
                (col.gameObject == _source || col.transform.IsChildOf(_source.transform)))
                continue;

            if (!col.TryGetComponent<IDamageable>(out var damageable)) continue;

            Vector3 hitDir = (col.transform.position - transform.position).normalized;

            var damageInfo = new DamageInfo(
                amount:             _damage,
                source:             _source,
                hitPoint:           col.ClosestPoint(transform.position),
                hitDirection:       hitDir,
                elementApplication: _elementApplication
            );

            damageable.TakeDamage(damageInfo);
            hitCount++;
        }

        Debug.Log($"[CallDownZone] Strike at {transform.position} — hit {hitCount} target(s) within radius {_aoeRadius}.");

        Destroy(gameObject);
    }
}
