using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// PlayMode tests for the reaction system.
// Covers time-dependent and physics-dependent behavior that EditMode can't test:
//   - Element expiry (real Time.deltaTime)
//   - Element timer reset on re-application
//   - Arc chain reaction via Physics.OverlapSphere
//   - Frozen reaction coroutine timing (Animator.speed + AttackController.IsAttackBlocked as proxies)
//
// Each test creates its own scene objects (visible in Hierarchy during the run)
// and destroys them in TearDown for full isolation.
public class ReactionPlayModeTests
{
    private GameObject _camera;
    private GameObject _source;
    private GameObject _enemyA;
    private GameObject _enemyB;
    private Health _healthA;
    private Health _healthB;
    private ElementStatusController _escA;
    private ElementStatusController _escB;

    [OneTimeSetUp]
    public void SuppressLogs() => LogAssert.ignoreFailingMessages = true;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _camera  = new GameObject("TestCamera");
        _camera.AddComponent<Camera>();

        _source  = CreateSource("TestSource",  new Vector3(0f, 0f, -5f));
        _enemyA  = CreateEnemy("TestEnemyA",   Vector3.zero,              Color.blue);
        _enemyB  = CreateEnemy("TestEnemyB",   new Vector3(4f, 0f, 0f),   Color.red);

        _healthA = _enemyA.GetComponent<Health>();
        _healthB = _enemyB.GetComponent<Health>();
        _escA    = _enemyA.GetComponent<ElementStatusController>();
        _escB    = _enemyB.GetComponent<ElementStatusController>();

        yield return new WaitForFixedUpdate(); // Colliders register in physics
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.Destroy(_camera);
        Object.Destroy(_source);
        Object.Destroy(_enemyA);
        Object.Destroy(_enemyB);
        yield return null;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    // An element applied with no follow-up reaction should expire on its own after 8 seconds.
    [UnityTest]
    public IEnumerator Element_ExpiresAfterDuration()
    {
        _escA.ApplyElement(new ElementApplication(ElementType.Fire, 1f, _source));

        Assert.AreEqual(ElementType.Fire, _escA.CurrentElement, "Fire should be applied.");

        yield return new WaitForSeconds(9f); // ElementDuration is 8s

        Assert.AreEqual(ElementType.None, _escA.CurrentElement, "Fire should have expired.");
    }

