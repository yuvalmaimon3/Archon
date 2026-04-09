using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Test/debug upgrade window that shows EVERY upgrade in the pool.
// Reads directly from the UpgradePool asset — any new upgrade added to the pool
// appears automatically the next time the window opens.
//
// PlayerUpgradeHandler detects this window (highest priority) and uses it
// instead of the normal 3-choice UI or UpgradeAllSelectionUI.
//
// Network: MonoBehaviour — local client only.
//
// Setup:
//   1. Add to a Canvas (Screen Space – Overlay).
//   2. Assign _upgradePool, _panel, _contentParent, and _buttonTemplate.
//   3. Set the Canvas inactive by default — Show() activates it.
public class FullUpgradeWindow : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("The upgrade pool to read from. New upgrades added here appear automatically.")]
    [SerializeField] private UpgradePool _upgradePool;

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

    // Returns the full upgrades array from the pool (used by PlayerUpgradeHandler
    // to set _pendingChoices so the chosen index maps correctly to a pool index).
    public UpgradeDefinition[] GetAllUpgrades()
    {
        return _upgradePool != null ? _upgradePool.upgrades : Array.Empty<UpgradeDefinition>();
    }

    // Opens the window showing all upgrades from the pool.
    // onChosen — fired with the 0-based index into the pool's upgrades array.
    public void Show(Action<int> onChosen)
    {
        if (_upgradePool == null || _upgradePool.upgrades.Length == 0)
        {
            Debug.LogWarning("[FullUpgradeWindow] No UpgradePool or pool is empty — ignored.");
            return;
        }

        _onChosen = onChosen;

        ClearButtons();

        var upgrades = _upgradePool.upgrades;
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

        Debug.Log($"[FullUpgradeWindow] Showing {upgrades.Length} upgrade(s) from pool.");
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
