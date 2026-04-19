using System;
using UnityEngine;

// A single stat bonus an item grants while equipped.
// Items carry an array of these in ItemDefinition.statModifiers.
[Serializable]
public struct ItemStatModifier
{
    public ItemEffectType effectType;

    [Tooltip("Magnitude of the effect — meaning depends on effectType:\n" +
             "  MaxHpFlat          → flat HP (e.g. 20)\n" +
             "  DamagePercent      → fraction added to damage mult (e.g. 0.10 = +10%)\n" +
             "  AttackSpeedPercent → fraction by which cooldown shrinks (e.g. 0.20)\n" +
             "  MoveSpeedFlat      → flat units/sec (e.g. 1.0)\n" +
             "  MoveSpeedPercent   → fraction added multiplicatively (e.g. 0.10 = +10%)\n" +
             "  CritChance         → flat chance added (e.g. 0.10 = +10%)\n" +
             "  CritDamage         → flat bonus added (e.g. 0.25 = +25%)")]
    [Min(0f)]
    public float value;
}
