using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Extension of the standard upgrade selection window (UpgradeSelectionUI).
// While the standard window shows a random subset of 3 upgrades,
// this window shows EVERY upgrade for testing and debugging.
//
// Two ways to populate:
//   1. PlayerUpgradeHandler calls Show(upgrades, callback) during level-up.
//   2. Manually enabling the canvas in play mode — OnEnable auto-finds the
//      UpgradePool from PlayerUpgradeHandler and populates the buttons.
//
// Any upgrade added to the pool appears automatically.
//
// Network: MonoBehaviour — local client only.
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
    private bool _templateHidden;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        HideTemplate();
        EnsureLayoutConfig();

        // Auto-populate when manually enabled and no buttons exist yet
        if (_spawnedButtons.Count > 0) return;

        var upgrades = FindUpgradesFromPlayer();
        if (upgrades != null && upgrades.Length > 0)
        {
            PopulateButtons(upgrades);
            Debug.Log($"[FullUpgradeWindow] Auto-populated {upgrades.Length} upgrade(s) on enable.");
        }
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

        HideTemplate();
        EnsureLayoutConfig();
        PopulateButtons(upgrades);

        if (_panel != null)
            _panel.SetActive(true);

        gameObject.SetActive(true);

        Debug.Log($"[FullUpgradeWindow] Showing {upgrades.Length} upgrade(s).");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void HideTemplate()
    {
        if (_templateHidden || _buttonTemplate == null) return;
        _buttonTemplate.gameObject.SetActive(false);
        _templateHidden = true;
    }

    // Ensures the parent layout groups are configured to give the scroll view proper height.
    // Fixes cases where the VerticalLayoutGroup above the ScrollView has childControlHeight off,
    // which causes the scroll area to collapse to zero height.
    private void EnsureLayoutConfig()
    {
        if (_contentParent == null) return;

        // Walk up from Content → Viewport → ScrollView → parent with VerticalLayoutGroup
        var scrollView = _contentParent.parent?.parent; // Content → Viewport → ScrollView
        if (scrollView == null) return;

        var layoutParent = scrollView.parent; // InnerPanel or similar
        if (layoutParent == null) return;

        var vlg = layoutParent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
        }

        // Ensure ScrollView has a LayoutElement with flexible height so it fills available space
        var scrollLE = scrollView.GetComponent<LayoutElement>();
        if (scrollLE == null)
            scrollLE = scrollView.gameObject.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1f;
    }

    // Finds the UpgradePool from any PlayerUpgradeHandler in the scene.
    private UpgradeDefinition[] FindUpgradesFromPlayer()
    {
        var handler = FindFirstObjectByType<PlayerUpgradeHandler>();
        if (handler == null) return null;

        return handler.UpgradePool?.upgrades;
    }

    private void PopulateButtons(UpgradeDefinition[] upgrades)
    {
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

        // Force layout rebuild so scroll view sizes correctly
        if (_contentParent is RectTransform contentRT)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);

        // Also rebuild the root panel to propagate layout changes up
        if (_panel != null && _panel.TryGetComponent<RectTransform>(out var panelRT))
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);
    }

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
