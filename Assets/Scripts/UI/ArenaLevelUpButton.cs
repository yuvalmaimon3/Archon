using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Test button: grants enough EXP to immediately level up the local player.
// Finds PlayerLevelSystem lazily on first click so it works after NGO spawns the player.
//
// Network: MonoBehaviour — UI lives locally; EXP grant goes through the ServerRpc.
public class ArenaLevelUpButton : MonoBehaviour
{
    [SerializeField] private Button _button;

    private PlayerLevelSystem _levelSystem;

    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        _button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (!TryGetLevelSystem()) return;

        if (_levelSystem.IsMaxLevel)
        {
            Debug.Log("[ArenaLevelUpButton] Player is already at max level.");
            return;
        }

        // Grant exactly what's needed to cross the current level threshold
        int needed = _levelSystem.ExpRequired - _levelSystem.CurrentExp;
        if (needed <= 0) needed = _levelSystem.ExpRequired; // safety fallback

        _levelSystem.AddExperienceServerRpc(needed);
        Debug.Log($"[ArenaLevelUpButton] Granted {needed} EXP — level-up triggered.");
    }

    private bool TryGetLevelSystem()
    {
        if (_levelSystem != null) return true;

        _levelSystem = FindFirstObjectByType<PlayerLevelSystem>();
        if (_levelSystem == null)
        {
            Debug.LogWarning("[ArenaLevelUpButton] PlayerLevelSystem not found. Is the host running?");
            return false;
        }
        return true;
    }
}
