using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Central orchestrator for a room's run flow.
//
// NETWORK SPLIT:
//   Server  — drives all round logic (spawn, timer expiry, state transitions, upgrade gating).
//             EnemySpawner and the authoritative RoundTimer run here only.
//   Clients — receive ClientRpcs at each transition so they can update UI and react to events.
//             Each client runs its own local RoundTimer seeded by the Rpc (display only, not authoritative).
//             Gate opens via event fired from the Rpc — no extra network object needed.
//
// NetworkVariables (CurrentRound, State) let late-joining clients immediately know where
// the room is without needing to replay Rpc history. They change at most 3-4 times per room
// so bandwidth cost is negligible.
//
// Flow:
//   OnNetworkSpawn (server) → GenerateRoom → StartRoom
//     Round 1 → Round 2 → Round 3 (final, no timer)
//   OnAllRoundsComplete → upgrade check → OnRoomComplete → gate opens on all clients
[RequireComponent(typeof(RoundTimer))]
[RequireComponent(typeof(EnemySpawner))]
public class RoomManager : NetworkBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Configuration")]
    [Tooltip("Defines the 3 rounds for this room. Assign a RoomConfig asset here.")]
    [SerializeField] private RoomConfig _config;

    [Header("Room Generation")]
    [Tooltip("RoomGenerator in this scene. Manager calls GenerateRoom() and reads bounds from it.")]
    [SerializeField] private RoomGenerator _roomGenerator;

    [Header("Between Rounds")]
    [Tooltip("Seconds to pause between rounds before spawning the next batch.")]
    [SerializeField] [Min(0f)] private float _betweenRoundDelay = 2f;

    [Header("EXP Reward")]
    [Tooltip("EXP granted to each alive player when all rounds are complete. " +
             "If this pushes a player past a level threshold the upgrade dialog appears " +
             "before the gate opens.")]
    [SerializeField] [Min(0)] private int _expRewardPerRoom = 25;

    // ── NetworkVariables ──────────────────────────────────────────────────────

    // Current round number (1-based). Clients read this for UI display.
    // Written only by server; readable by all clients.
    private readonly NetworkVariable<int> _currentRound = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Room state. Clients read this to know when to show upgrade UI, open gate, etc.
    private readonly NetworkVariable<RoomState> _state = new(
        RoomState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Component references (server-side only for spawner/timer) ─────────────

    private EnemySpawner _spawner;   // called only on server
    private RoundTimer   _timer;     // authoritative on server; display-only on clients

    // ── Events (fired locally on every client via ClientRpcs) ─────────────────

    // Fired once when the first round is about to start.
    public event Action OnRoomStarted;

    // Fired at the start of each round. (roundIndex: 0-based, totalRounds)
    public event Action<int, int> OnRoundStarted;

    // Fired when a round ends.
    public event Action<int, RoundEndReason> OnRoundEnded;

    // Fired after the final round ends, before the upgrade check.
    public event Action OnAllRoundsComplete;

    // Fired if an upgrade must be chosen before the gate opens.
    // Subscribe to show the upgrade selection UI.
    public event Action OnUpgradeRequired;

    // Fired when the room is fully complete. Gate subscribes here to open.
    public event Action OnRoomComplete;

    // ── Public read-only state ────────────────────────────────────────────────

    public RoomState State        => _state.Value;
    public int       CurrentRound => _currentRound.Value;
    public int       TotalRounds  => _config != null ? _config.RoundCount : 0;

    // ── Private server-side state ─────────────────────────────────────────────

    // How many players still need to pick an upgrade before the gate opens.
    // Incremented by SetUpgradeRequired() for each player who leveled up.
    // Decremented by NotifyUpgradeChosen() when a player finishes their selection.
    // Gate opens when this reaches 0 after all rounds are done.
    private int _pendingUpgradeChoices;

    // Prevents re-completion if something triggers FinishAllRounds twice.
    private bool _allRoundsFinished;

    // ── Unity / NGO lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        _spawner = GetComponent<EnemySpawner>();
        _timer   = GetComponent<RoundTimer>();
    }

    // OnNetworkSpawn is the right entry point for NGO objects.
    // Only the server drives room generation and the run flow.
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (_config == null)
        {
            Debug.LogError("[RoomManager] No RoomConfig assigned — cannot start.");
            return;
        }

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

    // Called once per player who leveled up during this room.
    // Increments the pending counter so the gate waits for that many upgrade picks.
    // Must be called before _allRoundsFinished is set (i.e. from GrantExpToAlivePlayers).
    public void SetUpgradeRequired()
    {
        if (!IsServer) return;

        if (_allRoundsFinished)
        {
            Debug.LogWarning("[RoomManager] SetUpgradeRequired called after rounds finished — too late.");
            return;
        }

        _pendingUpgradeChoices++;
        Debug.Log($"[RoomManager] Upgrade pending — {_pendingUpgradeChoices} player(s) need to choose.");
    }

    // Called by PlayerUpgradeHandler (via ServerRpc) once a player picks their upgrade.
    // When the last pending choice is resolved the room completes and the gate opens.
    public void NotifyUpgradeChosen()
    {
        if (!IsServer) return;

        if (_pendingUpgradeChoices <= 0)
        {
            Debug.LogWarning("[RoomManager] NotifyUpgradeChosen called but no upgrade was pending.");
            return;
        }

        _pendingUpgradeChoices--;
        Debug.Log($"[RoomManager] Upgrade chosen — {_pendingUpgradeChoices} remaining.");

        if (_pendingUpgradeChoices == 0 && _allRoundsFinished && _state.Value == RoomState.WaitingForUpgrade)
            CompleteRoom();
    }

    // ── Server: room flow ──────────────────────────────────────────────────────

    // Starts the room — notifies all clients via Rpc, then begins Round 1 on the server.
    private void StartRoom()
    {
        // IsServer already guaranteed by the only caller (OnNetworkSpawn).
        Debug.Log($"[RoomManager] Room starting — {TotalRounds} rounds.");
        NotifyRoomStartedClientRpc();
        StartCoroutine(RunRoom());
    }

    // Runs all rounds sequentially on the server.
    private IEnumerator RunRoom()
    {
        for (int i = 0; i < _config.RoundCount; i++)
            yield return StartCoroutine(RunRound(i));

        FinishAllRounds();
    }

    // Runs one round: notify clients → spawn enemies → wait for end condition → notify clients.
    private IEnumerator RunRound(int roundIndex)
    {
        var  roundConfig  = _config.Rounds[roundIndex];
        bool isFinalRound = (roundIndex == _config.RoundCount - 1);
        float timerDuration = isFinalRound ? 0f : roundConfig.TimerDuration;

        _currentRound.Value = roundIndex + 1;
        _state.Value        = RoomState.RoundActive;

        Debug.Log($"[RoomManager] Round {_currentRound.Value}/{TotalRounds} — " +
                  $"final:{isFinalRound}, timer:{(timerDuration > 0f ? timerDuration + "s" : "none")}");

        // Tell all clients: new round started + timer duration (so they can run a local display timer).
        NotifyRoundStartedClientRpc(roundIndex, TotalRounds, timerDuration);

        bool allDefeated  = false;
        bool timerExpired = false;

        void OnDefeated() => allDefeated = true;
        _spawner.OnAllEnemiesDefeated += OnDefeated;
        _spawner.SpawnRound(roundConfig);

        if (!isFinalRound && timerDuration > 0f)
        {
            // Non-final round: ends when enemies die OR timer runs out (whichever first).
            void OnTimerEnd() => timerExpired = true;
            _timer.OnTimerExpired += OnTimerEnd;
            _timer.Begin(timerDuration);

            yield return new WaitUntil(() => allDefeated || timerExpired);

            _timer.Cancel();
            _timer.OnTimerExpired -= OnTimerEnd;
        }
        else
        {
            // Final round (or explicitly timer-less): only ends when all enemies are dead.
            yield return new WaitUntil(() => allDefeated);
        }

        _spawner.OnAllEnemiesDefeated -= OnDefeated;

        var reason   = allDefeated ? RoundEndReason.AllEnemiesDefeated : RoundEndReason.TimerExpired;
        _state.Value = RoomState.BetweenRounds;

        Debug.Log($"[RoomManager] Round {_currentRound.Value} ended — {reason}.");
        NotifyRoundEndedClientRpc(roundIndex, reason);

        // Brief pause before the next round (skip after the final one).
        if (!isFinalRound && _betweenRoundDelay > 0f)
            yield return new WaitForSeconds(_betweenRoundDelay);
    }

    // Called once all rounds finish on the server.
    private void FinishAllRounds()
    {
        // Grant EXP BEFORE setting _allRoundsFinished so SetUpgradeRequired() is still valid.
        // If any alive player levels up, _pendingUpgradeChoices is incremented here.
        GrantExpToAlivePlayers();

        _allRoundsFinished = true;

        Debug.Log("[RoomManager] All rounds complete.");
        NotifyAllRoundsCompleteClientRpc();

        if (_pendingUpgradeChoices > 0)
        {
            _state.Value = RoomState.WaitingForUpgrade;
            Debug.Log($"[RoomManager] Waiting for {_pendingUpgradeChoices} upgrade selection(s).");
            NotifyUpgradeRequiredClientRpc();
        }
        else
        {
            CompleteRoom();
        }
    }

    // Grants _expRewardPerRoom EXP to every alive player in the scene.
    // If a player's EXP crosses a level threshold, PlayerLevelSystem fires OnLevelUp
    // which causes PlayerUpgradeHandler to show the upgrade dialog and eventually call
    // NotifyUpgradeChosen(). We increment _pendingUpgradeChoices here for each level-up
    // so the gate knows how many players still need to choose.
    private void GrantExpToAlivePlayers()
    {
        if (_expRewardPerRoom <= 0) return;

        var players = FindObjectsByType<PlayerLevelSystem>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            // Only alive players receive EXP — a player could die on the same frame
            // as the last enemy kill, so we must check IsDead explicitly.
            if (player.TryGetComponent<Health>(out var health) && health.IsDead)
            {
                Debug.Log($"[RoomManager] Skipping EXP for dead player '{player.name}'.");
                continue;
            }

            int levelBefore = player.CurrentLevel;

            player.AddExperience(_expRewardPerRoom);

            // If the player leveled up, flag that they need to pick an upgrade.
            // PlayerUpgradeHandler.HandleLevelUp will call NotifyUpgradeChosen() via ServerRpc
            // after the player makes their selection.
            if (player.CurrentLevel > levelBefore)
                SetUpgradeRequired();
        }

        Debug.Log($"[RoomManager] Granted {_expRewardPerRoom} EXP to alive players. " +
                  $"Pending upgrade choices: {_pendingUpgradeChoices}");
    }

    // Final step — marks room complete and notifies all clients (gate opens, transition begins).
    private void CompleteRoom()
    {
        _state.Value = RoomState.Complete;
        Debug.Log("[RoomManager] Room complete — gate opening on all clients.");
        NotifyRoomCompleteClientRpc();
    }

    // ── ClientRpcs — fire events on every client (including host) ─────────────

    // Each Rpc fires the corresponding local event so UI and other systems react uniformly
    // on every machine without any additional polling.

    [ClientRpc]
    private void NotifyRoomStartedClientRpc()
    {
        OnRoomStarted?.Invoke();
    }

    // timerDuration > 0 means clients should show a countdown; 0 means final round (no timer UI).
    [ClientRpc]
    private void NotifyRoundStartedClientRpc(int roundIndex, int totalRounds, float timerDuration)
    {
        // Clients run a local timer purely for display — not authoritative.
        // The server's timer drives actual round logic.
        if (!IsServer && timerDuration > 0f)
            _timer.Begin(timerDuration);

        OnRoundStarted?.Invoke(roundIndex, totalRounds);
    }

    [ClientRpc]
    private void NotifyRoundEndedClientRpc(int roundIndex, RoundEndReason reason)
    {
        // Stop the display timer on clients if it's still running.
        if (!IsServer)
            _timer.Cancel();

        OnRoundEnded?.Invoke(roundIndex, reason);
    }

    [ClientRpc]
    private void NotifyAllRoundsCompleteClientRpc()
    {
        OnAllRoundsComplete?.Invoke();
    }

    [ClientRpc]
    private void NotifyUpgradeRequiredClientRpc()
    {
        OnUpgradeRequired?.Invoke();
    }

    [ClientRpc]
    private void NotifyRoomCompleteClientRpc()
    {
        // Gate and any other completion listeners react here on all clients.
        OnRoomComplete?.Invoke();
    }
}
