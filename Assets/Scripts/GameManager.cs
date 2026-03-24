using System;
using UnityEngine;

/// <summary>
/// Central manager that tracks whether the game is running or idle.
/// All systems (movement, spawning, etc.) should check IsGameStarted
/// before allowing gameplay to happen.
///
/// Singleton pattern: only one instance exists at a time.
/// </summary>
public class GameManager : MonoBehaviour
{
    // Static reference so any script can access it with GameManager.Instance
    public static GameManager Instance { get; private set; }

    // True once the player presses Start, false at game over / stop
    public bool IsGameStarted { get; private set; }

    // Other scripts can subscribe to these events to react when the game starts or stops.
    // Example: enemies could start/stop spawning based on these events.
    public event Action OnGameStarted;
    public event Action OnGameStopped;

    private void Awake()
    {
        // Singleton guard: if another GameManager already exists, destroy this one
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Starts the game. Called by the Start button in DebugMenuUI.
    /// Sets IsGameStarted = true, which unlocks player movement and other systems.
    /// </summary>
    public void StartGame()
    {
        if (IsGameStarted) return; // already running, do nothing
        IsGameStarted = true;
        OnGameStarted?.Invoke(); // notify all listeners (e.g. enemy spawner)
        Debug.Log("[GameManager] Game started.");
    }

    /// <summary>
    /// Stops the game. Called by the Stop button or when the session ends.
    /// Freezes player movement and other gameplay systems.
    /// </summary>
    public void StopGame()
    {
        if (!IsGameStarted) return; // already stopped, do nothing
        IsGameStarted = false;
        OnGameStopped?.Invoke(); // notify all listeners
        Debug.Log("[GameManager] Game stopped.");
    }
}
