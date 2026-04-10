using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Creates the UpgradeSelectionUI canvas hierarchy in the currently open scene.
// Run via: Tools > Arcon > Create Upgrade Selection UI
public static class UpgradeSelectionUIBuilder
{
    [MenuItem("Tools/Arcon/Create Upgrade Selection UI")]
    private static void CreateUpgradeSelectionUI()
    {
        // ── Canvas root ───────────────────────────────────────────────────────
        var canvasGO = new GameObject("UpgradeSelectionCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler  = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        var ui = canvasGO.AddComponent<UpgradeSelectionUI>();

        // ── Panel (large, centered) ───────────────────────────────────────────
        var panelGO   = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.2f, 0.1f);
        panelRect.anchorMax = new Vector2(0.8f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        // ── Title ─────────────────────────────────────────────────────────────
        var titleGO   = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);

        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.9f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(20, 0);
        titleRect.offsetMax = new Vector2(-20, -10);

        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "Choose an Upgrade";
        titleTMP.fontSize  = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = Color.white;

        // ── ScrollView ────────────────────────────────────────────────────────
        var scrollGO   = new GameObject("ScrollView");
        scrollGO.transform.SetParent(panelGO.transform, false);

        var scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 0.9f);
        scrollRect.offsetMin = new Vector2(10, 10);
        scrollRect.offsetMax = new Vector2(-10, -5);

        var scrollComponent              = scrollGO.AddComponent<ScrollRect>();
        scrollComponent.horizontal       = false;
        scrollComponent.vertical         = true;
        scrollComponent.scrollSensitivity = 30f;
        scrollComponent.movementType     = ScrollRect.MovementType.Clamped;

        // Viewport
        var viewportGO   = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);

        var viewportRect    = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        var viewportImage   = viewportGO.AddComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0.01f); // near-transparent, required for Mask
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;

        scrollComponent.viewport = viewportRect;

        // Content
        var contentGO   = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);

        var contentRect    = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var vlg              = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing          = 10f;
        vlg.padding          = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment   = TextAnchor.UpperCenter;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf              = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit      = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit    = ContentSizeFitter.FitMode.Unconstrained;

        scrollComponent.content = contentRect;

        // Vertical Scrollbar
        var scrollbarGO = new GameObject("Scrollbar Vertical");
        scrollbarGO.transform.SetParent(scrollGO.transform, false);

        var scrollbarRect    = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot     = new Vector2(1f, 1f);
        scrollbarRect.sizeDelta = new Vector2(20f, 0f);
        scrollbarRect.offsetMin = new Vector2(-20f, 0f);
        scrollbarRect.offsetMax = Vector2.zero;

        var scrollbarBg    = scrollbarGO.AddComponent<Image>();
        scrollbarBg.color  = new Color(0.1f, 0.1f, 0.15f, 1f);

        var scrollbar      = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Scrollbar handle
        var handleGO   = new GameObject("Sliding Area");
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleAreaRect    = handleGO.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(5, 5);
        handleAreaRect.offsetMax = new Vector2(-5, -5);

        var actualHandle = new GameObject("Handle");
        actualHandle.transform.SetParent(handleGO.transform, false);
        var handleRect    = actualHandle.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(-5, -5);
        handleRect.offsetMax = new Vector2(5, 5);

        var handleImage = actualHandle.AddComponent<Image>();
        handleImage.color = new Color(0.4f, 0.4f, 0.7f, 1f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollComponent.verticalScrollbar = scrollbar;
        scrollComponent.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // ── Button Template (inactive — cloned at runtime) ────────────────────
        var templateGO = new GameObject("ButtonTemplate");
        templateGO.transform.SetParent(contentGO.transform, false);

        var templateRect    = templateGO.AddComponent<RectTransform>();
        templateRect.sizeDelta = new Vector2(0f, 120f); // fixed height per button

        var templateImage = templateGO.AddComponent<Image>();
        templateImage.color = new Color(0.18f, 0.18f, 0.28f, 1f);

        var templateButton  = templateGO.AddComponent<Button>();
        var colors          = templateButton.colors;
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.45f, 1f);
        colors.pressedColor     = new Color(0.12f, 0.12f, 0.22f, 1f);
        templateButton.colors   = colors;

        // Name label (upper half)
        var nameGO   = new GameObject("NameText");
        nameGO.transform.SetParent(templateGO.transform, false);
        var nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(12, 0);
        nameRect.offsetMax = new Vector2(-12, -8);
        var nameTMP   = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "Upgrade Name";
        nameTMP.fontSize  = 22;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.color     = Color.white;

        // Description label (lower half)
        var descGO   = new GameObject("DescriptionText");
        descGO.transform.SetParent(templateGO.transform, false);
        var descRect = descGO.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 0.5f);
        descRect.offsetMin = new Vector2(12, 8);
        descRect.offsetMax = new Vector2(-12, 0);
        var descTMP   = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text      = "Description";
        descTMP.fontSize  = 16;
        descTMP.alignment = TextAlignmentOptions.MidlineLeft;
        descTMP.color     = new Color(0.8f, 0.8f, 0.8f, 1f);

        var optionBtn = templateGO.AddComponent<UpgradeOptionButton>();

        // Wire UpgradeOptionButton fields
        var btnSO = new SerializedObject(optionBtn);
        btnSO.FindProperty("_nameText").objectReferenceValue        = nameTMP;
        btnSO.FindProperty("_descriptionText").objectReferenceValue = descTMP;
        btnSO.FindProperty("_button").objectReferenceValue          = templateButton;
        btnSO.ApplyModifiedProperties();

        // ── Wire UpgradeSelectionUI fields ────────────────────────────────────
        var uiSO = new SerializedObject(ui);
        uiSO.FindProperty("_panel").objectReferenceValue            = panelGO;
        uiSO.FindProperty("_titleText").objectReferenceValue        = titleTMP;
        uiSO.FindProperty("_scrollRect").objectReferenceValue       = scrollComponent;
        uiSO.FindProperty("_contentContainer").objectReferenceValue = contentGO.transform;
        uiSO.FindProperty("_buttonTemplate").objectReferenceValue   = optionBtn;
        uiSO.ApplyModifiedProperties();

        // Start inactive — PlayerUpgradeHandler activates on level-up
        templateGO.SetActive(false);
        canvasGO.SetActive(false);

        Undo.RegisterCreatedObjectUndo(canvasGO, "Create UpgradeSelectionUI");
        Selection.activeGameObject = canvasGO;

        Debug.Log("[UpgradeSelectionUIBuilder] UpgradeSelectionCanvas created with ScrollView. " +
                  "Run Tools > Arcon > Create Upgrade Selection UI to add it to the scene.");
    }
}
