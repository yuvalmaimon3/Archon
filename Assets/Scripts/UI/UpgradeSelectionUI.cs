using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Upgrade selection dialog shown to the local player on level-up.
// Dynamically spawns one button per option inside a ScrollView — supports any count.
// Activated by PlayerUpgradeHandler (owner only). Network: MonoBehaviour, local client only.
public class UpgradeSelectionUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject      _panel;
    [SerializeField] private TextMeshProUGUI _titleText;

    [Header("Scroll")]
    [SerializeField] private ScrollRect          _scrollRect;
    [SerializeField] private Transform           _contentContainer;

    [Tooltip("Hidden template button — cloned once per upgrade option.")]
    [SerializeField] private UpgradeOptionButton _buttonTemplate;

    // ── Private state ────────────────────────────────────────────────────────

    private Action<int>                        _onChosen;
    private readonly List<UpgradeOptionButton> _spawnedButtons = new();

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        if (_buttonTemplate != null)
            _buttonTemplate.gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(UpgradeDefinition[] options, Action<int> onChosen)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[UpgradeSelectionUI] Show called with no options — ignored.");
            return;
        }

        _onChosen = onChosen;

        ClearButtons();

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null) continue;

            var btn = Instantiate(_buttonTemplate, _contentContainer);
            btn.Setup(options[i], i, OnButtonClicked);
            _spawnedButtons.Add(btn);
        }

        if (_titleText != null)
            _titleText.text = "Choose an Upgrade";

        if (_panel != null)
            _panel.SetActive(true);

        gameObject.SetActive(true);

        // Scroll to top
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;

        Debug.Log($"[UpgradeSelectionUI] Showing {options.Length} upgrade option(s).");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnButtonClicked(int index)
    {
        Debug.Log($"[UpgradeSelectionUI] Option {index} chosen.");

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
