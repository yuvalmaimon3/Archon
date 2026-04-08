using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Test-time upgrade dialog that shows every upgrade in the pool (not just 3).
// PlayerUpgradeHandler prefers this UI over UpgradeSelectionUI when both are in the scene.
// Uses a scroll view so the list can grow beyond the window.
//
// Network: MonoBehaviour — local client only, same as UpgradeSelectionUI.
//
// Setup in Unity:
//   1. Add this component to a Canvas root.
//   2. Assign _panel, _titleText, _contentParent (ScrollRect Content), and _buttonTemplate.
//   3. The _buttonTemplate child is deactivated at Start and cloned per upgrade at runtime.
//   4. Set the Canvas inactive by default — PlayerUpgradeHandler activates it via Show().
public class UpgradeAllSelectionUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject        _panel;
    [SerializeField] private TextMeshProUGUI   _titleText;

    [Header("Scroll")]
    [Tooltip("Content transform inside the ScrollRect — buttons are parented here.")]
    [SerializeField] private Transform         _contentParent;

    [Tooltip("Template UpgradeOptionButton child — hidden at Start, cloned per upgrade.")]
    [SerializeField] private UpgradeOptionButton _buttonTemplate;

    // ── Private ───────────────────────────────────────────────────────────────

    private Action<int>                      _onChosen;
    private readonly List<UpgradeOptionButton> _spawnedButtons = new();

    private void Start()
    {
        // Keep the template hidden so it doesn't appear as an extra button
        if (_buttonTemplate != null)
            _buttonTemplate.gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Same signature as UpgradeSelectionUI.Show() — PlayerUpgradeHandler calls this.
    // options   — full upgrade list (all pool entries for test mode).
    // onChosen  — fired with the 0-based index into 'options' when a button is clicked.
    public void Show(UpgradeDefinition[] options, Action<int> onChosen)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[UpgradeAllSelectionUI] Show called with no options — ignored.");
            return;
        }

        _onChosen = onChosen;

        ClearButtons();

        for (int i = 0; i < options.Length; i++)
        {
            var btn = Instantiate(_buttonTemplate, _contentParent);
            btn.gameObject.SetActive(true);
            btn.Setup(options[i], i, OnButtonClicked);
            _spawnedButtons.Add(btn);
        }

        if (_titleText != null)
            _titleText.text = $"[TEST] Choose an Upgrade  ({options.Length} available)";

        if (_panel != null)
            _panel.SetActive(true);

        gameObject.SetActive(true);

        Debug.Log($"[UpgradeAllSelectionUI] Showing all {options.Length} upgrade(s).");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnButtonClicked(int index)
    {
        Debug.Log($"[UpgradeAllSelectionUI] Option {index} chosen.");

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
