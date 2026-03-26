using UnityEngine;

/// <summary>
/// Static shape/motion data for each of the 10 projectile archetypes.
/// Defines particle counts, sizes, trail widths, render modes, and motion quirks.
/// Element colors are applied on top of these structural settings by the builder.
/// </summary>
public static class ProjectileArchetypeConfig
{
    // ── Public data struct ─────────────────────────────────────────────────

    /// <summary>All structural parameters for a single projectile archetype.</summary>
    public struct Config
    {
        // Body particles
        public int   maxParticles;          // Cap for body PS
        public float emissionRate;          // Particles per second
        public float startSizeMin;          // Min start size
        public float startSizeMax;          // Max start size
        public float startLifetime;         // Seconds before particle dies
        public float startSpeedMin;         // Min local start speed (spread from shape)
        public float startSpeedMax;         // Max local start speed
        public ParticleSystemShapeType shapeType;
        public float shapeRadius;           // Sphere/cone radius
        public float shapeAngle;            // Cone angle (ignored for sphere)
        public ParticleSystemRenderMode renderMode;
        public float stretchSpeed;          // For StretchedBillboard: speed-based stretch
        public float stretchLength;         // For StretchedBillboard: length-based stretch

        // Trail (built-in trail module on body particles)
        public bool  useTrail;
        public float trailLifetime;         // Trail length in seconds
        public float trailWidthStart;
        public float trailWidthEnd;

        // Motion modifiers (applied on top of element noise)
        public float noiseMultiplier;       // Multiplied with element noiseStrength
        public float orbitalY;              // Orbital velocity for spinning effects
        public float gravityMultiplier;     // Multiplied with element gravityMod

        // Secondary emitter (embers, sparks, spray behind the projectile)
        public bool  hasSecondary;
        public int   secondaryMax;
        public float secondaryRate;
        public float secondarySize;
        public float secondarySpeed;
        public float secondaryLifetime;
        public float secondaryGravity;
    }

    // ── Lookup ─────────────────────────────────────────────────────────────

    /// <summary>Returns archetype config for the given shape.</summary>
    public static Config Get(ProjectileShape shape) => shape switch
    {
        ProjectileShape.Arrow        => _arrow,
        ProjectileShape.Orb          => _orb,
        ProjectileShape.Fireball     => _fireball,
        ProjectileShape.Shard        => _shard,
        ProjectileShape.Bolt         => _bolt,
        ProjectileShape.Spear        => _spear,
        ProjectileShape.Needle       => _needle,
        ProjectileShape.WaveShot     => _waveShot,
        ProjectileShape.BurstPellet  => _burstPellet,
        ProjectileShape.SpinningDisc => _spinningDisc,
        _                            => _orb
    };

    // ── Archetype definitions ──────────────────────────────────────────────

    // Arrow: elongated, fast, narrow trail — focused piercing shot
    private static readonly Config _arrow = new Config
    {
        maxParticles   = 8,   emissionRate    = 20f,
        startSizeMin   = 0.06f, startSizeMax  = 0.12f,
        startLifetime  = 0.25f, startSpeedMin = 0f, startSpeedMax = 0.3f,
        shapeType      = ParticleSystemShapeType.Cone,
        shapeRadius    = 0.03f, shapeAngle = 5f,
        renderMode     = ParticleSystemRenderMode.Stretch,
        stretchSpeed   = 0.15f, stretchLength = 1.5f,
        useTrail       = true, trailLifetime = 0.12f, trailWidthStart = 0.06f, trailWidthEnd = 0f,
        noiseMultiplier = 0.5f, orbitalY = 0f, gravityMultiplier = 0.5f,
        hasSecondary = false
    };

    // Orb: round glowing cluster — classic magic projectile
    private static readonly Config _orb = new Config
    {
        maxParticles   = 15,  emissionRate    = 18f,
        startSizeMin   = 0.08f, startSizeMax  = 0.16f,
        startLifetime  = 0.4f, startSpeedMin = 0.1f, startSpeedMax = 0.4f,
        shapeType      = ParticleSystemShapeType.Sphere,
        shapeRadius    = 0.12f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Billboard,
        stretchSpeed   = 0f, stretchLength = 0f,
        useTrail       = true, trailLifetime = 0.15f, trailWidthStart = 0.04f, trailWidthEnd = 0f,
        noiseMultiplier = 1.0f, orbitalY = 0f, gravityMultiplier = 1.0f,
        hasSecondary = false
    };

    // Fireball: large turbulent body, heavy trail with embers
    private static readonly Config _fireball = new Config
    {
        maxParticles   = 25,  emissionRate    = 28f,
        startSizeMin   = 0.12f, startSizeMax  = 0.25f,
        startLifetime  = 0.5f, startSpeedMin = 0.2f, startSpeedMax = 0.6f,
        shapeType      = ParticleSystemShapeType.Sphere,
        shapeRadius    = 0.18f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Billboard,
        stretchSpeed   = 0f, stretchLength = 0f,
        useTrail       = true, trailLifetime = 0.25f, trailWidthStart = 0.08f, trailWidthEnd = 0f,
        noiseMultiplier = 1.5f, orbitalY = 0f, gravityMultiplier = 1.2f,
        // Embers trailing behind
        hasSecondary   = true, secondaryMax = 12, secondaryRate = 10f,
        secondarySize  = 0.04f, secondarySpeed = 0.5f,
        secondaryLifetime = 0.6f, secondaryGravity = 0.3f
    };

