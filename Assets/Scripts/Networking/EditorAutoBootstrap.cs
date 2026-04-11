#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only: when pressing Play from any non-Bootstrap scene,
// loads Bootstrap additively so the NetworkManager initializes.
// BootstrapLoader then detects a game scene is already active and skips redirecting.
// Has no effect in builds — Bootstrap is always scene 0 there.
public static class EditorAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoLoadBootstrap()
    {
        if (SceneManager.GetActiveScene().name == "Bootstrap") return;

        // Load Bootstrap additively — brings in NetworkManager without switching scenes.
        SceneManager.LoadScene("Bootstrap", LoadSceneMode.Additive);
    }
}
#endif
