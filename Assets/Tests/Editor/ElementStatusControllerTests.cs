using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ElementStatusControllerTests
{
    private GameObject _go;
    private ElementStatusController _controller;
    private GameObject _source;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestTarget");
        _controller = _go.AddComponent<ElementStatusController>();
        _source = new GameObject("TestSource");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        Object.DestroyImmediate(_source);
    }

    // Suppress Debug.Log from production code
    [OneTimeSetUp]
    public void SuppressLogs()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    // ── Single element application ──────────────────────────────────────────

    [TestCase(ElementType.Fire)]
    [TestCase(ElementType.Water)]
    [TestCase(ElementType.Ice)]
    [TestCase(ElementType.Lightning)]
    [TestCase(ElementType.Wind)]
    [TestCase(ElementType.Earth)]
    public void ApplyElement_SingleElement_StoresCorrectly(ElementType element)
    {
        Apply(element, 1.5f);

        Assert.AreEqual(element, _controller.CurrentElement);
        Assert.AreEqual(1.5f, _controller.CurrentStrength);
    }

    [Test]
    public void ApplyElement_StoresSource()
    {
        Apply(ElementType.Fire, 1f);

        Assert.AreEqual(_source, _controller.LastApplicationSource);
    }

    // ── Element replacement (no reaction) ───────────────────────────────────

    [Test]
    public void ApplyElement_SameElement_ReplacesWithoutReaction()
    {
        Apply(ElementType.Fire, 1f);
        Apply(ElementType.Fire, 2f);

        Assert.AreEqual(ElementType.Fire, _controller.CurrentElement);
        Assert.AreEqual(2f, _controller.CurrentStrength);
        Assert.AreEqual(ReactionType.None, _controller.LastReaction);
    }

    [Test]
    public void ApplyElement_NonReactingPair_ReplacesElement()
    {
        Apply(ElementType.Wind, 1f);
        Apply(ElementType.Earth, 1.5f);

        Assert.AreEqual(ElementType.Earth, _controller.CurrentElement);
        Assert.AreEqual(1.5f, _controller.CurrentStrength);
        Assert.AreEqual(ReactionType.None, _controller.LastReaction);
    }

    // ── Reaction triggering ─────────────────────────────────────────────────

    [TestCase(ElementType.Water, ElementType.Ice,       ReactionType.Frozen)]
    [TestCase(ElementType.Water, ElementType.Fire,      ReactionType.Boiling)]
    [TestCase(ElementType.Ice,   ElementType.Fire,      ReactionType.ThermalShock)]
    [TestCase(ElementType.Water, ElementType.Lightning, ReactionType.Arc)]
    [TestCase(ElementType.Ice,   ElementType.Lightning, ReactionType.Crack)]
    [TestCase(ElementType.Fire,  ElementType.Lightning, ReactionType.Plasma)]
    public void ApplyElement_ReactingPair_TriggersReaction(
        ElementType first, ElementType second, ReactionType expectedReaction)
    {
        Apply(first, 1f);
        Apply(second, 1f);

        Assert.AreEqual(expectedReaction, _controller.LastReaction);
    }

    [TestCase(ElementType.Ice,       ElementType.Water,     ReactionType.Frozen)]
    [TestCase(ElementType.Fire,      ElementType.Water,     ReactionType.Boiling)]
    [TestCase(ElementType.Fire,      ElementType.Ice,       ReactionType.ThermalShock)]
    [TestCase(ElementType.Lightning, ElementType.Water,     ReactionType.Arc)]
    [TestCase(ElementType.Lightning, ElementType.Ice,       ReactionType.Crack)]
    [TestCase(ElementType.Lightning, ElementType.Fire,      ReactionType.Plasma)]
    public void ApplyElement_ReactingPair_ReverseOrder_TriggersReaction(
        ElementType first, ElementType second, ReactionType expectedReaction)
    {
        Apply(first, 1f);
        Apply(second, 1f);

        Assert.AreEqual(expectedReaction, _controller.LastReaction);
    }

    // ── Post-reaction state (ClearAll) ──────────────────────────────────────

    [Test]
    public void ApplyElement_AfterReaction_StateIsCleared()
    {
        Apply(ElementType.Water, 1f);
        Apply(ElementType.Ice, 1f);

        Assert.AreEqual(ElementType.None, _controller.CurrentElement);
        Assert.AreEqual(0f, _controller.CurrentStrength);
    }

    // ── OnReactionTriggered event ───────────────────────────────────────────

    [Test]
    public void ApplyElement_Reaction_FiresOnReactionTriggered()
    {
        ReactionResult? captured = null;
        _controller.OnReactionTriggered += r => captured = r;

        Apply(ElementType.Fire, 1f);
        Apply(ElementType.Water, 1f, baseDamage: 25, isCritical: true);

        Assert.IsNotNull(captured);
        Assert.AreEqual(ReactionType.Boiling, captured.Value.ReactionType);
        Assert.AreEqual(25, captured.Value.BaseDamage);
        Assert.AreEqual(_source, captured.Value.Source);
        Assert.IsTrue(captured.Value.IsCritical);
    }

    [Test]
    public void ApplyElement_NoReaction_DoesNotFireOnReactionTriggered()
    {
        bool fired = false;
        _controller.OnReactionTriggered += _ => fired = true;

        Apply(ElementType.Wind, 1f);
        Apply(ElementType.Earth, 1f);

        Assert.IsFalse(fired);
    }

    // ── OnElementChanged event ──────────────────────────────────────────────

    [Test]
    public void ApplyElement_FiresOnElementChanged()
    {
        var history = new List<ElementType>();
        _controller.OnElementChanged += (el, _) => history.Add(el);

        Apply(ElementType.Fire, 1f);

        Assert.AreEqual(1, history.Count);
        Assert.AreEqual(ElementType.Fire, history[0]);
    }

    [Test]
    public void ApplyElement_Reaction_FiresOnElementChangedWithPostOutcome()
    {
        var history = new List<ElementType>();
        _controller.OnElementChanged += (el, _) => history.Add(el);

        Apply(ElementType.Water, 1f);   // event 1: Water
        Apply(ElementType.Ice, 1f);     // event 2: None (ClearAll)

        Assert.AreEqual(2, history.Count);
        Assert.AreEqual(ElementType.Water, history[0]);
        Assert.AreEqual(ElementType.None, history[1]); // cleared after reaction
    }

    // ── ClearElement ────────────────────────────────────────────────────────

    [Test]
    public void ClearElement_ResetsToNone()
    {
        Apply(ElementType.Fire, 1f);
        _controller.ClearElement();

        Assert.AreEqual(ElementType.None, _controller.CurrentElement);
        Assert.AreEqual(0f, _controller.CurrentStrength);
    }

    [Test]
    public void ClearElement_FiresOnElementChanged()
    {
        Apply(ElementType.Fire, 1f);

        ElementType? captured = null;
        _controller.OnElementChanged += (el, _) => captured = el;

        _controller.ClearElement();

        Assert.AreEqual(ElementType.None, captured);
    }

    // ── WouldReact ──────────────────────────────────────────────────────────

    [Test]
    public void WouldReact_ValidPair_ReturnsTrue()
    {
        Apply(ElementType.Water, 1f);

        Assert.IsTrue(_controller.WouldReact(ElementType.Fire));
    }

    [Test]
    public void WouldReact_NonReactingPair_ReturnsFalse()
    {
        Apply(ElementType.Wind, 1f);

        Assert.IsFalse(_controller.WouldReact(ElementType.Earth));
    }

    [Test]
    public void WouldReact_CurrentIsNone_ReturnsFalse()
    {
        Assert.IsFalse(_controller.WouldReact(ElementType.Fire));
    }

    [Test]
    public void WouldReact_IncomingIsNone_ReturnsFalse()
    {
        Apply(ElementType.Fire, 1f);

        Assert.IsFalse(_controller.WouldReact(ElementType.None));
    }

    [Test]
    public void WouldReact_DoesNotMutateState()
    {
        Apply(ElementType.Water, 1f);
        _controller.WouldReact(ElementType.Fire);

        // State should still be Water — WouldReact is read-only
        Assert.AreEqual(ElementType.Water, _controller.CurrentElement);
    }

    // ── Full reaction cycle: apply → react → reapply ────────────────────────

    [Test]
    public void FullCycle_ApplyReactThenApplyNewElement()
    {
        // Apply Water, then Fire → Boiling reaction, state clears
        Apply(ElementType.Water, 1f);
        Apply(ElementType.Fire, 1f);
        Assert.AreEqual(ElementType.None, _controller.CurrentElement);

        // Apply Lightning on clean state — should just store it
        Apply(ElementType.Lightning, 1f);
        Assert.AreEqual(ElementType.Lightning, _controller.CurrentElement);

        // Apply Water → Arc reaction
        Apply(ElementType.Water, 1f);
        Assert.AreEqual(ReactionType.Arc, _controller.LastReaction);
        Assert.AreEqual(ElementType.None, _controller.CurrentElement);
    }

    // ── All 6 reactions produce correct event data ──────────────────────────

    [TestCase(ElementType.Water,     ElementType.Ice,       ReactionType.Frozen)]
    [TestCase(ElementType.Water,     ElementType.Fire,      ReactionType.Boiling)]
    [TestCase(ElementType.Ice,       ElementType.Fire,      ReactionType.ThermalShock)]
    [TestCase(ElementType.Water,     ElementType.Lightning, ReactionType.Arc)]
    [TestCase(ElementType.Ice,       ElementType.Lightning, ReactionType.Crack)]
    [TestCase(ElementType.Fire,      ElementType.Lightning, ReactionType.Plasma)]
    public void AllReactions_EventCarriesDamageAndSource(
        ElementType first, ElementType second, ReactionType expected)
    {
        ReactionResult? captured = null;
        _controller.OnReactionTriggered += r => captured = r;

        Apply(first, 1f);
        Apply(second, 1f, baseDamage: 30);

        Assert.IsNotNull(captured);
        Assert.AreEqual(expected, captured.Value.ReactionType);
        Assert.AreEqual(30, captured.Value.BaseDamage);
        Assert.AreEqual(_source, captured.Value.Source);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private void Apply(ElementType element, float strength,
                       int baseDamage = 0, bool isCritical = false)
    {
        var app = new ElementApplication(element, strength, _source);
        _controller.ApplyElement(app, baseDamage, isCritical);
    }
}
