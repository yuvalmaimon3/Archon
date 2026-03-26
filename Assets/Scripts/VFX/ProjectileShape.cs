/// <summary>
/// Visual archetype for projectile VFX. Defines the shape language and motion style
/// of the projectile particles — independent of which element colors it.
/// Any shape can be combined with any element for a unique visual.
/// </summary>
public enum ProjectileShape
{
    Arrow,        // Elongated, fast, narrow trail — a focused piercing shot
    Orb,          // Round glowing cluster, smooth trail — classic magic projectile
    Fireball,     // Large turbulent body, heavy trail with embers — powerful and weighty
    Shard,        // Angular spinning fragments — sharp and aggressive
    Bolt,         // Thin bright streak with jitter — quick and electric
    Spear,        // Long stretched body, focused trail — precise and forceful
    Needle,       // Tiny fast point, minimal trail — surgical precision
    WaveShot,     // Wide spreading pulse — area coverage
    BurstPellet,  // Small scattered particles — shotgun-like spread
    SpinningDisc  // Flat rotating ring of particles — slicing disc
}
