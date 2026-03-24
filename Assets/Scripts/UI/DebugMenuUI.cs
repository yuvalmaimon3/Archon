using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple in-game debug menu — always visible on the left side for testing.
/// Start button launches the game (enables movement). Join lets a client connect.
/// </summary>
public class DebugMenuUI : MonoBehaviour
{
    [Header("Host")]
    [SerializeField] private Button    startButton;
    [SerializeField] private Button    stopButton;
    [SerializeField] private TMP_Text  joinCodeLabel;

    [Header("Join")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button         joinButton;

    [Header("Common")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button exitButton;

    private static readonly Color ActiveColor   = new Color(0.18f, 0.52f, 0.18f);
    private static readonly Color InactiveColor = new Color(0.45f, 0.45f, 0.45f);

    private void Start()
    {
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnJoinCodeReceived += ShowJoinCode;

        Refresh();
    }

    private void OnDestroy()
    {
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnJoinCodeReceived -= ShowJoinCode;
    }

    // -------------------------------------------------------------------------

    // Called by StartButton OnClick() in the Inspector
    public void OnStart()
    {
        GameManager.Instance?.StartGame();
        NetworkManager.Singleton?.StartHost();
        Refresh();
    }

    // Called by JoinButton OnClick() in the Inspector
    public async void OnJoin()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) return;

        if (SessionManager.Instance != null)
            await SessionManager.Instance.JoinSessionAsync(code);
        else
            NetworkManager.Singleton?.StartClient();

        GameManager.Instance?.StartGame();
        Refresh();
    }

    // Called by StopButton OnClick() in the Inspector
    public void OnStop()
    {
        GameManager.Instance?.StopGame();

        if (SessionManager.Instance != null)
            SessionManager.Instance.LeaveSession();
        else
            NetworkManager.Singleton?.Shutdown();

        if (joinCodeLabel != null) joinCodeLabel.text = "";
        Refresh();
    }

    // Called by RestartButton OnClick() in the Inspector
    public void OnRestart()
    {
        GameManager.Instance?.StopGame();
        NetworkManager.Singleton?.Shutdown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Called by ExitButton OnClick() in the Inspector
    public void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowJoinCode(string code)
    {
        if (joinCodeLabel != null)
            joinCodeLabel.text = $"Code:\n{code}";
    }

    private void Refresh()
    {
        bool started = GameManager.Instance != null && GameManager.Instance.IsGameStarted;
        bool running = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Start/Stop swap
        startButton.gameObject.SetActive(!started);
        stopButton .gameObject.SetActive(started);

        // Hide join section once connected
        if (joinCodeInput != null) joinCodeInput.gameObject.SetActive(!running);
        if (joinButton    != null) joinButton.gameObject   .SetActive(!running);

        if (!started && joinCodeLabel != null)
            joinCodeLabel.text = "";

        // Tint Start button to hint it's clickable
        SetButtonColor(startButton, started ? InactiveColor : ActiveColor);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }
}
