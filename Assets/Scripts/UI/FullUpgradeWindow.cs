using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Extension of the standard upgrade selection window (UpgradeSelectionUI).
// While the standard window shows a random subset of 3 upgrades,
// this window shows EVERY upgrade for testing and debugging.
//
// The upgrades array is passed by PlayerUpgradeHandler from its own UpgradePool —
// no scene-level pool wiring needed. Any upgrade added to the pool appears automatically.
//
// Network: MonoBehaviour — local client only.
//
// Setup:
//   1. Add to a Canvas (Screen Space – Overlay).
//   2. Assign _panel, _contentParent, and _buttonTemplate.
//   3. Set the Canvas inactive by default — Show() activates it.
public class FullUpgradeWindow : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject      _panel;
    [SerializeField] private TextMeshProUGUI _titleText;

    [Header("Scroll")]
    [Tooltip("Content transform inside the ScrollRect — buttons are parented here.")]
    [SerializeField] private Transform          _contentParent;

    [Tooltip("Template UpgradeOptionButton — hidden at Start, cloned per upgrade.")]
    [SerializeField] private UpgradeOptionButton _buttonTemplate;

    // ── Private ───────────────────────────────────────────────────────────────

    private Action<int>                        _onChosen;
    private readonly List<UpgradeOptionButton> _spawnedButtons = new();

    private void Start()
    {
        if (_buttonTemplate != null)
            _buttonTemplate.gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Opens the window showing the given upgrades.
    // upgrades — full array from the pool (passed by PlayerUpgradeHandler).
    // onChosen — fired with the 0-based index into the array when a button is clicked.
    public void Show(UpgradeDefinition[] upgrades, Action<int> onChosen)
    {
        if (upgrades == null || upgrades.Length == 0)
        {
            Debug.LogWarning("[FullUpgradeWindow] No upgrades to show — ignored.");
            return;
        }

        _onChosen = onChosen;

        ClearButtons();

        for (int i = 0; i < upgrades.Length; i++)
        {
            if (upgrades[i] == null) continue;

            var btn = Instantiate(_buttonTemplate, _contentParent);
            btn.gameObject.SetActive(true);
            btn.Setup(upgrades[i], i, OnButtonClicked);
            _spawnedButtons.Add(btn);
        }

        if (_titleText != null)
            _titleText.text = $"Full Upgrade Window ({upgrades.Length} upgrades)";

        if (_panel != null)
            _panel.SetActive(true);

        gameObject.SetActive(true);

        Debug.Log($"[FullUpgradeWindow] Showing {upgrades.Length} upgrade(s).");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnButtonClicked(int index)
    {
        Debug.Log($"[FullUpgradeWindow] Upgrade {index} chosen.");

        if (_panel != null)
            _panel.SetActive(false);

        gameObject.SetActive(false);

        ClearButtons();

        var callback = _onChosen;
        _onChosen    = null;
        callback?.Invoke(index);
    }

    private void ClearButtons()
    {
        foreach (var btn in _spawnedButtons)
            if (btn != null) Destroy(btn.gameObject);

        _spawnedButtons.Clear();
    }
}
