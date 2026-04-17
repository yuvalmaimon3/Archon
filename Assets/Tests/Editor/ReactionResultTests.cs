using NUnit.Framework;
using UnityEngine;

public class ReactionResultTests
{
    // ── NoReaction constant ──────────────────────────────────────────────────

    [Test]
    public void NoReaction_HasReactionIsFalse()
    {
        ReactionResult nr = ReactionResult.NoReaction;

        Assert.IsFalse(nr.HasReaction);
        Assert.AreEqual(ReactionType.None, nr.ReactionType);
        Assert.AreEqual(ReactionOutcomeType.None, nr.OutcomeType);
        Assert.AreEqual(ElementType.None, nr.ResultElement);
        Assert.AreEqual(0f, nr.ResultStrength);
        Assert.AreEqual(0, nr.BaseDamage);
        Assert.IsNull(nr.Source);
        Assert.IsFalse(nr.IsCritical);
    }

    // ── WithBaseDamage ───────────────────────────────────────────────────────

    [Test]
    public void WithBaseDamage_ReturnsCopyWithDamage()
    {
        ReactionResult original = MakeSampleReaction();
        ReactionResult copy = original.WithBaseDamage(50);

        Assert.AreEqual(50, copy.BaseDamage);
        Assert.AreEqual(0, original.BaseDamage); // original unchanged
        Assert.AreEqual(original.ReactionType, copy.ReactionType);
        Assert.AreEqual(original.HasReaction, copy.HasReaction);
    }

    // ── WithSource ───────────────────────────────────────────────────────────

    [Test]
    public void WithSource_ReturnsCopyWithSource()
    {
        var go = new GameObject("TestSource");
        ReactionResult original = MakeSampleReaction();
        ReactionResult copy = original.WithSource(go);

        Assert.AreEqual(go, copy.Source);
        Assert.IsNull(original.Source);
        Assert.AreEqual(original.ReactionType, copy.ReactionType);

        Object.DestroyImmediate(go);
    }

    // ── WithIsCritical ───────────────────────────────────────────────────────

    [Test]
    public void WithIsCritical_ReturnsCopyWithCritFlag()
    {
        ReactionResult original = MakeSampleReaction();
        ReactionResult copy = original.WithIsCritical(true);

        Assert.IsTrue(copy.IsCritical);
        Assert.IsFalse(original.IsCritical);
        Assert.AreEqual(original.BaseDamage, copy.BaseDamage);
    }

    // ── Chaining ─────────────────────────────────────────────────────────────

    [Test]
    public void CopyMethods_CanBeChained()
    {
        var go = new GameObject("ChainSource");
        ReactionResult result = MakeSampleReaction()
            .WithBaseDamage(100)
            .WithSource(go)
            .WithIsCritical(true);

        Assert.AreEqual(100, result.BaseDamage);
        Assert.AreEqual(go, result.Source);
        Assert.IsTrue(result.IsCritical);
        Assert.IsTrue(result.HasReaction);
        Assert.AreEqual(ReactionType.Frozen, result.ReactionType);

        Object.DestroyImmediate(go);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private ReactionResult MakeSampleReaction()
    {
        return new ReactionResult(
            hasReaction: true,
            reactionType: ReactionType.Frozen,
            outcomeType: ReactionOutcomeType.ClearAll,
            resultElement: ElementType.None,
            resultStrength: 0f
        );
    }
}
