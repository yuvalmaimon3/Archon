using UnityEngine;

/// <summary>
/// ScriptableObject that holds the shared data for a single attack type.
/// Create instances via: Assets → Create → Arcon → Combat → Attack Definition.
///
/// Intentionally contains only common fields. Projectile-specific, melee-specific,
/// and contact-specific data will be added in later steps once this base shape is stable.
/// </summary>
[CreateAssetMenu(fileName = "NewAttack", menuName = "Arcon/Combat/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier for this attack. Used to reference it in code or logs.")]
    [SerializeField] private string attackId;

    [Tooltip("How this attack is delivered to its target.")]
    [SerializeField] private AttackType attackType;

    [Header("Stats")]
    [Tooltip("Base damage dealt on a successful hit.")]
    [Min(0)]
    [SerializeField] private int damage = 10;

    [Tooltip("Seconds between attacks. Lower = faster.")]
    [Min(0f)]
    [SerializeField] private float cooldown = 1f;

    [Tooltip("Maximum distance from attacker to target for this attack to be valid.")]
    [Min(0f)]
    [SerializeField] private float range = 5f;

    [Header("Projectile")]
    [Tooltip("Prefab spawned when this attack fires. Required when AttackType is Projectile.")]
    [SerializeField] private Projectile projectilePrefab;

    [Tooltip("Units per second the projectile travels.")]
    [Min(0f)]
    [SerializeField] private float projectileSpeed = 10f;

    [Header("Melee")]
    [Tooltip("Radius of the overlap sphere used to detect melee targets. Required when AttackType is Melee.")]
    [Min(0f)]
    [SerializeField] private float meleeRadius = 1.5f;

    [Header("Contact")]
    [Tooltip("Seconds between damage ticks while a target stays in contact. Required when AttackType is Contact.")]
    [Min(0.05f)]
    [SerializeField] private float contactTickInterval = 0.5f;

    [Header("Element")]
    [Tooltip("Element applied to the target on hit. None = non-elemental attack.")]
    [SerializeField] private ElementType elementType = ElementType.None;

    [Tooltip("Strength of the elemental application. Ignored if ElementType is None.")]
    [Min(0f)]
    [SerializeField] private float elementStrength = 1f;

    // ── Read-only properties ─────────────────────────────────────────────────

    /// <summary>Unique identifier for this attack.</summary>
    public string AttackId => attackId;

    /// <summary>How this attack is delivered.</summary>
    public AttackType AttackType => attackType;

    /// <summary>Base damage dealt on hit.</summary>
    public int Damage => damage;

    /// <summary>Seconds between consecutive uses of this attack.</summary>
    public float Cooldown => cooldown;

    /// <summary>Maximum valid distance from attacker to target.</summary>
    public float Range => range;

    /// <summary>Prefab spawned for projectile attacks. Null for non-projectile attacks.</summary>
    public Projectile ProjectilePrefab => projectilePrefab;

    /// <summary>Travel speed of the projectile in units per second.</summary>
    public float ProjectileSpeed => projectileSpeed;

    /// <summary>Radius of the overlap sphere used to detect melee targets.</summary>
    public float MeleeRadius => meleeRadius;

    /// <summary>Seconds between damage ticks for contact attacks.</summary>
    public float ContactTickInterval => contactTickInterval;

    /// <summary>Element applied to the target on hit.</summary>
    public ElementType ElementType => elementType;

    /// <summary>Strength of the elemental application.</summary>
    public float ElementStrength => elementStrength;
}
