using System;
using System.Collections;
using UnityEngine;

// Central orchestrator for a room's run flow.
//
// Flow:
//   StartRoom()
//     ↓
//   Round 1 → Round 2 → Round 3 (final, no timer)
//     ↓ (each round: spawn enemies, run timer if not final, wait for end condition)
//   OnAllRoundsComplete fires
//     ↓ (if upgrade pending)
//   OnUpgradeRequired fires → waits for NotifyUpgradeChosen()
//     ↓
//   OnRoomComplete fires → gate opens
//
// Upgrade integration:
//   Any system (e.g. PlayerLevelSystem) can call SetUpgradeRequired() to signal that
//   the player leveled up and must choose an upgrade before the gate opens.
//   The upgrade UI then calls NotifyUpgradeChosen() when the player picks.
[RequireComponent(typeof(RoundTimer))]
[RequireComponent(typeof(EnemySpawner))]
public class RoomManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Configuration")]
    [Tooltip("Defines the 3 rounds for this room (enemy types, counts, timer durations).")]
    [SerializeField] private RoomConfig _config;

    [Header("Room Generation")]
    [Tooltip("RoomGenerator in this scene. Manager calls GenerateRoom() and reads bounds from it.")]
    [SerializeField] private RoomGenerator _roomGenerator;

    [Header("Between Rounds")]
    [Tooltip("Seconds to wait between rounds before spawning the next batch.")]
    [SerializeField] [Min(0f)] private float _betweenRoundDelay = 2f;

    // ── Component references (auto-fetched in Awake) ──────────────────────────

    private EnemySpawner _spawner;
    private RoundTimer   _timer;

    // ── Events ────────────────────────────────────────────────────────────────

    // Fired once when the first round is about to start.
    public event Action OnRoomStarted;

    // Fired at the start of each round. (roundIndex: 0-based, totalRounds: e.g. 3)
    public event Action<int, int> OnRoundStarted;

    // Fired when a round ends. Carries why it ended (enemies defeated / timer expired).
    public event Action<int, RoundEndReason> OnRoundEnded;

    // Fired after the last round ends, before the upgrade check.
    public event Action OnAllRoundsComplete;

    // Fired if an upgrade must be chosen before the gate opens.
    // Subscribe to show the upgrade selection UI.
    public event Action OnUpgradeRequired;

    // Fired when the room is fully complete. Subscribe to open the gate.
    public event Action OnRoomComplete;

    // ── Read-only state ───────────────────────────────────────────────────────

    public RoomState State           { get; private set; } = RoomState.Idle;
    public int       CurrentRound    { get; private set; } = 0; // 1-based for readability in logs/UI
    public int       TotalRounds     => _config != null ? _config.RoundCount : 0;

    // ── Private state ─────────────────────────────────────────────────────────

    // Set by SetUpgradeRequired() when the player leveled up mid-room.
    private bool _upgradeRequired;

    // Set true once all rounds finish — prevents re-completion.
    private bool _allRoundsFinished;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _spawner = GetComponent<EnemySpawner>();
        _timer   = GetComponent<RoundTimer>();
    }

    // Auto-starts the room on Play so the test scene works immediately.
    // In production, call StartRoom() from a room transition system instead.
    private void Start()
    {
        // Generate the room geometry first, then pass bounds to the spawner.
        if (_roomGenerator != null)
        {
            _roomGenerator.GenerateRoom();
            _spawner.SetSpawnBounds(_roomGenerator.RoomWidth, _roomGenerator.RoomLength);
        }
        else
        {
            Debug.LogWarning("[RoomManager] No RoomGenerator assigned — using default spawn bounds.");
        }

        StartRoom();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Starts the room run — fires OnRoomStarted, then begins Round 1.
    // Only valid when State == Idle.
    [ContextMenu("Start Room")]
    public void StartRoom()
    {
        if (State != RoomState.Idle)
        {
            Debug.LogWarning($"[RoomManager] StartRoom called in state {State} — ignored.");
            return;
        }

        if (_config == null)
        {
            Debug.LogError("[RoomManager] No RoomConfig assigned — cannot start.");
            return;
        }

        Debug.Log($"[RoomManager] Room starting — {TotalRounds} rounds.");
        OnRoomStarted?.Invoke();
        StartCoroutine(RunRoom());
    }

    // Called by an external system (e.g. PlayerLevelSystem) when the player levels up.
    // Must be called BEFORE all rounds finish to take effect for this room.
    // If called after all rounds finish, it is ignored (gate opens on its own).
    public void SetUpgradeRequired()
    {
        if (_allRoundsFinished)
        {
            Debug.LogWarning("[RoomManager] SetUpgradeRequired called after rounds finished — too late.");
            return;
        }
        _upgradeRequired = true;
        Debug.Log("[RoomManager] Upgrade flagged — gate will wait for upgrade choice.");
    }

    // Called by the upgrade UI once the player has selected an upgrade.
    // If all rounds are already done and we were waiting, this completes the room.
    public void NotifyUpgradeChosen()
    {
        if (!_upgradeRequired)
        {
            Debug.LogWarning("[RoomManager] NotifyUpgradeChosen called but no upgrade was pending.");
            return;
        }

        _upgradeRequired = false;
        Debug.Log("[RoomManager] Upgrade chosen.");

        if (_allRoundsFinished && State == RoomState.WaitingForUpgrade)
            CompleteRoom();
    }

    // ── Room flow ─────────────────────────────────────────────────────────────

    // Runs all rounds sequentially, then finalises the room.
    private IEnumerator RunRoom()
    {
        for (int i = 0; i < _config.RoundCount; i++)
            yield return StartCoroutine(RunRound(i));

        FinishAllRounds();
    }

    // Runs one round: spawns enemies, optionally runs a timer, waits for the end condition.
    private IEnumerator RunRound(int roundIndex)
    {
        var  roundConfig  = _config.Rounds[roundIndex];
        bool isFinalRound = (roundIndex == _config.RoundCount - 1);

        State        = RoomState.RoundActive;
        CurrentRound = roundIndex + 1;

        Debug.Log($"[RoomManager] Round {CurrentRound}/{TotalRounds} — " +
                  $"final:{isFinalRound}, timer:{(isFinalRound ? "none" : roundConfig.TimerDuration + "s")}");

        OnRoundStarted?.Invoke(roundIndex, TotalRounds);

        // Flags set by event callbacks within this round's scope.
        bool allDefeated  = false;
        bool timerExpired = false;

        void OnDefeated() => allDefeated = true;
        _spawner.OnAllEnemiesDefeated += OnDefeated;

        _spawner.SpawnRound(roundConfig);

        if (!isFinalRound && !roundConfig.IsTimerless)
        {
            // Non-final round: ends when enemies die OR timer runs out.
            void OnTimerEnd() => timerExpired = true;
            _timer.OnTimerExpired += OnTimerEnd;
            _timer.Begin(roundConfig.TimerDuration);

            yield return new WaitUntil(() => allDefeated || timerExpired);

            _timer.Cancel();
            _timer.OnTimerExpired -= OnTimerEnd;
        }
        else
        {
            // Final round (or explicitly timer-less): only ends when all enemies die.
            yield return new WaitUntil(() => allDefeated);
        }

        _spawner.OnAllEnemiesDefeated -= OnDefeated;

        var reason = allDefeated ? RoundEndReason.AllEnemiesDefeated : RoundEndReason.TimerExpired;
        State = RoomState.BetweenRounds;

        Debug.Log($"[RoomManager] Round {CurrentRound} ended — {reason}.");
        OnRoundEnded?.Invoke(roundIndex, reason);

        // Pause between rounds (skip after the final round).
        if (!isFinalRound && _betweenRoundDelay > 0f)
            yield return new WaitForSeconds(_betweenRoundDelay);
    }

    // Called once all rounds are done.
    private void FinishAllRounds()
    {
        _allRoundsFinished = true;

        Debug.Log("[RoomManager] All rounds complete.");
        OnAllRoundsComplete?.Invoke();

        if (_upgradeRequired)
        {
            // Block here until the player picks an upgrade — NotifyUpgradeChosen() will finish the room.
            State = RoomState.WaitingForUpgrade;
            Debug.Log("[RoomManager] Waiting for upgrade selection.");
            OnUpgradeRequired?.Invoke();
        }
        else
        {
            CompleteRoom();
        }
    }

    // Final step — marks room complete and notifies all listeners (gate, transition system, etc.).
    private void CompleteRoom()
    {
        State = RoomState.Complete;
        Debug.Log("[RoomManager] Room complete — gate opening.");
        OnRoomComplete?.Invoke();
    }
}
