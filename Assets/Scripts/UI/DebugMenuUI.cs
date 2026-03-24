using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the debug menu panel on the left side of the screen.
/// Always visible during Play mode so you can control the game easily while testing.
///
/// Button methods are PUBLIC so they can be wired directly in the Unity Inspector
/// (click the button in the Hierarchy → Inspector → OnClick() to see the assignment).
/// </summary>
public class DebugMenuUI : MonoBehaviour
{
    // --- Inspector-assigned references ---
    // Drag the corresponding GameObjects from the Hierarchy into these slots in the Inspector

    [Header("Host")]
    [SerializeField] private Button    startHostButton;   // shows when game is NOT started
    [SerializeField] private Button    stopButton;    // shows when game IS started
    [SerializeField] private TMP_Text  joinCodeLabel; // displays the relay join code after hosting

    [Header("Join")]
    [SerializeField] private TMP_InputField joinCodeInput; // text field where client types the join code
    [SerializeField] private Button         joinButton;    // client clicks this to connect

    [Header("Quick Test (no Relay)")]
    [SerializeField] private Button startClientButton; // instantly joins localhost as client — no join code needed

    [Header("Common")]
    [SerializeField] private Button restartHostButton; // reloads the current scene
    [SerializeField] private Button exitButton;    // quits the game (or stops Play mode in Editor)

    // Colors used to visually hint whether the Start button is active or inactive
    private static readonly Color ActiveColor   = new Color(0.18f, 0.52f, 0.18f); // green = clickable
    private static readonly Color InactiveColor = new Color(0.45f, 0.45f, 0.45f); // grey = already started

    private void Start()
    {
        // Listen for the join code event so we can display it on screen when hosting
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnJoinCodeReceived += ShowJoinCode;

        // Set the correct initial button visibility
        Refresh();
    }

    private void OnDestroy()
    {
        // Always unsubscribe from events when the object is destroyed to avoid memory leaks
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnJoinCodeReceived -= ShowJoinCode;
    }

    // -------------------------------------------------------------------------
    // Button handlers — each is wired to a button's OnClick() in the Inspector
    // -------------------------------------------------------------------------

    /// <summary>
    /// START button — begins the game as HOST.
    /// Unlocks player movement and starts accepting connections from other players.
    /// </summary>
    public void OnStart()
    {
        GameManager.Instance?.StartGame();             // set IsGameStarted = true → movement unlocked
        NetworkManager.Singleton?.StartHost();         // start as host so others can join
        Refresh();                                     // update button visibility
    }

    /// <summary>
    /// JOIN button — connects this player to an existing host session.
    /// Reads the join code from the input field and uses Relay to find the host.
    /// </summary>
    public async void OnJoin()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) return; // do nothing if the input field is empty

        if (SessionManager.Instance != null)
            await SessionManager.Instance.JoinSessionAsync(code); // connect via Relay
        else
            NetworkManager.Singleton?.StartClient(); // fallback: direct connect (LAN only)

        GameManager.Instance?.StartGame(); // unlock movement after connecting
        Refresh();
    }

    /// <summary>
    /// START CLIENT button — directly connects to localhost as a client. No Relay, no join code.
    /// Use this for quick local testing: run the game twice (or use ParrelSync),
    /// press Start on one instance and Start Client on the other.
    /// </summary>
    public void OnStartClient()
    {
        // Bypass everything — just connect directly to localhost
        // Avoid ?. on Unity objects — Unity overrides == null, so ?. can bypass that check
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.StartClient();
        if (GameManager.Instance != null)     GameManager.Instance.StartGame();
        Refresh();
    }

    /// <summary>
    /// STOP button — ends the current session and freezes gameplay.
    /// The player stays in the scene but can't move until Start is pressed again.
    /// </summary>
    public void OnStop()
    {
        GameManager.Instance?.StopGame(); // freeze gameplay

        if (SessionManager.Instance != null)
            SessionManager.Instance.LeaveSession(); // disconnect from relay
        else
            NetworkManager.Singleton?.Shutdown(); // fallback shutdown

        if (joinCodeLabel != null) joinCodeLabel.text = ""; // clear the join code from screen
        Refresh();
    }

    /// <summary>
    /// RESTART button — completely reloads the current scene.
    /// Useful for resetting everything to its initial state quickly.
    /// </summary>
    public void OnRestart()
    {
        GameManager.Instance?.StopGame();
        NetworkManager.Singleton?.Shutdown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // reload current scene
    }

    /// <summary>
    /// EXIT button — stops Play mode in the Unity Editor, or quits the built game.
    /// </summary>
    public void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // stop Play mode in Editor
#else
        Application.Quit(); // quit the built game
#endif
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Displays the relay join code on screen after the host creates a session.
    /// Other players need to type this code to connect.
    /// </summary>
    private void ShowJoinCode(string code)
    {
        if (joinCodeLabel != null)
            joinCodeLabel.text = $"Code:\n{code}";
    }

    /// <summary>
    /// Updates button visibility based on current game state.
    /// Called after any state change (start, stop, join, etc.).
    /// </summary>
    private void Refresh()
    {
        bool started = GameManager.Instance != null && GameManager.Instance.IsGameStarted;
        bool running = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Swap Start ↔ Stop depending on whether the game is running
        startHostButton.gameObject.SetActive(!started);
        stopButton .gameObject.SetActive(started);

        // Hide the Join section once a connection is active (no point joining when already connected)
        if (joinCodeInput != null) joinCodeInput.gameObject.SetActive(!running);
        if (joinButton    != null) joinButton.gameObject   .SetActive(!running);

        // Clear join code label when not started
        if (!started && joinCodeLabel != null)
            joinCodeLabel.text = "";

        // Green when clickable, grey when already started
        SetButtonColor(startHostButton, started ? InactiveColor : ActiveColor);
    }

    /// <summary>Changes the background color of a button's Image component.</summary>
    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }
}
