using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Applies gameplay effects to this enemy based on the currently active element.
// Server-only — movement and Health are server-authoritative.
//
// Ice    : -50% move speed while active; restored on clear.
// Fire   : 10% of attacker's effective damage per second (DoT).
// Electro: every 2s, freezes self-movement for 0.5s (knockback still applies).
// Water  : no gameplay effect.
[RequireComponent(typeof(ElementStatusController))]
[RequireComponent(typeof(EnemyMovementBase))]
[RequireComponent(typeof(Health))]
public class ElementStatusEffects : NetworkBehaviour
{
    [Header("Fire")]
    [SerializeField] private float fireTickInterval = 1f;

    [Header("Electro")]
    [SerializeField] private float electroInterval     = 2f;
    [SerializeField] private float electroStunDuration = 0.5f;

    private ElementStatusController _elementStatus;
    private EnemyMovementBase       _movement;
    private Health                  _health;

    private Coroutine   _activeCoroutine;
    private ElementType _activeElement;   // tracks what effect is currently running

    // Speed cached before ice slow so we can restore it exactly.
    private float _preIceSpeed;

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        _elementStatus = GetComponent<ElementStatusController>();
        _movement      = GetComponent<EnemyMovementBase>();
        _health        = GetComponent<Health>();

        _elementStatus.OnElementChanged += OnElementChanged;

        Debug.Log($"[ElementStatusEffects] '{name}' ready.");
    }

    public override void OnNetworkDespawn()
    {
        if (_elementStatus != null)
            _elementStatus.OnElementChanged -= OnElementChanged;

        StopActiveEffect();
    }

    // ── Element handler ──────────────────────────────────────────────────────

    private void OnElementChanged(ElementType element, float strength)
    {
        StopActiveEffect();
        _activeElement = element;

        switch (element)
        {
            case ElementType.Ice:
                ApplyIceSlow();
                break;

            case ElementType.Fire:
                _activeCoroutine = StartCoroutine(FireDoTCoroutine());
                break;

            case ElementType.Lightning:
                _activeCoroutine = StartCoroutine(ElectroShockCoroutine());
                break;

            // Water: no gameplay effect
        }
    }

    // ── Ice ──────────────────────────────────────────────────────────────────

    private void ApplyIceSlow()
    {
        _preIceSpeed = _movement.ScaledSpeed;
        _movement.SetMoveSpeed(_preIceSpeed * 0.5f);
        Debug.Log($"[ElementStatusEffects] '{name}' Ice — speed slowed to {_preIceSpeed * 0.5f:F2}.");
    }

    private void ClearIceSlow()
    {
        _movement.SetMoveSpeed(_preIceSpeed);
        Debug.Log($"[ElementStatusEffects] '{name}' Ice cleared — speed restored to {_preIceSpeed:F2}.");
    }

    // ── Fire DoT ─────────────────────────────────────────────────────────────

    private IEnumerator FireDoTCoroutine()
    {
        Debug.Log($"[ElementStatusEffects] '{name}' Fire DoT started.");

        while (true)
        {
            yield return new WaitForSeconds(fireTickInterval);

            if (_health.IsDead) yield break;

            // Read attacker's current attack damage at tick time (responds to upgrades).
            GameObject source = _elementStatus.LastApplicationSource;
            AttackController attackCtrl = source != null
                ? source.GetComponent<AttackController>()
                : null;

            int baseDamage = attackCtrl != null ? attackCtrl.EffectiveDamage : 0;
            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 0.1f));

            // No element on tick — avoids triggering reactions from DoT.
            var info = new DamageInfo(tickDamage, source, transform.position, Vector3.zero);
            _health.TakeDamage(info);

            Debug.Log($"[ElementStatusEffects] '{name}' Fire tick — {tickDamage} dmg (base:{baseDamage}).");
        }
    }

    // ── Electro shock ────────────────────────────────────────────────────────

    private IEnumerator ElectroShockCoroutine()
    {
        Debug.Log($"[ElementStatusEffects] '{name}' Electro shock started.");

        while (true)
        {
            yield return new WaitForSeconds(electroInterval);

            if (_health.IsDead) yield break;

            // Freeze self-movement; knockback can still be applied via KnockbackHandler.
            _movement.SuspendMovement();
            Debug.Log($"[ElementStatusEffects] '{name}' Electro — movement suspended.");

            yield return new WaitForSeconds(electroStunDuration);

            _movement.ResumeMovement();
            Debug.Log($"[ElementStatusEffects] '{name}' Electro — movement resumed.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void StopActiveEffect()
    {
        // Restore ice speed if it was running.
        if (_activeElement == ElementType.Ice)
            ClearIceSlow();

        // Resolve any in-progress electro stun before switching elements.
        if (_movement != null && _movement.IsMovementSuspended)
            _movement.ResumeMovement();

        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        _activeElement = ElementType.None;
    }
}
