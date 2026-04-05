using System.Collections;
using UnityEngine;

// Temporary melee attack visual feedback — remove this component once real animations exist.
//
// Attach to any melee enemy alongside AttackController.
// Subscribes to AttackController.OnAttackUsed and shows a fading ground ring
// matching the exact MeleeRadius from the AttackDefinition whenever the attack fires.
// Zero changes to base combat code — fully self-contained.
public class MeleeSwingFeedback : MonoBehaviour
{
    [Header("Ring Appearance")]
    [Tooltip("Color of the flash ring.")]
    [SerializeField] private Color ringColor = new Color(1f, 0.3f, 0.1f, 1f);

    [Tooltip("Width of the ring line in world units.")]
    [SerializeField] private float lineWidth = 0.08f;

    [Header("Animation")]
    [Tooltip("How long the ring stays visible in seconds.")]
    [SerializeField] private float duration = 0.35f;

    [Tooltip("How much the ring expands outward during the flash. 0 = no expansion.")]
    [SerializeField] private float expandAmount = 0.3f;

    // Number of points used to approximate the circle — higher = smoother
    private const int Segments = 32;

    private AttackController _attackController;
    private LineRenderer _ring;
    private Coroutine _flashCoroutine;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _attackController = GetComponent<AttackController>();

        if (_attackController == null)
        {
            Debug.LogError($"[MeleeSwingFeedback] No AttackController found on '{name}'. Disabling.", this);
            enabled = false;
            return;
        }

        _ring = BuildRingRenderer();
        _ring.enabled = false;
    }

    // Subscribe/unsubscribe through OnEnable/OnDisable so the handler
    // is automatically removed when the enemy is disabled (e.g. on death).
    private void OnEnable()
    {
        if (_attackController != null)
            _attackController.OnAttackUsed += OnAttackUsed;
    }

    private void OnDisable()
    {
        if (_attackController != null)
            _attackController.OnAttackUsed -= OnAttackUsed;
    }

    // ── Event handler ────────────────────────────────────────────────────────

    // Called every time any attack on this controller fires.
    // Ignored if the attack is not Melee type.
    private void OnAttackUsed()
    {
        AttackDefinition def = _attackController.AttackDefinition;
        if (def == null || def.AttackType != AttackType.Melee) return;

        // Restart the flash if one is already running (rapid attacks)
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(FlashRing(def.MeleeRadius));
    }

    // ── Visual ───────────────────────────────────────────────────────────────

    // Animates the ring: expands outward and fades alpha to zero over duration seconds.
    private IEnumerator FlashRing(float radius)
    {
        _ring.enabled = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // Ease-out fade: snappy flash, slow tail
            float alpha = Mathf.Pow(1f - t, 2f);
            float currentRadius = radius + expandAmount * t;

            Color c = ringColor;
            c.a = alpha;
            _ring.startColor = c;
            _ring.endColor = c;

            DrawCircle(_ring, currentRadius);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _ring.enabled = false;
        _flashCoroutine = null;
    }

    // Creates a dedicated child GameObject with a LineRenderer configured as a horizontal ring.
    // Child is visible in the Hierarchy as "MeleeRingFX".
    private LineRenderer BuildRingRenderer()
    {
        var child = new GameObject("MeleeRingFX");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;

        var lr = child.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = Segments;
        lr.useWorldSpace = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.textureMode = LineTextureMode.Tile;

        // Sprites/Default is unlit, alpha-blended, and works in both Built-in and URP
        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = ringColor;
        lr.endColor = ringColor;

        return lr;
    }

    // Fills the LineRenderer with Segments positions forming a flat horizontal circle.
    private static void DrawCircle(LineRenderer lr, float radius)
    {
        float angleStep = 360f / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }
}
