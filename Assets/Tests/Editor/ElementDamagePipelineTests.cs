using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools; // LogAssert

// Integration tests for the full element damage pipeline:
// DamageInfo → Health.TakeDamage → ElementStatusController → ReactionDamageHandler
//
// Tests the real component chain — Health suppresses direct damage on reactions,
// ReactionDamageHandler applies x2 multiplied damage, events propagate correctly.
//
// Component add order matters: Health.Awake caches ElementStatusController,
// ReactionDamageHandler.Awake caches both — add dependencies first.
public class ElementDamagePipelineTests
{
    private GameObject _target;
    private Health _health;
    private ElementStatusController _elementStatus;
    private ReactionDamageHandler _reactionHandler;
    private GameObject _source;

    [SetUp]
    public void SetUp()
    {
        _target = new GameObject("TestTarget");

        // Order matters: Health.Awake caches ElementStatusController,
        // ReactionDamageHandler.Awake/OnEnable caches both + subscribes to events.
        _elementStatus = _target.AddComponent<ElementStatusController>();
        _health = _target.AddComponent<Health>();
        _reactionHandler = _target.AddComponent<ReactionDamageHandler>();

        _health.SetMaxHealth(100);
        _source = new GameObject("TestSource");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_target);
        Object.DestroyImmediate(_source);
    }

    [OneTimeSetUp]
    public void SuppressLogs()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    // ── Normal hit (no reaction) ────────────────────────────────────────────

    [Test]
    public void NormalHit_AppliesDamageAndStoresElement()
    {
        Hit(ElementType.Fire, 1f, 20);

        Assert.AreEqual(80, _health.CurrentHealth);
        Assert.AreEqual(ElementType.Fire, _elementStatus.CurrentElement);
    }

    [Test]
    public void NormalHit_NoElement_AppliesDamageOnly()
    {
        var info = new DamageInfo(25, _source, Vector3.zero, Vector3.forward);
        _health.TakeDamage(info);

        Assert.AreEqual(75, _health.CurrentHealth);
        Assert.AreEqual(ElementType.None, _elementStatus.CurrentElement);
    }

    // ── Reaction damage suppression + multiplier ────────────────────────────

    [Test]
    public void ReactionHit_SuppressesDirectDamage_AppliesMultiplied()
    {
        // First hit: normal damage
        Hit(ElementType.Water, 1f, 20);
        Assert.AreEqual(80, _health.CurrentHealth);

        // Second hit triggers Boiling — direct damage suppressed, reaction = 30*2 = 60
        Hit(ElementType.Fire, 1f, 30);

        // 100 - 20 (first) - 60 (reaction only) = 20
        Assert.AreEqual(20, _health.CurrentHealth);
    }

    [Test]
    public void ReactionHit_OnlyReactionDamageApplied()
    {
        Hit(ElementType.Ice, 1f, 10);
        Assert.AreEqual(90, _health.CurrentHealth);

        // ThermalShock: reaction = 15 * 2 = 30 (direct 15 suppressed)
        Hit(ElementType.Fire, 1f, 15);

        Assert.AreEqual(60, _health.CurrentHealth);
    }

    // ── All 6 reactions through full pipeline ───────────────────────────────

    [TestCase(ElementType.Water, ElementType.Ice,       10, 25)] // Frozen
    [TestCase(ElementType.Water, ElementType.Fire,      10, 25)] // Boiling
    [TestCase(ElementType.Ice,   ElementType.Fire,      10, 25)] // ThermalShock
    [TestCase(ElementType.Water, ElementType.Lightning,  10, 25)] // Arc
    [TestCase(ElementType.Ice,   ElementType.Lightning,  10, 25)] // Crack
    [TestCase(ElementType.Fire,  ElementType.Lightning,  10, 25)] // Plasma
    public void AllReactions_CorrectDamageThrough_FullPipeline(
        ElementType first, ElementType second, int firstDmg, int secondDmg)
    {
        _health.SetMaxHealth(1000);

        Hit(first, 1f, firstDmg);
        int hpAfterFirst = 1000 - firstDmg;
        Assert.AreEqual(hpAfterFirst, _health.CurrentHealth);

        // Reaction: direct suppressed, reaction = secondDmg * 2
        Hit(second, 1f, secondDmg);
        int expected = hpAfterFirst - (secondDmg * 2);

        Assert.AreEqual(expected, _health.CurrentHealth);
        Assert.AreEqual(ElementType.None, _elementStatus.CurrentElement); // ClearAll
    }

    // ── Zero damage reaction ────────────────────────────────────────────────

    [Test]
    public void ZeroDamageReaction_SkipsReactionDamage()
    {
        Hit(ElementType.Water, 1f, 10);
        Assert.AreEqual(90, _health.CurrentHealth);

        // Reaction with 0 base → 0 * 2 = 0, ReactionDamageHandler skips
        Hit(ElementType.Fire, 1f, 0);

        Assert.AreEqual(90, _health.CurrentHealth);
    }

    // ── Death from reaction ─────────────────────────────────────────────────

    [Test]
    public void ReactionDamage_CanKill()
    {
        _health.SetMaxHealth(50);

        Hit(ElementType.Ice, 1f, 10);
        Assert.AreEqual(40, _health.CurrentHealth);

        // Crack: 25 * 2 = 50 → kills (40 HP left)
        Hit(ElementType.Lightning, 1f, 25);

        Assert.IsTrue(_health.IsDead);
        Assert.AreEqual(0, _health.CurrentHealth);
    }

    [Test]
    public void ReactionDamage_FiresOnDeathEvent()
    {
        _health.SetMaxHealth(30);
        bool deathFired = false;
        _health.OnDeath += _ => deathFired = true;

        Hit(ElementType.Water, 1f, 10);
        Hit(ElementType.Ice, 1f, 20); // Frozen: 20*2=40 → kills (20 HP left)

        Assert.IsTrue(deathFired);
    }

    // ── Post-reaction state ─────────────────────────────────────────────────

    [Test]
    public void AfterReaction_ElementStateIsCleared()
    {
        Hit(ElementType.Fire, 1f, 10);
        Hit(ElementType.Water, 1f, 10);

        Assert.AreEqual(ElementType.None, _elementStatus.CurrentElement);
        Assert.AreEqual(0f, _elementStatus.CurrentStrength);
    }

    // ── Multi-reaction cycle ────────────────────────────────────────────────

    [Test]
    public void FullCycle_MultipleReactionsInSequence()
    {
        _health.SetMaxHealth(500);

        // Round 1: Water + Fire = Boiling
        Hit(ElementType.Water, 1f, 10);
        Assert.AreEqual(490, _health.CurrentHealth);
        Hit(ElementType.Fire, 1f, 20);
        Assert.AreEqual(450, _health.CurrentHealth); // 490 - 40

        // Round 2: Ice + Lightning = Crack
        Hit(ElementType.Ice, 1f, 15);
        Assert.AreEqual(435, _health.CurrentHealth);
        Hit(ElementType.Lightning, 1f, 30);
        Assert.AreEqual(375, _health.CurrentHealth); // 435 - 60

        // Round 3: Fire + Lightning = Plasma
        Hit(ElementType.Fire, 1f, 10);
        Assert.AreEqual(365, _health.CurrentHealth);
        Hit(ElementType.Lightning, 1f, 25);
        Assert.AreEqual(315, _health.CurrentHealth); // 365 - 50
    }

    // ── OnAnyReactionDamage static event ────────────────────────────────────

    [Test]
    public void ReactionDamage_BroadcastsStaticEvent()
    {
        int capturedDamage = 0;
        GameObject capturedSource = null;
        void Handler(Vector3 _, int dmg, GameObject src)
        {
            capturedDamage = dmg;
            capturedSource = src;
        }

        ReactionDamageHandler.OnAnyReactionDamage += Handler;

        Hit(ElementType.Water, 1f, 10);
        Hit(ElementType.Lightning, 1f, 20); // Arc → 20*2=40

        Assert.AreEqual(40, capturedDamage);
        Assert.AreEqual(_source, capturedSource);

        ReactionDamageHandler.OnAnyReactionDamage -= Handler;
    }

    // ── Damage number events ────────────────────────────────────────────────

    [Test]
    public void NormalHit_FiresOnDamageTaken_WithCritFlag()
    {
        int capturedAmount = 0;
        bool capturedCrit = false;
        _health.OnDamageTaken += (amount, crit) =>
        {
            capturedAmount = amount;
            capturedCrit = crit;
        };

        Hit(ElementType.Wind, 1f, 35, isCritical: true);

        Assert.AreEqual(35, capturedAmount);
        Assert.IsTrue(capturedCrit);
    }

    [Test]
    public void ReactionHit_SuppressedDirect_DoesNotFireOnDamageTaken()
    {
        Hit(ElementType.Water, 1f, 10);

        int callCount = 0;
        _health.OnDamageTaken += (_, __) => callCount++;

        // Triggers reaction — direct suppressed, reaction damage fires OnDamageTaken once
        Hit(ElementType.Fire, 1f, 20);

        Assert.AreEqual(1, callCount); // only reaction damage, not direct
    }

    // ── Element expiration ─────────────────────────────────────────────────
    // Timer-based tests (8s expiration) require Play Mode for real Time.deltaTime.
    // The expiration logic is covered by the TestReactions scene for live validation.

    [Test]
    public void ClearElement_AfterManualClear_ResetsState()
    {
        Hit(ElementType.Fire, 1f, 5);
        Assert.AreEqual(ElementType.Fire, _elementStatus.CurrentElement);

        _elementStatus.ClearElement();

        Assert.AreEqual(ElementType.None, _elementStatus.CurrentElement);
        Assert.AreEqual(0f, _elementStatus.CurrentStrength);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private void Hit(ElementType element, float strength, int damage,
                     bool isCritical = false)
    {
        var app = new ElementApplication(element, strength, _source);
        var info = new DamageInfo(damage, _source, Vector3.zero, Vector3.forward, app, isCritical);
        _health.TakeDamage(info);
    }
}
