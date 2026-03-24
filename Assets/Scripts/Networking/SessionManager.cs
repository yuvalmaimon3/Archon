using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Manages co-op sessions using Unity Relay.
/// The host creates a session and shares a join code; clients connect with that code.
/// Max 4 players (1 host + 3 clients). Adjust MaxConnections as needed.
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public const int MaxConnections = 3; // max clients (host is +1)

    public string JoinCode { get; private set; }
    public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

    public event Action<string> OnJoinCodeReceived;  // fired after host creates session
    public event Action         OnSessionStarted;
    public event Action         OnSessionFailed;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // Host
    // -------------------------------------------------------------------------

    /// <summary>Creates a Relay allocation, obtains a join code, and starts as host.</summary>
    public async Task HostSessionAsync()
    {
        if (!NetworkBootstrapper.Instance.IsReady)
        {
            Debug.LogWarning("[SessionManager] UGS not ready yet.");
            return;
        }

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            ConfigureTransport(allocation);

            NetworkManager.Singleton.StartHost();

            OnJoinCodeReceived?.Invoke(JoinCode);
            OnSessionStarted?.Invoke();

            Debug.Log($"[SessionManager] Session hosted. Join code: {JoinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionManager] Host failed: {e.Message}");
            OnSessionFailed?.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Client
    // -------------------------------------------------------------------------

    /// <summary>Joins an existing Relay session using a join code.</summary>
    public async Task JoinSessionAsync(string joinCode)
    {
        if (!NetworkBootstrapper.Instance.IsReady)
        {
            Debug.LogWarning("[SessionManager] UGS not ready yet.");
            return;
        }

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());

            ConfigureTransport(joinAllocation);

            NetworkManager.Singleton.StartClient();

            OnSessionStarted?.Invoke();

            Debug.Log($"[SessionManager] Joined session with code: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionManager] Join failed: {e.Message}");
            OnSessionFailed?.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Disconnect
    // -------------------------------------------------------------------------

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

    private void ConfigureTransport(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "udp"));
    }

    private void ConfigureTransport(JoinAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "udp"));
    }
}
