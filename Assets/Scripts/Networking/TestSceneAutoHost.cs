using Unity.Netcode;
using UnityEngine;

// Automatically starts a local host session when entering Play mode.
// Attach this to the NetworkManager GameObject in test scenes only.
//
// Why: Test scenes skip the lobby/relay flow — they just need a running NGO
// session so NetworkBehaviours (PlayerMovement, EnemyInitializer, etc.)
// function correctly. StartHost() makes this machine both server and client,
// so IsOwner / IsServer are true and all networking works without a second player.
//
// Do NOT include this in production scenes — session start is handled by SessionManager.
public class TestSceneAutoHost : MonoBehaviour
{
    // Delay in seconds before starting the host.
    // A small delay lets all Awake/Start calls on other objects finish first.
    [SerializeField] private float startDelay = 0.1f;

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[TestSceneAutoHost] No NetworkManager found in scene. Cannot auto-start.");
            return;
        }

        // If already running (e.g. entering play mode a second time) do nothing.
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[TestSceneAutoHost] NetworkManager already listening — skipping auto-start.");
            return;
        }

        Invoke(nameof(StartHost), startDelay);
    }

    private void StartHost()
    {
        bool started = NetworkManager.Singleton.StartHost();
        Debug.Log(started
            ? "[TestSceneAutoHost] Host started successfully. NGO session is live."
            : "[TestSceneAutoHost] StartHost() failed — check NetworkManager configuration.");
    }
}