    // Shard: angular spinning fragments — sharp and aggressive
    private static readonly Config _shard = new Config
    {
        maxParticles   = 10,  emissionRate    = 15f,
        startSizeMin   = 0.06f, startSizeMax  = 0.14f,
        startLifetime  = 0.35f, startSpeedMin = 0.3f, startSpeedMax = 0.7f,
        shapeType      = ParticleSystemShapeType.Cone,
        shapeRadius    = 0.08f, shapeAngle = 15f,
        renderMode     = ParticleSystemRenderMode.Stretch,
        stretchSpeed   = 0.1f, stretchLength = 0.8f,
        useTrail       = true, trailLifetime = 0.1f, trailWidthStart = 0.03f, trailWidthEnd = 0f,
        noiseMultiplier = 0.8f, orbitalY = 3f, gravityMultiplier = 0.6f,
        hasSecondary = false
    };

    // Bolt: thin bright streak with jitter — quick and electric
    private static readonly Config _bolt = new Config
    {
        maxParticles   = 8,   emissionRate    = 25f,
        startSizeMin   = 0.04f, startSizeMax  = 0.08f,
        startLifetime  = 0.15f, startSpeedMin = 0f, startSpeedMax = 0.2f,
        shapeType      = ParticleSystemShapeType.SingleSidedEdge,
        shapeRadius    = 0.08f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Stretch,
        stretchSpeed   = 0.2f, stretchLength = 2f,
        useTrail       = true, trailLifetime = 0.1f, trailWidthStart = 0.03f, trailWidthEnd = 0f,
        noiseMultiplier = 2.0f, orbitalY = 0f, gravityMultiplier = 0f,
        hasSecondary = false
    };

    // Spear: long stretched body, focused trail — precise and forceful
    private static readonly Config _spear = new Config
    {
        maxParticles   = 10,  emissionRate    = 22f,
        startSizeMin   = 0.05f, startSizeMax  = 0.1f,
        startLifetime  = 0.3f, startSpeedMin = 0f, startSpeedMax = 0.2f,
        shapeType      = ParticleSystemShapeType.Cone,
        shapeRadius    = 0.04f, shapeAngle = 3f,
        renderMode     = ParticleSystemRenderMode.Stretch,
        stretchSpeed   = 0.2f, stretchLength = 2.5f,
        useTrail       = true, trailLifetime = 0.18f, trailWidthStart = 0.05f, trailWidthEnd = 0f,
        noiseMultiplier = 0.3f, orbitalY = 0f, gravityMultiplier = 0.3f,
        hasSecondary = false
    };

    // Needle: tiny fast point, minimal trail — surgical precision
    private static readonly Config _needle = new Config
    {
        maxParticles   = 5,   emissionRate    = 15f,
        startSizeMin   = 0.03f, startSizeMax  = 0.05f,
        startLifetime  = 0.12f, startSpeedMin = 0f, startSpeedMax = 0.1f,
        shapeType      = ParticleSystemShapeType.Sphere,
        shapeRadius    = 0.02f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Stretch,
        stretchSpeed   = 0.25f, stretchLength = 3f,
        useTrail       = true, trailLifetime = 0.06f, trailWidthStart = 0.02f, trailWidthEnd = 0f,
        noiseMultiplier = 0.4f, orbitalY = 0f, gravityMultiplier = 0f,
        hasSecondary = false
    };

    // Wave Shot: wide spreading pulse — area coverage
    private static readonly Config _waveShot = new Config
    {
        maxParticles   = 20,  emissionRate    = 22f,
        startSizeMin   = 0.08f, startSizeMax  = 0.18f,
        startLifetime  = 0.4f, startSpeedMin = 0.5f, startSpeedMax = 1.2f,
        shapeType      = ParticleSystemShapeType.Hemisphere,
        shapeRadius    = 0.2f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Billboard,
        stretchSpeed   = 0f, stretchLength = 0f,
        useTrail       = true, trailLifetime = 0.15f, trailWidthStart = 0.05f, trailWidthEnd = 0f,
        noiseMultiplier = 0.8f, orbitalY = 0f, gravityMultiplier = 0.8f,
        hasSecondary = false
    };

    // Burst Pellet: small scattered particles — shotgun-like spread
    private static readonly Config _burstPellet = new Config
    {
        maxParticles   = 15,  emissionRate    = 20f,
        startSizeMin   = 0.04f, startSizeMax  = 0.08f,
        startLifetime  = 0.3f, startSpeedMin = 0.8f, startSpeedMax = 1.5f,
        shapeType      = ParticleSystemShapeType.Sphere,
        shapeRadius    = 0.15f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Billboard,
        stretchSpeed   = 0f, stretchLength = 0f,
        useTrail       = true, trailLifetime = 0.08f, trailWidthStart = 0.02f, trailWidthEnd = 0f,
        noiseMultiplier = 1.2f, orbitalY = 0f, gravityMultiplier = 1.0f,
        hasSecondary = false
    };

    // Spinning Disc: flat rotating ring of particles — slicing disc
    private static readonly Config _spinningDisc = new Config
    {
        maxParticles   = 12,  emissionRate    = 18f,
        startSizeMin   = 0.06f, startSizeMax  = 0.1f,
        startLifetime  = 0.35f, startSpeedMin = 0f, startSpeedMax = 0.1f,
        shapeType      = ParticleSystemShapeType.Circle,
        shapeRadius    = 0.18f, shapeAngle = 0f,
        renderMode     = ParticleSystemRenderMode.Billboard,
        stretchSpeed   = 0f, stretchLength = 0f,
        useTrail       = true, trailLifetime = 0.2f, trailWidthStart = 0.04f, trailWidthEnd = 0f,
        noiseMultiplier = 0.3f, orbitalY = 6f, gravityMultiplier = 0f,
        hasSecondary = false
    };
}
