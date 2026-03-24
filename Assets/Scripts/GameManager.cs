using System;
using UnityEngine;

/// <summary>
/// Tracks the global game state (idle → running).
/// Other systems check IsGameStarted before allowing gameplay.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsGameStarted { get; private set; }

    public event Action OnGameStarted;
    public event Action OnGameStopped;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartGame()
    {
        if (IsGameStarted) return;
        IsGameStarted = true;
        OnGameStarted?.Invoke();
        Debug.Log("[GameManager] Game started.");
    }

    public void StopGame()
    {
        if (!IsGameStarted) return;
        IsGameStarted = false;
        OnGameStopped?.Invoke();
        Debug.Log("[GameManager] Game stopped.");
    }
}
