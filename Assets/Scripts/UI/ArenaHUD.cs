using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

// Arena test HUD: shows current Level + EXP, and flashes "LEVEL UP!" on level-up.
// Polls for PlayerLevelSystem after the host spawns the player.
//
// Network: MonoBehaviour — pure UI, reads NetworkVariables via events.
public class ArenaHUD : MonoBehaviour
{
    [SerializeField] private Text _levelExpText;    // "Level 1  |  EXP 0 / 100"
    [SerializeField] private Text _levelUpFlash;    // "LEVEL UP!" — fades out

    [SerializeField] private float _flashDuration = 2f;

    private PlayerLevelSystem _levelSystem;

    private void Start()
    {
        if (_levelUpFlash != null)
            _levelUpFlash.enabled = false;

        StartCoroutine(WaitForPlayer());
    }

    private void OnDestroy()
    {
        if (_levelSystem != null)
        {
            _levelSystem.OnLevelUp    -= OnLevelUp;
            _levelSystem.OnExpChanged -= OnExpChanged;
        }
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    // Wait until NGO spawns the player, then subscribe to its events.
    private IEnumerator WaitForPlayer()
    {
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

        yield return new WaitUntil(() =>
            FindFirstObjectByType<PlayerLevelSystem>() != null);

        _levelSystem = FindFirstObjectByType<PlayerLevelSystem>();
        _levelSystem.OnLevelUp    += OnLevelUp;
        _levelSystem.OnExpChanged += OnExpChanged;

        // Populate immediately with current state
        UpdateText(_levelSystem.CurrentLevel, _levelSystem.CurrentExp, _levelSystem.ExpRequired);
        Debug.Log("[ArenaHUD] Connected to PlayerLevelSystem.");
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnExpChanged(int currentExp, int expRequired)
    {
        UpdateText(_levelSystem.CurrentLevel, currentExp, expRequired);
    }

    private void OnLevelUp(int newLevel)
    {
        UpdateText(newLevel, _levelSystem.CurrentExp, _levelSystem.ExpRequired);
        StartCoroutine(ShowLevelUpFlash());
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void UpdateText(int level, int exp, int required)
    {
        if (_levelExpText == null) return;
        string expStr = required == int.MaxValue ? "MAX" : $"{exp} / {required}";
        _levelExpText.text = $"Level {level}  |  EXP  {expStr}";
    }

    private IEnumerator ShowLevelUpFlash()
    {
        if (_levelUpFlash == null) yield break;

        _levelUpFlash.enabled = true;
        _levelUpFlash.color = Color.white;

        float elapsed = 0f;
        while (elapsed < _flashDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / _flashDuration);
            _levelUpFlash.color = new Color(1f, 0.9f, 0.2f, alpha); // gold fade
            elapsed += Time.deltaTime;
            yield return null;
        }

        _levelUpFlash.enabled = false;
    }
}
