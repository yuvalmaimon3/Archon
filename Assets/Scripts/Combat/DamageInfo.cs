using UnityEngine;

/// <summary>
/// Immutable data payload describing a single damage event.
/// Passed by value — readonly ensures no field is mutated after creation.
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>Raw damage amount to subtract from health.</summary>
    public readonly int Amount;

    /// <summary>
    /// The GameObject responsible for this damage (player, projectile, trap, etc.).
    /// Useful for kill attribution, friendly-fire filtering, or score tracking.
    /// </summary>
    public readonly GameObject Source;

    /// <summary>World-space point where the hit landed. Used for VFX / impact effects.</summary>
    public readonly Vector3 HitPoint;

    /// <summary>
    /// Normalized direction the hit came from.
    /// Used for knockback, hit-reaction animations, or directional VFX.
    /// </summary>
    public readonly Vector3 HitDirection;

    public DamageInfo(int amount, GameObject source, Vector3 hitPoint, Vector3 hitDirection)
    {
        Amount       = amount;
        Source       = source;
        HitPoint     = hitPoint;
        HitDirection = hitDirection;
    }
}
