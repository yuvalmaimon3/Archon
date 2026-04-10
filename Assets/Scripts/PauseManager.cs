using Unity.Netcode;
using UnityEngine;

// Manages local game pause — freezes time via Time.timeScale.
// Local-only MonoBehaviour: pause state is per-client, no network sync needed.
// Pause is blocked in co-op (more than 1 client connected).
// Other systems (UI, audio) subscribe to OnPauseChanged to react.
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    // Fired whenever pause state changes. True = paused, False = resumed.
    public static event System.Action<bool> OnPauseChanged;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TryTogglePause();
    }

    private void TryTogglePause()
    {
        // Pause is single-player only — co-op sessions are not pausable.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count > 1)
        {
            Debug.Log("[PauseManager] Pause blocked — co-op session active.");
            return;
        }

        SetPause(!IsPaused);
    }

    public void SetPause(bool pause)
    {
        if (IsPaused == pause) return;

        IsPaused = pause;
        Time.timeScale = pause ? 0f : 1f;

        Debug.Log($"[PauseManager] Game {(pause ? "paused" : "resumed")}.");
        OnPauseChanged?.Invoke(IsPaused);
    }
}
