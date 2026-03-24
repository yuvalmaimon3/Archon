using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Initializes Unity Gaming Services and signs the player in anonymously.
/// Must run before any Relay or session operations.
/// Attach to a persistent GameObject that lives for the entire session.
/// </summary>
public class NetworkBootstrapper : MonoBehaviour
{
    public static NetworkBootstrapper Instance { get; private set; }

    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private async void Start()
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            IsReady = true;
            Debug.Log($"[NetworkBootstrapper] Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkBootstrapper] Initialization failed: {e.Message}");
        }
    }
}
