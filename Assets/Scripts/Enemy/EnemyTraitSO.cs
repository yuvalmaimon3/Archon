using UnityEngine;

// Abstract base for enemy trait ScriptableObjects.
// Concrete trait types (e.g. Regenerator, Poisonous, Shielded) derive from this
// and carry their own data + optional behavior hooks.
// Assign trait assets to EnemyData.SpecialTraits so each enemy type can declare
// its modifiers uniformly without bloating EnemyData itself.
public abstract class EnemyTraitSO : ScriptableObject
{
    [Header("Trait Identity")]
    [Tooltip("Display name shown in bestiary/UI.")]
    [SerializeField] private string traitName;

    [Tooltip("Short description of what this trait does.")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    public string TraitName   => traitName;
    public string Description => description;
}
