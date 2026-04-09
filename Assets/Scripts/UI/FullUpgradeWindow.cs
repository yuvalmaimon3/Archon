using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Debug/testing window that shows ALL upgrades from the pool at once.
// Works exactly like UpgradeSelectionUI but spawns one button per upgrade dynamically.
// Network: MonoBehaviour — local client only.
public class FullUpgradeWindow : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject      _panel;
    [SerializeField] private TextMeshProUGUI _titleText;

    [Tooltip("Template button — hidden at Start, cloned once per upgrade.")]
    [SerializeField] private UpgradeOptionButton _buttonTemplate;

    private Action<int>                        _onChosen;
    private readonly List<UpgradeOptionButton> _spawnedButtons = new();

    private void Start()
    {
        if (_buttonTemplate != null)
            _buttonTemplate.gameObject.SetActive(false);
    }

    // Called by PlayerUpgradeHandler with all upgrades in the pool.
    public void Show(UpgradeDefinition[] upgrades, Action<int> onChosen)
    {
        if (upgrades == null || upgrades.Length == 0)
        {
            Debug.LogWarning("[FullUpgradeWindow] No upgrades to show.");
            return;
        }

        _onChosen = onChosen;

        ClearButtons();

        for (int i = 0; i < upgrades.Length; i++)
        {
            if (upgrades[i] == null) continue;
            var btn = Instantiate(_buttonTemplate, _buttonTemplate.transform.parent);
            btn.gameObject.SetActive(true);
            btn.Setup(upgrades[i], i, OnButtonClicked);
            _spawnedButtons.Add(btn);
        }

        if (_titleText != null)
            _titleText.text = $"All Upgrades ({upgrades.Length})";

        if (_panel != null)
            _panel.SetActive(true);

        gameObject.SetActive(true);

        Debug.Log($"[FullUpgradeWindow] Showing {upgrades.Length} upgrade(s).");
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
