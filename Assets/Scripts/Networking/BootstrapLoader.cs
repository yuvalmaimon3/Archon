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
        Debug.Log($"[BootstrapLoader] Loading '{gameScene}'...");
        SceneManager.LoadScene(gameScene);
    }
}
