using NUnit.Framework;

public class ReactionResolverTests
{
    private const float DefaultStrength = 1f;

    // ── Valid reactions — both orderings ─────────────────────────────────────

    [TestCase(ElementType.Water, ElementType.Ice,       ReactionType.Frozen)]
    [TestCase(ElementType.Ice,   ElementType.Water,     ReactionType.Frozen)]
    [TestCase(ElementType.Water, ElementType.Fire,      ReactionType.Boiling)]
    [TestCase(ElementType.Fire,  ElementType.Water,     ReactionType.Boiling)]
    [TestCase(ElementType.Ice,   ElementType.Fire,      ReactionType.ThermalShock)]
    [TestCase(ElementType.Fire,  ElementType.Ice,       ReactionType.ThermalShock)]
    [TestCase(ElementType.Water, ElementType.Lightning, ReactionType.Arc)]
    [TestCase(ElementType.Lightning, ElementType.Water, ReactionType.Arc)]
    [TestCase(ElementType.Ice,   ElementType.Lightning, ReactionType.Crack)]
    [TestCase(ElementType.Lightning, ElementType.Ice,   ReactionType.Crack)]
    [TestCase(ElementType.Fire,  ElementType.Lightning, ReactionType.Plasma)]
    [TestCase(ElementType.Lightning, ElementType.Fire,  ReactionType.Plasma)]
    public void Resolve_ValidPair_ReturnsCorrectReaction(
        ElementType existing, ElementType incoming, ReactionType expected)
    {
        ReactionResult result = ReactionResolver.Resolve(
            existing, DefaultStrength, incoming, DefaultStrength);

        Assert.IsTrue(result.HasReaction);
        Assert.AreEqual(expected, result.ReactionType);
    }

    [TestCase(ElementType.Water, ElementType.Ice)]
    [TestCase(ElementType.Water, ElementType.Fire)]
    [TestCase(ElementType.Ice,   ElementType.Fire)]
    [TestCase(ElementType.Water, ElementType.Lightning)]
    [TestCase(ElementType.Ice,   ElementType.Lightning)]
    [TestCase(ElementType.Fire,  ElementType.Lightning)]
    public void Resolve_ValidPair_OutcomeIsClearAll(
        ElementType existing, ElementType incoming)
    {
        ReactionResult result = ReactionResolver.Resolve(
            existing, DefaultStrength, incoming, DefaultStrength);

        Assert.AreEqual(ReactionOutcomeType.ClearAll, result.OutcomeType);
        Assert.AreEqual(ElementType.None, result.ResultElement);
        Assert.AreEqual(0f, result.ResultStrength);
    }

    // ── No reaction: None element ───────────────────────────────────────────

    [TestCase(ElementType.None, ElementType.Fire)]
    [TestCase(ElementType.Fire, ElementType.None)]
    [TestCase(ElementType.None, ElementType.None)]
    public void Resolve_NoneElement_ReturnsNoReaction(
        ElementType existing, ElementType incoming)
    {
        ReactionResult result = ReactionResolver.Resolve(
            existing, DefaultStrength, incoming, DefaultStrength);

        Assert.IsFalse(result.HasReaction);
        Assert.AreEqual(ReactionType.None, result.ReactionType);
    }

    // ── No reaction: same element ───────────────────────────────────────────

    [TestCase(ElementType.Fire)]
    [TestCase(ElementType.Water)]
    [TestCase(ElementType.Ice)]
    [TestCase(ElementType.Lightning)]
    [TestCase(ElementType.Wind)]
    [TestCase(ElementType.Earth)]
    public void Resolve_SameElement_ReturnsNoReaction(ElementType element)
    {
        ReactionResult result = ReactionResolver.Resolve(
            element, DefaultStrength, element, DefaultStrength);

        Assert.IsFalse(result.HasReaction);
    }

    // ── No reaction: non-reacting pairs ─────────────────────────────────────

    [TestCase(ElementType.Wind,  ElementType.Earth)]
    [TestCase(ElementType.Earth, ElementType.Wind)]
    [TestCase(ElementType.Wind,  ElementType.Fire)]
    [TestCase(ElementType.Fire,  ElementType.Wind)]
    [TestCase(ElementType.Wind,  ElementType.Water)]
    [TestCase(ElementType.Water, ElementType.Wind)]
    [TestCase(ElementType.Wind,  ElementType.Ice)]
    [TestCase(ElementType.Ice,   ElementType.Wind)]
    [TestCase(ElementType.Wind,  ElementType.Lightning)]
    [TestCase(ElementType.Lightning, ElementType.Wind)]
    [TestCase(ElementType.Earth, ElementType.Fire)]
    [TestCase(ElementType.Fire,  ElementType.Earth)]
    [TestCase(ElementType.Earth, ElementType.Water)]
    [TestCase(ElementType.Water, ElementType.Earth)]
    [TestCase(ElementType.Earth, ElementType.Ice)]
    [TestCase(ElementType.Ice,   ElementType.Earth)]
    [TestCase(ElementType.Earth, ElementType.Lightning)]
    [TestCase(ElementType.Lightning, ElementType.Earth)]
    public void Resolve_NonReactingPair_ReturnsNoReaction(
        ElementType existing, ElementType incoming)
    {
        ReactionResult result = ReactionResolver.Resolve(
            existing, DefaultStrength, incoming, DefaultStrength);

        Assert.IsFalse(result.HasReaction);
        Assert.AreEqual(ReactionType.None, result.ReactionType);
        Assert.AreEqual(ReactionOutcomeType.None, result.OutcomeType);
    }

    // ── Strength passthrough ────────────────────────────────────────────────

    [Test]
    public void Resolve_DifferentStrengths_StillReacts()
    {
        ReactionResult result = ReactionResolver.Resolve(
            ElementType.Water, 0.5f, ElementType.Fire, 2.0f);

        Assert.IsTrue(result.HasReaction);
        Assert.AreEqual(ReactionType.Boiling, result.ReactionType);
    }

    // ── Resolver does not set damage/source (those come from Health) ────────

    [Test]
    public void Resolve_DoesNotSetBaseDamageOrSource()
    {
        ReactionResult result = ReactionResolver.Resolve(
            ElementType.Ice, 1f, ElementType.Fire, 1f);

        Assert.AreEqual(0, result.BaseDamage);
        Assert.IsNull(result.Source);
        Assert.IsFalse(result.IsCritical);
    }
}
