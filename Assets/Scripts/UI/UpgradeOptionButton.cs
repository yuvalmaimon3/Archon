using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Represents one upgrade choice in the upgrade selection dialog.
// Populated at runtime by UpgradeSelectionUI with a specific UpgradeDefinition.
//
// Attach to a UI Button GameObject that has:
//   - a TextMeshProUGUI child named "NameText"
//   - a TextMeshProUGUI child named "DescriptionText"
//   - an Image child named "IconImage" (optional — hidden when no icon is set)
//   - a Button component on the root
public class UpgradeOptionButton : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Displays the upgrade name.")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("Displays the upgrade description.")]
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Tooltip("Displays the upgrade icon. Leave empty to skip icon rendering.")]
    [SerializeField] private Image _iconImage;

    [Tooltip("The Button component. Wired automatically if left empty.")]
    [SerializeField] private Button _button;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve the Button if not assigned in the Inspector
        if (_button == null)
            _button = GetComponent<Button>();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Configures this button to display the given upgrade and fire 'onClick' with
    // the button's index when clicked. Called by UpgradeSelectionUI before showing the panel.
    public void Setup(UpgradeDefinition upgrade, int index, System.Action<int> onClick)
    {
        if (_nameText != null)
            _nameText.text = upgrade != null ? upgrade.upgradeName : "???";

        if (_descriptionText != null)
            _descriptionText.text = upgrade != null ? upgrade.description : string.Empty;

        // Show icon if one is assigned; hide the image slot otherwise so layout stays clean
        if (_iconImage != null)
        {
            bool hasIcon = upgrade != null && upgrade.icon != null;
            _iconImage.sprite  = hasIcon ? upgrade.icon : null;
            _iconImage.enabled = hasIcon;
        }

        // Clear previous listeners, then register the new one
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick(index));

        gameObject.SetActive(true);
    }

    // Hides and clears this button (used when fewer options are available than slots).
    public void Hide()
    {
        _button.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
