using UnityEngine;
using UnityEngine.SceneManagement;

// Loads the main game scene immediately after Bootstrap initializes.
// NetworkCore (NetworkManager + all networking services) is moved to
// DontDestroyOnLoad by NGO, so it survives the scene transition and
// remains available in every scene for the lifetime of the application.
public class BootstrapLoader : MonoBehaviour
{
    [SerializeField] private string gameScene = "Arena";

    private void Start()
    {
        // Skip if already in a game scene — Bootstrap was loaded additively by EditorAutoBootstrap.
        // In a normal build Bootstrap is always scene 0 and loads first, so this never triggers there.
        if (SceneManager.GetActiveScene().name != "Bootstrap")
        {
            Debug.Log($"[BootstrapLoader] Already in '{SceneManager.GetActiveScene().name}' — skipping load.");
            return;
        }

        Debug.Log($"[BootstrapLoader] Loading '{gameScene}'...");
        SceneManager.LoadScene(gameScene);
    }
}
