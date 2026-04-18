using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// PlayMode tests for the reaction system.
// Covers time-dependent and physics-dependent behavior that EditMode can't test:
//   - Element expiry (real Time.deltaTime)
//   - Element timer reset on re-application
//   - Arc chain reaction via Physics.OverlapSphere
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Capsule enemy with all reaction components + colored visual for Scene view.
    private static GameObject CreateEnemy(string goName, Vector3 position, Color color)
    {
        var go = new GameObject(goName);
        go.transform.position = position;
        go.tag = "Enemy";

        // Dependency order matches Awake caching in Health and ReactionDamageHandler
        go.AddComponent<CapsuleCollider>();
        go.AddComponent<ElementStatusController>();
        go.AddComponent<Health>();
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
