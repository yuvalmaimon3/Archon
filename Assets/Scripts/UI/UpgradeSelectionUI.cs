using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Upgrade selection dialog shown to the local player on level-up.
// Built entirely at runtime — no scene setup required.
// Call UpgradeSelectionUI.CreateInstance() to spawn it, then Show() to display.
// Network: MonoBehaviour — local client only.
public class UpgradeSelectionUI : MonoBehaviour
{
    // ── Serialized (set by CreateInstance, visible in Inspector after spawn) ──

    [SerializeField] private GameObject           _panel;
    [SerializeField] private TextMeshProUGUI      _titleText;
    [SerializeField] private ScrollRect           _scrollRect;
    [SerializeField] private Transform            _contentContainer;
    [SerializeField] private UpgradeOptionButton  _buttonTemplate;

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

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;

        Debug.Log($"[UpgradeSelectionUI] Showing {options.Length} upgrade option(s).");
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    // Builds the full canvas hierarchy at runtime — no prefab or scene setup needed.
    public static UpgradeSelectionUI CreateInstance()
    {
        // Canvas root
        var canvasGO = new GameObject("UpgradeSelectionCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        var ui = canvasGO.AddComponent<UpgradeSelectionUI>();

        // Panel — large, centered
        var panel     = CreateRect("Panel", canvasGO.transform,
                                    new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f));
        var panelImg  = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        // Title
        var titleGO = CreateRect("TitleText", panel,
                                  new Vector2(0f, 0.92f), new Vector2(1f, 1f),
                                  new Vector2(20, 0), new Vector2(-20, -8));
        var titleTMP   = titleGO.gameObject.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "Choose an Upgrade";
        titleTMP.fontSize  = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = Color.white;

        // ScrollView
        var scrollGO  = CreateRect("ScrollView", panel,
                                    new Vector2(0f, 0f), new Vector2(1f, 0.92f),
                                    new Vector2(10, 10), new Vector2(-10, -5));
        var scroll          = scrollGO.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal       = false;
        scroll.scrollSensitivity = 30f;
        scroll.movementType     = ScrollRect.MovementType.Clamped;

        // Viewport
        var viewport    = CreateRect("Viewport", scrollGO, Vector2.zero, Vector2.one);
        var vpImg       = viewport.gameObject.AddComponent<Image>();
        vpImg.color     = new Color(0, 0, 0, 0.01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport;

        // Content
        var contentRT        = new GameObject("Content").AddComponent<RectTransform>();
        contentRT.SetParent(viewport, false);
        contentRT.anchorMin  = new Vector2(0f, 1f);
        contentRT.anchorMax  = new Vector2(1f, 1f);
        contentRT.pivot      = new Vector2(0.5f, 1f);
        contentRT.sizeDelta  = Vector2.zero;

        var vlg                    = contentRT.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 10f;
        vlg.padding                = new RectOffset(10, 30, 10, 10); // right padding for scrollbar
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf             = contentRT.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit     = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content      = contentRT;

        // Scrollbar
        var sbGO = new GameObject("Scrollbar Vertical").AddComponent<RectTransform>();
        sbGO.SetParent(scrollGO, false);
        sbGO.anchorMin  = new Vector2(1f, 0f);
        sbGO.anchorMax  = new Vector2(1f, 1f);
        sbGO.pivot      = new Vector2(1f, 1f);
        sbGO.sizeDelta  = new Vector2(20f, 0f);
        sbGO.offsetMin  = new Vector2(-20f, 0f);
        sbGO.offsetMax  = Vector2.zero;

        var sbImg  = sbGO.gameObject.AddComponent<Image>();
        sbImg.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        var sb     = sbGO.gameObject.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;

        var slideArea = new GameObject("Sliding Area").AddComponent<RectTransform>();
        slideArea.SetParent(sbGO, false);
        slideArea.anchorMin = Vector2.zero;
        slideArea.anchorMax = Vector2.one;
        slideArea.offsetMin = new Vector2(5, 5);
        slideArea.offsetMax = new Vector2(-5, -5);

        var handle    = new GameObject("Handle").AddComponent<RectTransform>();
        handle.SetParent(slideArea, false);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = new Vector2(-5, -5);
        handle.offsetMax = new Vector2(5, 5);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.4f, 0.4f, 0.7f, 1f);
        sb.handleRect    = handle;
        sb.targetGraphic = handleImg;

        scroll.verticalScrollbar            = sb;
        scroll.verticalScrollbarVisibility  = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // Button template (inactive — cloned per upgrade in Show())
        var templateGO  = new GameObject("ButtonTemplate");
        templateGO.transform.SetParent(contentRT, false);
        var templateRect       = templateGO.AddComponent<RectTransform>();
        templateRect.sizeDelta = new Vector2(0f, 110f);

        var btnImg  = templateGO.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);

        var btn    = templateGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.45f, 1f);
        colors.pressedColor     = new Color(0.12f, 0.12f, 0.22f, 1f);
        btn.colors = colors;

        var nameRT = CreateRect("NameText", templateGO.transform,
                                 new Vector2(0f, 0.5f), Vector2.one,
                                 new Vector2(12, 0), new Vector2(-12, -8));
        var nameTMP   = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "Upgrade Name";
        nameTMP.fontSize  = 22;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.color     = Color.white;

        var descRT = CreateRect("DescriptionText", templateGO.transform,
                                 Vector2.zero, new Vector2(1f, 0.5f),
                                 new Vector2(12, 8), new Vector2(-12, 0));
        var descTMP   = descRT.gameObject.AddComponent<TextMeshProUGUI>();
        descTMP.fontSize  = 16;
        descTMP.alignment = TextAlignmentOptions.MidlineLeft;
        descTMP.color     = new Color(0.8f, 0.8f, 0.8f, 1f);

        var optionBtn = templateGO.AddComponent<UpgradeOptionButton>();
        optionBtn.SetReferences(nameTMP, descTMP, btn);

        // Wire UpgradeSelectionUI fields
        ui._panel            = panel.gameObject;
        ui._titleText        = titleTMP;
        ui._scrollRect       = scroll;
        ui._contentContainer = contentRT;
        ui._buttonTemplate   = optionBtn;

        // Start inactive
        templateGO.SetActive(false);
        canvasGO.SetActive(false);

        DontDestroyOnLoad(canvasGO);

        Debug.Log("[UpgradeSelectionUI] Canvas created at runtime.");
        return ui;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnButtonClicked(int index)
    {
        Debug.Log($"[UpgradeSelectionUI] Option {index} chosen.");

        if (_panel != null) _panel.SetActive(false);
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

    // Helper: creates a RectTransform child with anchor/offset settings
    private static RectTransform CreateRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt        = go.AddComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        return rt;
    }
}
