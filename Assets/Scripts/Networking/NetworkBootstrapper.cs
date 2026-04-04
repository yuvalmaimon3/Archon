using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// The first step in the multiplayer setup.
/// Connects to Unity Gaming Services (UGS) and signs the player in anonymously.
///
/// Why anonymous sign-in?
///   Unity Relay (which lets players connect without port forwarding) requires
///   each player to have a UGS identity. Anonymous sign-in gives them one
///   automatically — no account or password needed.
///
/// This must complete before SessionManager can host or join a session.
/// </summary>
public class NetworkBootstrapper : MonoBehaviour
{
    // Static reference so other scripts can check if UGS is ready
    public static NetworkBootstrapper Instance { get; private set; }

    // True once UGS is initialized and the player is signed in
    public bool IsReady { get; private set; }

    private void Awake()
    {
        // Singleton guard: only one instance allowed
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private async void Start()
    {
        // async void is fine for MonoBehaviour entry points (Start, Awake, etc.)
        await InitializeAsync();
    }

    /// <summary>
    /// Initializes Unity Services and signs in anonymously.
    /// Sets IsReady = true on success so SessionManager knows it can proceed.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // Step 1: Initialize the Unity Gaming Services SDK
            await UnityServices.InitializeAsync();

            // Step 2: Sign in (only if not already signed in from a previous call)
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            IsReady = true;
            Debug.Log($"[NetworkBootstrapper] Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (System.Exception e)
        {
            // Common causes: no internet connection, UGS project not configured in Dashboard
            Debug.LogError($"[NetworkBootstrapper] Initialization failed: {e.Message}");
        }
    }
}
