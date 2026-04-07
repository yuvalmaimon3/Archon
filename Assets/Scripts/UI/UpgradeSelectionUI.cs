using System;
using TMPro;
using UnityEngine;

// Screen-space upgrade selection dialog shown to the local player on level-up.
// Activated by PlayerUpgradeHandler (on the owner's machine only).
// Displays up to 3 upgrade options as buttons; hides itself after a choice is made.
//
// Network: MonoBehaviour — this UI lives only on the local client.
//          No networking needed; PlayerUpgradeHandler sends the choice to the server.
//
// Setup in Unity:
//   1. Create a Canvas (Screen Space – Overlay) in the scene.
//   2. Add this component to the Canvas root.
//   3. Add a child Panel for the background.
//   4. Add a TextMeshProUGUI title inside the panel.
//   5. Add 3 child GameObjects with UpgradeOptionButton, assigned to _optionButtons[].
//   6. Set the Canvas inactive by default — Show() activates it.
public class UpgradeSelectionUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Panel")]
    [Tooltip("The root panel GameObject shown/hidden when the dialog opens/closes. " +
             "Usually the direct child of the Canvas.")]
    [SerializeField] private GameObject _panel;

    [Tooltip("Optional title text (e.g. 'Choose an Upgrade'). Set in the Inspector.")]
    [SerializeField] private TextMeshProUGUI _titleText;

    [Header("Option Buttons")]
    [Tooltip("The three upgrade option buttons. Assign all three in the Inspector. " +
             "Buttons with no matching upgrade will be hidden.")]
    [SerializeField] private UpgradeOptionButton[] _optionButtons;

    // ── Private state ────────────────────────────────────────────────────────

    // Callback provided by PlayerUpgradeHandler — fired with the chosen button index.
    private Action<int> _onChosen;

    // ── Public API ────────────────────────────────────────────────────────────

    // Shows the dialog with the given upgrade options.
    // 'options'  — the subset of upgrades to display (1–3 entries).
    // 'onChosen' — called once with the 0-based choice index when a button is clicked.
    public void Show(UpgradeDefinition[] options, Action<int> onChosen)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[UpgradeSelectionUI] Show called with no options — ignored.");
            return;
        }

        _onChosen = onChosen;

        // Populate buttons that have a matching upgrade, hide the rest
        for (int i = 0; i < _optionButtons.Length; i++)
        {
            if (_optionButtons[i] == null) continue;

            if (i < options.Length)
                _optionButtons[i].Setup(options[i], i, OnButtonClicked);
            else
                _optionButtons[i].Hide();
        }

        if (_titleText != null)
            _titleText.text = "Choose an Upgrade";

        if (_panel != null)
            _panel.SetActive(true);

        gameObject.SetActive(true);

        Debug.Log($"[UpgradeSelectionUI] Showing {options.Length} upgrade option(s).");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Fired when any option button is clicked. Hides the dialog and invokes the callback.
    private void OnButtonClicked(int index)
    {
        Debug.Log($"[UpgradeSelectionUI] Option {index} chosen.");

        if (_panel != null)
            _panel.SetActive(false);

        gameObject.SetActive(false);

        // Invoke and clear the callback (prevents accidental double-fire)
        var callback = _onChosen;
        _onChosen    = null;
        callback?.Invoke(index);
    }
}
