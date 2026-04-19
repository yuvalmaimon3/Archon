using System.Collections.Generic;
using UnityEngine;

// Contract for item stat handlers — one implementation per ItemEffectType.
public interface IItemEffectHandler
{
    void Apply(ItemStatModifier modifier, UpgradeContext ctx);
}

// Maps ItemEffectType → handler.
// To add a new item stat type:
// 1. Add enum value in ItemEnums.cs
// 2. Create a handler class implementing IItemEffectHandler
// 3. Register it in the dictionary below
public static class ItemEffectRegistry
{
    private static readonly Dictionary<ItemEffectType, IItemEffectHandler> Handlers = new()
    {
        { ItemEffectType.MaxHpFlat,          new ItemMaxHpFlatHandler() },
        { ItemEffectType.DamagePercent,       new ItemDamagePercentHandler() },
        { ItemEffectType.AttackSpeedPercent,  new ItemAttackSpeedPercentHandler() },
        { ItemEffectType.MoveSpeedFlat,       new ItemMoveSpeedFlatHandler() },
        { ItemEffectType.MoveSpeedPercent,    new ItemMoveSpeedPercentHandler() },
        { ItemEffectType.CritChance,          new ItemCritChanceHandler() },
        { ItemEffectType.CritDamage,          new ItemCritDamageHandler() },
    };

    public static bool TryApply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        if (Handlers.TryGetValue(modifier.effectType, out var handler))
        {
            handler.Apply(modifier, ctx);
            return true;
        }

        Debug.LogWarning($"[ItemEffectRegistry] No handler for: {modifier.effectType}");
        return false;
    }

    // Applies all stat modifiers from an item definition.
    public static void ApplyAll(ItemDefinition item, UpgradeContext ctx)
    {
        foreach (var modifier in item.statModifiers)
            TryApply(modifier, ctx);
    }
}

// ── Effect handlers ──────────────────────────────────────────────────────────

public class ItemMaxHpFlatHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        int newMax = ctx.Health.MaxHealth + Mathf.Max(1, (int)modifier.value);
        ctx.Health.AdjustMaxHealth(newMax);
        ctx.HealthSync?.UpdateSyncedMaxHealth(newMax);
        Debug.Log($"[Item] MaxHP +{(int)modifier.value} → {newMax}");
    }
}

public class ItemDamagePercentHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        foreach (var ac in ctx.AttackControllers)
        {
            float newMult = ac.DamageMultiplier * (1f + modifier.value);
            ac.SetDamageMultiplier(newMult);
            Debug.Log($"[Item] Damage ×{newMult:F3} on '{ac.gameObject.name}'");
        }
    }
}

public class ItemAttackSpeedPercentHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        foreach (var ac in ctx.AttackControllers)
        {
            float newMult = ac.CooldownMultiplier * (1f - modifier.value);
            ac.SetCooldownMultiplier(newMult);
            Debug.Log($"[Item] Attack speed +{modifier.value * 100f:F0}% on '{ac.gameObject.name}' → cooldown ×{newMult:F3}");
        }
    }
}

public class ItemMoveSpeedFlatHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        if (ctx.Movement == null) return;
        ctx.Movement.AddSpeedBonus(modifier.value);
        Debug.Log($"[Item] Move speed +{modifier.value}");
    }
}

public class ItemMoveSpeedPercentHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        if (ctx.Movement == null) return;
        ctx.Movement.AddSpeedMultiplier(modifier.value);
        Debug.Log($"[Item] Move speed +{modifier.value * 100f:F0}%");
    }
}

public class ItemCritChanceHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        if (ctx.CritHandler == null) return;
        ctx.CritHandler.AddCritChance(modifier.value);
        Debug.Log($"[Item] Crit chance +{modifier.value * 100f:F1}%");
    }
}

public class ItemCritDamageHandler : IItemEffectHandler
{
    public void Apply(ItemStatModifier modifier, UpgradeContext ctx)
    {
        if (ctx.CritHandler == null) return;
        ctx.CritHandler.AddCritDamage(modifier.value);
        Debug.Log($"[Item] Crit damage +{modifier.value * 100f:F0}%");
    }
}
