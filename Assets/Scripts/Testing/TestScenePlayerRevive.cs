using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Scene-specific auto-revive logic for test scenes.
// Place this on any GameObject in the test scene (e.g. the NetworkManager GO).
//
// How it works:
//   - Waits for a NetworkObject with the "Player" tag to exist (handles late NGO spawn).
//   - Subscribes to that player's DeathController.OnDied event.
//   - On death: waits reviveDelay seconds, then calls TriggerRevive() + Health.Revive()
//     to fully restore the player (scripts, colliders, renderers, HP).
//
// Why a dedicated script instead of an #if or scene-name check:
//   This script only lives in test scenes — no production code is touched.
//   If you want auto-revive in any scene, just drop this GO into it.
//
// Server-only: revive is authoritative. Clients receive the state via NGO sync.
public class TestScenePlayerRevive : MonoBehaviour
{
    [Header("Revive Settings")]
    [Tooltip("Seconds to wait after the player dies before reviving them.")]
    [Min(0f)]
    [SerializeField] private float reviveDelay = 2f;

    // Health.Revive() always restores to MaxHealth — no partial revive on base API.

    // Cached references — resolved once we find the player after NGO spawn.
    private DeathController _deathController;
    private Health          _health;
    private bool            _subscribed;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        // Player might not be spawned yet — poll until found.
        StartCoroutine(WaitForPlayer());
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayer();
    }

    // ── Private logic ────────────────────────────────────────────────────────

    // Polls every frame until the Player-tagged object is found and ready.
    // Needed because TestSceneAutoHost spawns the player after a small delay.
    private IEnumerator WaitForPlayer()
    {
        while (_deathController == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _deathController = playerGO.GetComponent<DeathController>();
                _health          = playerGO.GetComponent<Health>();
            }
            yield return null; // try again next frame
        }

        SubscribeToPlayer();
        Debug.Log($"[TestScenePlayerRevive] Found player '{_deathController.name}' — auto-revive active (delay:{reviveDelay}s).");
    }

    private void SubscribeToPlayer()
    {
        if (_subscribed || _deathController == null) return;
        _deathController.OnDied += OnPlayerDied;
        _subscribed = true;
    }

    private void UnsubscribeFromPlayer()
    {
        if (!_subscribed || _deathController == null) return;
        _deathController.OnDied -= OnPlayerDied;
        _subscribed = false;
    }

    // Called when the player's DeathController fires OnDied.
    private void OnPlayerDied()
    {
        // Revive is server-authoritative — only run on the server/host.
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        Debug.Log($"[TestScenePlayerRevive] Player died — reviving in {reviveDelay}s.");
        StartCoroutine(ReviveAfterDelay());
    }

    private IEnumerator ReviveAfterDelay()
    {
        yield return new WaitForSeconds(reviveDelay);

        if (_deathController == null || _health == null)
        {
            Debug.LogWarning("[TestScenePlayerRevive] Player references lost before revive — skipping.");
            yield break;
        }

        // Restore HP first so Health.IsDead clears before TriggerRevive re-enables components.
        _health.Revive();

        // Re-enable scripts, renderers, colliders and restore tag.
        _deathController.TriggerRevive();

        Debug.Log($"[TestScenePlayerRevive] Player revived at full HP.");
    }
}
