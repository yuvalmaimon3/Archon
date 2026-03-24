using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Manages the multiplayer session lifecycle using Unity Relay.
///
/// How it works:
///   - The HOST calls HostSessionAsync() which creates a "relay allocation" on Unity's servers
///     and gets back a short JOIN CODE (e.g. "ABC123").
///   - The HOST shares that code with friends (e.g. shown on screen).
///   - CLIENTS call JoinSessionAsync(code) to connect to the host through Unity's relay servers.
///   - No port forwarding or IP addresses needed — Unity's relay acts as a middleman.
///
/// Max players: MaxConnections clients + 1 host = 4 total.
/// </summary>
public class SessionManager : MonoBehaviour
{
    // Static reference so any script can call SessionManager.Instance.HostSessionAsync() etc.
    public static SessionManager Instance { get; private set; }

    // Maximum number of CLIENTS (not counting the host). Total players = MaxConnections + 1
    public const int MaxConnections = 3;

    // The 6-character code that other players enter to join this session (host only)
    public string JoinCode { get; private set; }

    // Shortcut to check if the local player is currently the host
    public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

    // Events that other scripts can subscribe to for UI updates or game logic
    public event Action<string> OnJoinCodeReceived; // fires when host gets the join code
    public event Action         OnSessionStarted;   // fires when session is fully started
    public event Action         OnSessionFailed;    // fires if something goes wrong

    private void Awake()
    {
        // Singleton guard: only one SessionManager allowed
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // Host — creates the session and waits for clients
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a Relay allocation on Unity's servers, gets a join code,
    /// and starts this player as the host (server + client in one).
    /// </summary>
    public async Task HostSessionAsync()
    {
        // Make sure Unity Gaming Services finished signing in before proceeding
        if (!NetworkBootstrapper.Instance.IsReady)
        {
            Debug.LogWarning("[SessionManager] UGS not ready yet.");
            return;
        }

        try
        {
            // Step 1: Reserve a slot on Unity's Relay servers for MaxConnections clients
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

            // Step 2: Get the short join code that other players will use to connect
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Step 3: Tell our transport layer (UTP) to route traffic through the relay
            ConfigureTransport(allocation);

            // Step 4: Start as host — this player is both server and client
            NetworkManager.Singleton.StartHost();

            // Notify UI to display the join code
            OnJoinCodeReceived?.Invoke(JoinCode);
            OnSessionStarted?.Invoke();

            Debug.Log($"[SessionManager] Session hosted. Join code: {JoinCode}");
        }
        catch (Exception e)
        {
            // Common causes: no internet, UGS not configured, relay service down
            Debug.LogError($"[SessionManager] Host failed: {e.Message}");
            OnSessionFailed?.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Client — joins an existing session using the host's join code
    // -------------------------------------------------------------------------

    /// <summary>
    /// Uses the join code to find the host's relay allocation and connect to them.
    /// </summary>
    public async Task JoinSessionAsync(string joinCode)
    {
        if (!NetworkBootstrapper.Instance.IsReady)
        {
            Debug.LogWarning("[SessionManager] UGS not ready yet.");
            return;
        }

        try
        {
            // Step 1: Look up the relay allocation using the join code
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());

            // Step 2: Configure our transport to route through the relay to the host
            ConfigureTransport(joinAllocation);

            // Step 3: Start as a client and connect to the host
            NetworkManager.Singleton.StartClient();

            OnSessionStarted?.Invoke();

            Debug.Log($"[SessionManager] Joined session with code: {joinCode}");
        }
        catch (Exception e)
        {
            // Common causes: wrong join code, host no longer exists, network issue
            Debug.LogError($"[SessionManager] Join failed: {e.Message}");
            OnSessionFailed?.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Disconnect — cleanly leave or shut down the session
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shuts down the network session. Works for both host and client.
    /// After this, the game returns to the idle/lobby state.
    /// </summary>
    public void LeaveSession()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.Shutdown();
        JoinCode = null;
        Debug.Log("[SessionManager] Left session.");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tells Unity Transport (UTP) to route all network traffic through the Relay server.
    /// Called after getting an allocation — must happen before StartHost/StartClient.
    /// Two overloads: one for the host's allocation, one for the client's join allocation.
    /// </summary>
    private void ConfigureTransport(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "udp")); // udp = fast, low latency
    }

    private void ConfigureTransport(JoinAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "udp"));
    }
}
