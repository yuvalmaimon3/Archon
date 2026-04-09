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
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above HUD

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var ui = canvasGO.AddComponent<UpgradeSelectionUI>();

        // ── Panel (semi-transparent background) ──────────────────────────────
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.25f);
        panelRect.anchorMax = new Vector2(0.7f, 0.75f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage  = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

        // ── Title text ────────────────────────────────────────────────────────
        var titleGO   = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);

        var titleRect  = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.82f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(10, 0);
        titleRect.offsetMax = new Vector2(-10, -10);

        var titleTMP   = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text  = "Choose an Upgrade";
        titleTMP.fontSize       = 28;
        titleTMP.fontStyle      = FontStyles.Bold;
        titleTMP.alignment      = TextAlignmentOptions.Center;
        titleTMP.color          = Color.white;

        // ── Three option buttons ──────────────────────────────────────────────
        var buttonSlots = new UpgradeOptionButton[3];

        for (int i = 0; i < 3; i++)
        {
            float yMax = 0.78f - i * 0.27f;
            float yMin = yMax  - 0.22f;

            var btnGO  = new GameObject($"OptionButton_{i}");
            btnGO.transform.SetParent(panelGO.transform, false);

            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.05f, yMin);
            btnRect.anchorMax = new Vector2(0.95f, yMax);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.18f, 0.18f, 0.28f, 1f);

            var button   = btnGO.AddComponent<Button>();
            var colors   = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.28f, 0.45f, 1f);
            colors.pressedColor     = new Color(0.12f, 0.12f, 0.22f, 1f);
            button.colors           = colors;

            // Name label (upper half of button)
            var nameGO   = new GameObject("NameText");
            nameGO.transform.SetParent(btnGO.transform, false);
            var nameRect  = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(8, 0);
            nameRect.offsetMax = new Vector2(-8, -4);
            var nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text      = "Upgrade Name";
            nameTMP.fontSize  = 20;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
            nameTMP.color     = Color.white;

            // Description label (lower half of button)
            var descGO   = new GameObject("DescriptionText");
            descGO.transform.SetParent(btnGO.transform, false);
            var descRect  = descGO.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 0.5f);
            descRect.offsetMin = new Vector2(8, 4);
            descRect.offsetMax = new Vector2(-8, 0);
            var descTMP  = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text      = "Description";
            descTMP.fontSize  = 15;
            descTMP.alignment = TextAlignmentOptions.MidlineLeft;
            descTMP.color     = new Color(0.8f, 0.8f, 0.8f, 1f);

            var optionBtn = btnGO.AddComponent<UpgradeOptionButton>();

            // Wire serialized fields via SerializedObject so the Inspector shows them
            var so = new SerializedObject(optionBtn);
            so.FindProperty("_nameText").objectReferenceValue        = nameTMP;
            so.FindProperty("_descriptionText").objectReferenceValue = descTMP;
            so.FindProperty("_button").objectReferenceValue          = button;
            so.ApplyModifiedProperties();

            buttonSlots[i] = optionBtn;
        }

        // ── Wire UpgradeSelectionUI serialized fields ─────────────────────────
        var uiSO = new SerializedObject(ui);
        uiSO.FindProperty("_panel").objectReferenceValue = panelGO;
        uiSO.FindProperty("_titleText").objectReferenceValue = titleGO.GetComponent<TextMeshProUGUI>();

        var buttonsArray = uiSO.FindProperty("_optionButtons");
        buttonsArray.arraySize = 3;
        for (int i = 0; i < 3; i++)
            buttonsArray.GetArrayElementAtIndex(i).objectReferenceValue = buttonSlots[i];

        uiSO.ApplyModifiedProperties();

        // Start inactive — PlayerUpgradeHandler activates it via Show()
        canvasGO.SetActive(false);

        Undo.RegisterCreatedObjectUndo(canvasGO, "Create UpgradeSelectionUI");
        Selection.activeGameObject = canvasGO;

        Debug.Log("[UpgradeSelectionUIBuilder] UpgradeSelectionCanvas created and wired. " +
                  "Canvas is inactive by default — PlayerUpgradeHandler will activate it on level-up.");
    }
}
