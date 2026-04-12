using TMPro;
using UnityEngine;

// Spawns floating damage numbers above an enemy on each hit.
// Add this component to any enemy that has NetworkDeathSync.
//
// Network: MonoBehaviour — client-side visual only.
//          NetworkDeathSync.OnDamageDealt fires on all machines (server + clients),
//          so every player sees the numbers without an extra RPC.
[RequireComponent(typeof(NetworkDeathSync))]
public class DamageNumberSpawner : MonoBehaviour
{
    [Tooltip("World-space height above the enemy's origin where numbers spawn.")]
    [SerializeField] private float _spawnHeight = 2.2f;

    [Tooltip("Font size of the damage number.")]
    [SerializeField] private float _fontSize = 6f;

    [Tooltip("Optional: override the font asset. Leave empty to use TMP's default.")]
    [SerializeField] private TMP_FontAsset _font;

    private NetworkDeathSync _deathSync;

    private void Awake()
    {
        _deathSync = GetComponent<NetworkDeathSync>();
    }

    [Tooltip("Text color for critical hits.")]
    [SerializeField] private Color _critColor = Color.red;

    [Tooltip("Font size multiplier for critical hit numbers.")]
    [SerializeField] private float _critFontSizeMultiplier = 1.4f;

    private void OnEnable()
    {
        if (_deathSync != null)
            _deathSync.OnDamageDealt += SpawnNumber;
    }

    private void OnDisable()
    {
        if (_deathSync != null)
            _deathSync.OnDamageDealt -= SpawnNumber;
    }

    private void SpawnNumber(int amount, bool isCritical)
    {
        var go  = new GameObject("DmgNum");
        go.transform.position = transform.position + Vector3.up * _spawnHeight;

        var tmp        = go.AddComponent<TextMeshPro>();
        tmp.fontSize   = isCritical ? _fontSize * _critFontSizeMultiplier : _fontSize;
        tmp.color      = isCritical ? _critColor : Color.white;
        tmp.fontStyle  = FontStyles.Bold;
        tmp.alignment  = TextAlignmentOptions.Center;

        if (_font != null)
            tmp.font = _font;

        // Billboard — always faces the camera
        go.AddComponent<FaceCamera>();

        go.AddComponent<FloatingDamageText>().Play(amount);
    }
}
