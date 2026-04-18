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

    [Tooltip("Random variation per hit. 0 = fixed. 0.10 = ±10%. Final roll: [damage×(1−v), damage×(1+v)].")]
    [Range(0f, 1f)]
    [SerializeField] private float damageVariance = 0f;

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

    [Tooltip("Tag of GameObjects this projectile can hit. Set explicitly per attack: 'Enemy' for player attacks, 'Player' for enemy attacks.")]
    [SerializeField] private string projectileTargetTag;

    [Header("Melee")]
    [Tooltip("Radius of the overlap sphere used to detect melee targets. Required when AttackType is Melee.")]
    [Min(0f)]
    [SerializeField] private float meleeRadius = 1.5f;

    [Header("Contact")]
    [Tooltip("Seconds between damage ticks while a target stays in contact. Required when AttackType is Contact.")]
    [Min(0.05f)]
    [SerializeField] private float contactTickInterval = 0.5f;

    [Header("Summoning")]
    [Tooltip("Enemy prefab to summon. Must have EnemyInitializer. Must have NetworkObject for multiplayer.")]
    [SerializeField] private GameObject summoningPrefab;

    [Tooltip("How many enemies to summon per attack.")]
    [Min(1)]
    [SerializeField] private int summoningCount = 1;

    [Tooltip("Radius around the summoner in which spawns are scattered.")]
    [Min(0f)]
    [SerializeField] private float summoningSpreadRadius = 3f;

    [Tooltip("Level applied to each summoned enemy via EnemyInitializer.")]
    [Min(1)]
    [SerializeField] private int summoningMinionLevel = 1;

    [Tooltip("Max search radius passed to NavMesh.SamplePosition to find a valid spawn point near the candidate.")]
    [Min(0.5f)]
    [SerializeField] private float summoningNavMeshSearchRadius = 2f;

    [Header("CallDown")]
    [Tooltip("Prefab spawned at each strike location. Required when AttackType is CallDown.")]
    [SerializeField] private CallDownZone callDownZonePrefab;

    [Tooltip("Seconds the warning indicator is shown before the strike lands.")]
    [Min(0.1f)]
    [SerializeField] private float callDownWarnDuration = 1.5f;

    [Tooltip("Radius of the AOE damage sphere when the strike lands.")]
    [Min(0f)]
    [SerializeField] private float callDownAoeRadius = 2f;

    [Tooltip("Number of strike zones spawned per attack. 1 = single hit; >1 = first at targetPosition + rest scattered.")]
    [Min(1)]
    [SerializeField] private int callDownTargetCount = 1;

    [Tooltip("How far additional zones scatter from the first strike position when TargetCount > 1.")]
    [Min(0f)]
    [SerializeField] private float callDownSpreadRadius = 2f;

    [Tooltip("Tag of GameObjects the strike damages. 'Player' for enemy attacks; 'Enemy' for player attacks.")]
    [SerializeField] private string callDownTargetTag;

    [Header("Element")]
    [Tooltip("Element applied to the target on hit. None = non-elemental attack.")]
    [SerializeField] private ElementType elementType = ElementType.None;

    [Tooltip("Strength of the elemental application. Ignored if ElementType is None.")]
    [Min(0f)]
    [SerializeField] private float elementStrength = 1f;

    [Header("Presentation")]
    [Tooltip("Animator trigger name played when this attack fires. Leave blank for none.")]
    [SerializeField] private string attackAnimationTrigger;

    [Tooltip("Sound played when this attack fires.")]
    [SerializeField] private AudioClip attackSound;

    // ── Read-only properties ─────────────────────────────────────────────────

    /// <summary>Unique identifier for this attack.</summary>
    public string AttackId => attackId;

    /// <summary>How this attack is delivered.</summary>
    public AttackType AttackType => attackType;

    /// <summary>Base damage dealt on hit.</summary>
    public int Damage => damage;

    /// <summary>Fractional variance per damage roll. 0 = fixed. 0.10 = ±10%.</summary>
    public float DamageVariance => damageVariance;

    /// <summary>Seconds between consecutive uses of this attack.</summary>
    public float Cooldown => cooldown;

    /// <summary>Maximum valid distance from attacker to target.</summary>
    public float Range => range;

    /// <summary>Prefab spawned for projectile attacks. Null for non-projectile attacks.</summary>
    public Projectile ProjectilePrefab => projectilePrefab;

    /// <summary>Travel speed of the projectile in units per second.</summary>
    public float ProjectileSpeed => projectileSpeed;

    /// <summary>Tag of GameObjects this projectile can hit. Player attacks: "Enemy"; enemy attacks: "Player".</summary>
    public string ProjectileTargetTag => projectileTargetTag;

    /// <summary>Radius of the overlap sphere used to detect melee targets.</summary>
    public float MeleeRadius => meleeRadius;

    /// <summary>Enemy prefab spawned on each Summoning attack.</summary>
    public GameObject SummoningPrefab => summoningPrefab;

    /// <summary>How many enemies to summon per attack.</summary>
    public int SummoningCount => summoningCount;

    /// <summary>Scatter radius around the summoner for spawn positions.</summary>
    public float SummoningSpreadRadius => summoningSpreadRadius;

    /// <summary>Level applied to each summoned enemy via EnemyInitializer.</summary>
    public int SummoningMinionLevel => summoningMinionLevel;

    /// <summary>Max search radius for NavMesh.SamplePosition when finding a valid spawn point.</summary>
    public float SummoningNavMeshSearchRadius => summoningNavMeshSearchRadius;

    /// <summary>Prefab spawned at each strike location for CallDown attacks.</summary>
    public CallDownZone CallDownZonePrefab => callDownZonePrefab;

    /// <summary>Seconds the warning indicator is shown before the strike lands.</summary>
    public float CallDownWarnDuration => callDownWarnDuration;

    /// <summary>Radius of the AOE damage sphere when the strike lands.</summary>
    public float CallDownAoeRadius => callDownAoeRadius;

    /// <summary>Number of strike zones spawned per attack.</summary>
    public int CallDownTargetCount => callDownTargetCount;

    /// <summary>Scatter radius for additional zones when TargetCount > 1.</summary>
    public float CallDownSpreadRadius => callDownSpreadRadius;

    /// <summary>Tag of GameObjects the strike damages.</summary>
    public string CallDownTargetTag => callDownTargetTag;

    /// <summary>Seconds between damage ticks for contact attacks.</summary>
    public float ContactTickInterval => contactTickInterval;

    /// <summary>Element applied to the target on hit.</summary>
    public ElementType ElementType => elementType;

    /// <summary>Strength of the elemental application.</summary>
    public float ElementStrength => elementStrength;

    /// <summary>Animator trigger name played when this attack fires. Empty string means none.</summary>
    public string AttackAnimationTrigger => attackAnimationTrigger;

    /// <summary>Sound played when this attack fires.</summary>
    public AudioClip AttackSound => attackSound;
}
