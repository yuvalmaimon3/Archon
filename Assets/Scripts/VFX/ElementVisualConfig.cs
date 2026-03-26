using UnityEngine;

/// <summary>
/// Static visual identity data for each element.
/// Defines color palettes, particle behavior modifiers, and gradient factories.
/// All VFX builders read from this to colorize and animate particles by element.
///
/// Kept as static code (not ScriptableObject) for fast iteration.
/// Convert to SO later if artists need Inspector tweaking.
/// </summary>
public static class ElementVisualConfig
{
    // ── Public data struct ─────────────────────────────────────────────────

    /// <summary>All visual parameters for a single element.</summary>
    public struct Config
    {
        public Color primary;         // Outer body / main color
        public Color secondary;       // Inner core / glow color
        public Color trailStart;      // Trail near the projectile
        public Color trailEnd;        // Trail at the fade-out end
        public Color hitFlash;        // Brief flash on impact

        public float noiseStrength;   // Particle turbulence intensity
        public float noiseFrequency;  // How fast the noise cycles
        public float gravityMod;      // Negative = rises, positive = falls
        public float emissionMult;    // Multiplier on base emission rate
    }

    // ── Lookup ─────────────────────────────────────────────────────────────

    /// <summary>Returns visual config for the given element.</summary>
    public static Config Get(ElementType element) => element switch
    {
        ElementType.Fire      => _fire,
        ElementType.Water     => _water,
        ElementType.Lightning => _lightning,
        ElementType.Ice       => _ice,
        _                     => _neutral
    };

    /// <summary>Returns the reaction burst color pair for a reaction type.</summary>
    public static (Color primary, Color glow) GetReactionColors(ReactionType reaction) => reaction switch
    {
        ReactionType.Vaporize      => (new Color(0.95f, 0.95f, 0.95f), new Color(1f, 0.7f, 0.3f)),     // white steam + warm glow
        ReactionType.Melt          => (new Color(1f, 0.5f, 0.15f),     new Color(1f, 1f, 0.9f)),        // orange flash → white heat
        ReactionType.Overload      => (new Color(1f, 0.55f, 0.05f),    new Color(1f, 0.95f, 0.3f)),     // explosive orange + yellow
        ReactionType.Freeze        => (new Color(0.3f, 0.75f, 1f),     new Color(0.85f, 1f, 1f)),       // icy blue + white frost
        ReactionType.Electrocharge => (new Color(0.2f, 0.55f, 1f),     new Color(1f, 1f, 0.6f)),        // blue surge + yellow sparks
        ReactionType.Superconduct  => (new Color(0.55f, 0.3f, 0.9f),   new Color(0.7f, 0.95f, 1f)),    // violet + cold cyan
        _                          => (Color.white, Color.white)
    };

    // ── Element configs ────────────────────────────────────────────────────

    // Fire: warm orange/yellow, flickering, rising
    private static readonly Config _fire = new Config
    {
        primary        = new Color(1.0f, 0.42f, 0.05f),   // deep orange
        secondary      = new Color(1.0f, 0.85f, 0.2f),    // yellow core
        trailStart     = new Color(1.0f, 0.55f, 0.1f, 0.9f),
        trailEnd       = new Color(1.0f, 0.25f, 0.0f, 0.0f),
        hitFlash       = new Color(1.0f, 0.9f, 0.4f),
        noiseStrength  = 0.25f,
        noiseFrequency = 3.0f,
        gravityMod     = -0.08f,   // embers rise
        emissionMult   = 1.2f
    };

    // Water: cool blue/cyan, smooth and flowing
    private static readonly Config _water = new Config
    {
        primary        = new Color(0.1f, 0.45f, 1.0f),    // deep blue
        secondary      = new Color(0.35f, 0.8f, 1.0f),    // bright cyan
        trailStart     = new Color(0.2f, 0.55f, 1.0f, 0.85f),
        trailEnd       = new Color(0.1f, 0.3f, 0.8f, 0.0f),
        hitFlash       = new Color(0.5f, 0.85f, 1.0f),
        noiseStrength  = 0.1f,
        noiseFrequency = 1.5f,
        gravityMod     = 0.06f,    // droplets fall slightly
        emissionMult   = 1.0f
    };

    // Lightning: electric yellow/white, jittery and sharp
    private static readonly Config _lightning = new Config
    {
        primary        = new Color(1.0f, 0.92f, 0.2f),    // electric yellow
        secondary      = new Color(1.0f, 1.0f, 0.85f),    // near-white
        trailStart     = new Color(1.0f, 0.95f, 0.4f, 0.95f),
        trailEnd       = new Color(0.8f, 0.75f, 0.2f, 0.0f),
        hitFlash       = new Color(1.0f, 1.0f, 0.9f),
        noiseStrength  = 0.5f,     // high jitter — crackling feel
        noiseFrequency = 8.0f,     // fast noise cycles
        gravityMod     = 0.0f,
        emissionMult   = 1.1f
    };

    // Ice: cold cyan/white, minimal noise, crystalline
    private static readonly Config _ice = new Config
    {
        primary        = new Color(0.45f, 0.85f, 1.0f),   // light cyan
        secondary      = new Color(0.88f, 0.98f, 1.0f),   // near-white frost
        trailStart     = new Color(0.5f, 0.9f, 1.0f, 0.85f),
        trailEnd       = new Color(0.3f, 0.6f, 0.9f, 0.0f),
        hitFlash       = new Color(0.9f, 1.0f, 1.0f),
        noiseStrength  = 0.04f,    // almost still — frozen feel
        noiseFrequency = 0.5f,
        gravityMod     = 0.0f,
        emissionMult   = 0.9f
    };

    // Neutral fallback for ElementType.None
    private static readonly Config _neutral = new Config
    {
        primary        = new Color(0.8f, 0.8f, 0.8f),
        secondary      = Color.white,
        trailStart     = new Color(0.9f, 0.9f, 0.9f, 0.7f),
        trailEnd       = new Color(0.7f, 0.7f, 0.7f, 0f),
        hitFlash       = Color.white,
        noiseStrength  = 0.1f,
        noiseFrequency = 2f,
        gravityMod     = 0f,
        emissionMult   = 1f
    };

    // ── Gradient helpers ───────────────────────────────────────────────────

    /// <summary>Builds a body-particle color-over-lifetime gradient from the element config.</summary>
    public static Gradient MakeBodyGradient(Config cfg)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(cfg.secondary, 0f),
                new GradientColorKey(cfg.primary, 0.3f),
                new GradientColorKey(cfg.primary, 0.7f),
                new GradientColorKey(cfg.primary * 0.6f, 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.1f),
                new GradientAlphaKey(0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return g;
    }

    /// <summary>Builds a trail color-over-lifetime gradient from the element config.</summary>
    public static Gradient MakeTrailGradient(Config cfg)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(cfg.trailStart, 0f),
                new GradientColorKey(cfg.trailEnd, 1f)
            },
            new[] {
                new GradientAlphaKey(cfg.trailStart.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return g;
    }
}