    // Re-applying an element before it expires resets the 8-second countdown.
    [UnityTest]
    public IEnumerator Element_TimerResets_OnReapplication()
    {
        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));

        yield return new WaitForSeconds(6f); // Nearly expired

        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source)); // Reset timer

        yield return new WaitForSeconds(4f); // 10s total, but only 4s since reset

        Assert.AreEqual(ElementType.Water, _escA.CurrentElement,
            "Water should still be active — timer was reset at the 6s mark.");
    }

    // Arc reaction chains to all nearby wet enemies within arcAoeRadius (default 8 units).
    // TestEnemyB is 4 units away — well within range.
    [UnityTest]
    public IEnumerator ArcChain_DamagesNearbyWetEnemy()
    {
        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        _escB.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));

        // Water + Lightning = Arc on A → chain fires on nearby wet B
        // Arc multiplier = 1.5, so 20 * 1.5 = 30 damage on each
        Hit(_healthA, ElementType.Lightning, 20);

        yield return null; // Chain resolves synchronously, one frame to settle

        Assert.AreEqual(70, _healthA.CurrentHealth, "TestEnemyA: 100 - 30 (20 × 1.5 Arc).");
        Assert.AreEqual(70, _healthB.CurrentHealth, "TestEnemyB: 100 - 30 (Arc chain).");
    }

    // B is 10 units away — outside the 8-unit arcAoeRadius. Should not be chained.
    [UnityTest]
    public IEnumerator ArcChain_DoesNotHit_EnemyOutsideRadius()
    {
        _enemyB.transform.position = new Vector3(10f, 0f, 0f);
        yield return new WaitForFixedUpdate(); // physics picks up the new position

        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        _escB.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));

        Hit(_healthA, ElementType.Lightning, 20);
        yield return null;

        Assert.AreEqual(100, _healthB.CurrentHealth, "B is outside Arc radius — must not be chained.");
    }

    // B is not tagged 'Enemy'. Arc only chains to objects with that tag.
    [UnityTest]
    public IEnumerator ArcChain_DoesNotHit_WrongTag()
    {
        _enemyB.tag = "Untagged";

        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        _escB.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));

        Hit(_healthA, ElementType.Lightning, 20);
        yield return null;

        Assert.AreEqual(100, _healthB.CurrentHealth, "B is not tagged 'Enemy' — must not be chained.");
    }

    // B has Fire, not Water. Arc only chains to wet (Water) enemies.
    [UnityTest]
    public IEnumerator ArcChain_DoesNotHit_NonWetEnemy()
    {
        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        _escB.ApplyElement(new ElementApplication(ElementType.Fire,  1f, _source)); // not wet

        Hit(_healthA, ElementType.Lightning, 20);
        yield return null;

        Assert.AreEqual(100, _healthB.CurrentHealth, "B has Fire, not Water — must not be chained.");
    }

    // All wet enemies within range are chained — not just the first one found.
    // A at (0,0,0) chains to B at (4,0,0) and C at (2,0,0). Each takes 20 × 1.5 = 30 damage.
    [UnityTest]
    public IEnumerator ArcChain_HitsAllNearbyWetEnemies()
    {
        var enemyC  = CreateEnemy("TestEnemyC", new Vector3(2f, 0f, 0f), Color.yellow);
        var healthC = enemyC.GetComponent<Health>();
        var escC    = enemyC.GetComponent<ElementStatusController>();

        yield return new WaitForFixedUpdate(); // register C's collider

        _escA.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        _escB.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        escC.ApplyElement(new ElementApplication(ElementType.Water,  1f, _source));

        Hit(_healthA, ElementType.Lightning, 20);
        yield return null;

        Assert.AreEqual(70, _healthB.CurrentHealth, "B should be chained.");
        Assert.AreEqual(70, healthC.CurrentHealth,  "C should also be chained.");

        Object.Destroy(enemyC);
    }

    // FreezeCoroutine sets Animator.speed = 0 synchronously before its first yield.
    // Verifies the freeze begins immediately when the Frozen reaction fires.
    [UnityTest]
    public IEnumerator FrozenReaction_AnimatorSpeedDropsToZero()
    {
        // Animator must exist on the GameObject before ReactionDamageHandler.Start() runs
        // so it gets cached by CacheReferences(). Created here rather than in SetUp
        // because only Frozen tests need an Animator.
        var enemy    = CreateEnemy("TestEnemyFreeze", new Vector3(0f, 0f, 3f), Color.cyan, includeAnimator: true);
        var health   = enemy.GetComponent<Health>();
        var esc      = enemy.GetComponent<ElementStatusController>();
        var animator = enemy.GetComponent<Animator>();

        yield return new WaitForFixedUpdate(); // Start() runs → ReactionDamageHandler caches Animator

        esc.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        Hit(health, ElementType.Ice, 20); // Water + Ice = Frozen

        yield return null; // FreezeCoroutine executes up to its first yield

        Assert.AreEqual(0f, animator.speed, "Animator should be stopped while frozen.");

        Object.Destroy(enemy);
    }

    // After frozenDuration (default 2s) the coroutine resumes and restores Animator.speed to 1.
    [UnityTest]
    public IEnumerator FrozenReaction_AnimatorSpeedRestores_AfterDuration()
    {
        var enemy    = CreateEnemy("TestEnemyFreeze", new Vector3(0f, 0f, 3f), Color.cyan, includeAnimator: true);
        var health   = enemy.GetComponent<Health>();
        var esc      = enemy.GetComponent<ElementStatusController>();
        var animator = enemy.GetComponent<Animator>();

        yield return new WaitForFixedUpdate();

        esc.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        Hit(health, ElementType.Ice, 20); // Frozen → animator.speed = 0

        yield return new WaitForSeconds(2.5f); // frozenDuration is 2s — extra 0.5s buffer

        Assert.AreEqual(1f, animator.speed, "Animator speed should be restored after freeze ends.");

        Object.Destroy(enemy);
    }

    // FreezeCoroutine calls _attackController?.BlockAttacks() before its first yield.
    // AttackController is a MonoBehaviour — testable without a network session.
    [UnityTest]
    public IEnumerator FrozenReaction_AttackBlocked_DuringFreeze()
    {
        var enemy      = CreateEnemy("TestEnemyFreezeAttack", new Vector3(0f, 0f, 3f), Color.cyan,
                                     includeAnimator: true, includeAttackController: true);
        var health     = enemy.GetComponent<Health>();
        var esc        = enemy.GetComponent<ElementStatusController>();
        var attackCtrl = enemy.GetComponent<AttackController>();

        yield return new WaitForFixedUpdate(); // Start() runs → RDH caches AttackController

        esc.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        Hit(health, ElementType.Ice, 20); // Water + Ice = Frozen

        yield return null; // FreezeCoroutine runs to first yield

        Assert.IsTrue(attackCtrl.IsAttackBlocked, "Attacks should be blocked while frozen.");

        Object.Destroy(enemy);
    }

    // After frozenDuration (2s) the coroutine calls _attackController?.UnblockAttacks().
    [UnityTest]
    public IEnumerator FrozenReaction_AttackUnblocked_AfterFreeze()
    {
        var enemy      = CreateEnemy("TestEnemyFreezeAttack", new Vector3(0f, 0f, 3f), Color.cyan,
                                     includeAnimator: true, includeAttackController: true);
        var health     = enemy.GetComponent<Health>();
        var esc        = enemy.GetComponent<ElementStatusController>();
        var attackCtrl = enemy.GetComponent<AttackController>();

        yield return new WaitForFixedUpdate();

        esc.ApplyElement(new ElementApplication(ElementType.Water, 1f, _source));
        Hit(health, ElementType.Ice, 20); // Frozen → attacks blocked

        yield return new WaitForSeconds(2.5f); // frozenDuration = 2s, +0.5s buffer

        Assert.IsFalse(attackCtrl.IsAttackBlocked, "Attacks should be unblocked after freeze ends.");

        Object.Destroy(enemy);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Capsule enemy with all reaction components + colored visual for Scene view.
    // includeAnimator / includeAttackController: add before ReactionDamageHandler so Start() caches them.
    private static GameObject CreateEnemy(string goName, Vector3 position, Color color,
                                          bool includeAnimator = false,
                                          bool includeAttackController = false)
    {
        var go = new GameObject(goName);
        go.transform.position = position;
        go.tag = "Enemy";

        // Dependency order matters: Health.Awake caches ElementStatusController,
        // ReactionDamageHandler.Start() caches optional components if present.
        go.AddComponent<CapsuleCollider>();
        go.AddComponent<ElementStatusController>();
        go.AddComponent<Health>();
        if (includeAnimator)         go.AddComponent<Animator>();
        if (includeAttackController) go.AddComponent<AttackController>();
        go.AddComponent<ReactionDamageHandler>();

        AddVisual(go, PrimitiveType.Capsule, color);
        return go;
    }

    // Small sphere representing the attack source.
    private static GameObject CreateSource(string goName, Vector3 position)
    {
        var go = new GameObject(goName);
        go.transform.position = position;
        AddVisual(go, PrimitiveType.Sphere, Color.green);
        return go;
    }

    // Grabs a primitive mesh without leaving a GameObject in the scene.
    private static void AddVisual(GameObject go, PrimitiveType primitiveType, Color color)
    {
        var temp = GameObject.CreatePrimitive(primitiveType);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);

        go.AddComponent<MeshFilter>().mesh = mesh;

        var mr     = go.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat    = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        mr.material = mat;
    }

    private void Hit(Health health, ElementType element, int damage)
    {
        var app  = new ElementApplication(element, 1f, _source);
        var info = new DamageInfo(damage, _source, Vector3.zero, Vector3.forward, app);
        health.TakeDamage(info);
    }
}
