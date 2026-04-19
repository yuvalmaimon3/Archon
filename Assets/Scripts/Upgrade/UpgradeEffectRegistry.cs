using System.Collections.Generic;
using UnityEngine;

// Player components available to upgrade and item effect handlers.
// Created by PlayerUpgradeHandler (and later PlayerEquipment) and passed into each handler on apply.
public class UpgradeContext
{
    public GameObject GameObject;
    public Health Health;
    public NetworkHealthSync HealthSync;
    public AttackController[] AttackControllers;
    public PlayerMovement Movement;
    public PlayerProjectileModifiers ProjectileModifiers;
    public PlayerCritHandler CritHandler; // used by item crit effect handlers
}

// Contract for upgrade effect handlers — one implementation per UpgradeEffectType.
public interface IUpgradeEffectHandler
{
    void Apply(UpgradeDefinition upgrade, UpgradeContext ctx);
}

// Maps UpgradeEffectType → handler. To add a new upgrade type:
// 1. Add the enum value in UpgradeDefinition.cs
// 2. Create a handler class implementing IUpgradeEffectHandler
// 3. Register it in the dictionary below
public static class UpgradeEffectRegistry
{
    private static readonly Dictionary<UpgradeEffectType, IUpgradeEffectHandler> Handlers = new()
    {
        { UpgradeEffectType.MaxHpFlat,          new MaxHpFlatHandler() },
        { UpgradeEffectType.HealPercent,         new HealPercentHandler() },
        { UpgradeEffectType.DamagePercent,       new DamagePercentHandler() },
        { UpgradeEffectType.MoveSpeedFlat,       new MoveSpeedFlatHandler() },
        { UpgradeEffectType.MoveSpeedPercent,    new MoveSpeedPercentHandler() },
        { UpgradeEffectType.AttackSpeedPercent,   new AttackSpeedPercentHandler() },
        { UpgradeEffectType.ProjectileSplit,      new ProjectileSplitHandler() },
        { UpgradeEffectType.BlastReaction,        new BlastReactionHandler() },
        { UpgradeEffectType.LifeSteal,            new LifeStealHandler() },
    };

    public static bool TryApply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        if (Handlers.TryGetValue(upgrade.effectType, out var handler))
        {
            handler.Apply(upgrade, ctx);
            return true;
        }

        Debug.LogWarning($"[UpgradeEffectRegistry] No handler for effect type: {upgrade.effectType}");
        return false;
    }
}

// ── Effect handlers ──────────────────────────────────────────────────────────

public class MaxHpFlatHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        int newMax = ctx.Health.MaxHealth + Mathf.Max(1, (int)upgrade.value);
        ctx.Health.AdjustMaxHealth(newMax);
        ctx.HealthSync?.UpdateSyncedMaxHealth(newMax);
        Debug.Log($"[Upgrade] MaxHP +{(int)upgrade.value} → {newMax}");
    }
}

public class HealPercentHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        int healAmount = Mathf.RoundToInt(ctx.Health.MaxHealth * upgrade.value);
        ctx.Health.Heal(healAmount);
        Debug.Log($"[Upgrade] Healed {healAmount} HP ({upgrade.value * 100f:F0}% of max)");
    }
}

public class DamagePercentHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        foreach (var ac in ctx.AttackControllers)
        {
            float newMult = ac.DamageMultiplier * (1f + upgrade.value);
            ac.SetDamageMultiplier(newMult);
            Debug.Log($"[Upgrade] Damage ×{newMult:F3} on '{ac.gameObject.name}'");
        }
    }
}

public class MoveSpeedFlatHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        if (ctx.Movement == null) return;

        ctx.Movement.AddSpeedBonus(upgrade.value);
        Debug.Log($"[Upgrade] Move speed +{upgrade.value}");
    }
}

public class MoveSpeedPercentHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        if (ctx.Movement == null) return;

        ctx.Movement.AddSpeedMultiplier(upgrade.value);
        Debug.Log($"[Upgrade] Move speed +{upgrade.value * 100f:F0}%");
    }
}

public class AttackSpeedPercentHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        // Compounded multiplicatively: 20% faster twice → 1.0 × 0.80 × 0.80 = 0.64
        foreach (var ac in ctx.AttackControllers)
        {
            float newMult = ac.CooldownMultiplier * (1f - upgrade.value);
            ac.SetCooldownMultiplier(newMult);
            Debug.Log($"[Upgrade] Attack speed +{upgrade.value * 100f:F0}% on '{ac.gameObject.name}' " +
                      $"→ cooldown ×{newMult:F3} (effective: {ac.EffectiveCooldown:F2}s)");
        }
    }
}

public class ProjectileSplitHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        if (ctx.ProjectileModifiers == null)
            ctx.ProjectileModifiers = ctx.GameObject.AddComponent<PlayerProjectileModifiers>();

        ctx.ProjectileModifiers.SplitOnHit = true;
        ctx.ProjectileModifiers.SplitAngleDegrees = upgrade.value;
        Debug.Log($"[Upgrade] Shotgun split enabled — angle:{upgrade.value}°");
    }
}

public class BlastReactionHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        if (upgrade.effectPrefab == null ||
            !upgrade.effectPrefab.TryGetComponent<ReactionExplosion>(out _))
        {
            Debug.LogError("[Upgrade] BlastReaction upgrade has no ReactionExplosion prefab assigned.");
            return;
        }

        var blast = ctx.GameObject.GetComponent<BlastReactionUpgradeEffect>()
                    ?? ctx.GameObject.AddComponent<BlastReactionUpgradeEffect>();

        blast.SetConfig(upgrade.effectPrefab.GetComponent<ReactionExplosion>(), upgrade.value);
        Debug.Log($"[Upgrade] Blast Reaction enabled — radius:{upgrade.value}u");
    }
}

public class LifeStealHandler : IUpgradeEffectHandler
{
    public void Apply(UpgradeDefinition upgrade, UpgradeContext ctx)
    {
        if (ctx.ProjectileModifiers == null)
            ctx.ProjectileModifiers = ctx.GameObject.AddComponent<PlayerProjectileModifiers>();

        ctx.ProjectileModifiers.LifeSteal = true;
        ctx.ProjectileModifiers.LifeStealFraction = upgrade.value;
        Debug.Log($"[Upgrade] Life steal enabled — {upgrade.value * 100f:F0}% per hit");
    }
}
