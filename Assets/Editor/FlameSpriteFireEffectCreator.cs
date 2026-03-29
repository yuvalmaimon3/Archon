using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a looping campfire-style fire using <c>Assets/Effects/images/flame sprite.png</c> on every layer
/// (core, outer flicker, embers, soft smoke) plus a warm point light. Saves a prefab under Assets/Effects/Prefabs.
/// Menu: Effects / Create Flame Sprite Fire
/// </summary>
public static class FlameSpriteFireEffectCreator
{
    private const string FlameSpritePath = "Assets/Effects/images/flame sprite.png";
    private const string MatFolder       = "Assets/Effects/Materials";
    private const string PrefabPath    = "Assets/Effects/Prefabs/FX_FlameSpriteFire.prefab";

    [MenuItem("Effects/Create Flame Sprite Fire")]
    public static void Create()
    {
        EnsureFolder("Assets/Effects");
        EnsureFolder("Assets/Effects/Materials");
        EnsureFolder("Assets/Effects/Prefabs");

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(FlameSpritePath) == null)
        {
            Debug.LogError($"[FlameSpriteFire] Missing texture at '{FlameSpritePath}'. Import the flame sprite first.");
            return;
        }

        // Idempotent: remove previous preview object with same name in the active scene
        var existing = GameObject.Find("FX_FlameSpriteFire");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var root = BuildFireHierarchy();
        root.transform.position = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;

        Debug.Log($"[FlameSpriteFire] Created '{root.name}' and saved prefab to '{PrefabPath}'.");
    }

    /// <summary>Assembles child particle systems and light under one root.</summary>
    private static GameObject BuildFireHierarchy()
    {
        var root = new GameObject("FX_FlameSpriteFire");
        AddPointLight(root, new Color(1f, 0.45f, 0.08f), 2.8f, 5f, new Vector3(0f, 0.35f, 0f));

        SetupFireCore(ChildPS(root, "Fire_Core"));
        SetupFireOuter(ChildPS(root, "Fire_Outer"));
        SetupFireEmbers(ChildPS(root, "Fire_Embers"));
        SetupFireSmoke(ChildPS(root, "Fire_Smoke"));
        return root;
    }

    private static GameObject ChildPS(GameObject parent, string childName)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<ParticleSystem>();
        return go;
    }

    private static void AddPointLight(GameObject root, Color color, float intensity, float range, Vector3 localPos)
    {
        var lg = new GameObject("FX_Light");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = localPos;
        var lt = lg.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = color;
        lt.intensity = intensity;
        lt.range = range;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            var folder = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    // ── Materials (URP Particles Unlit; fallback to legacy) ─────────────────

    /// <summary>Loads or creates additive particle material; updates in place so GUIDs stay stable if the menu runs again.</summary>
    private static Material MakeAdditiveMat(string matName, Color tint)
    {
        var assetPath = $"{MatFolder}/{matName}.mat";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlameSpritePath);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Additive");
            mat = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(mat, assetPath);
        }

        var sh = mat.shader;
        if (sh.name.StartsWith("Universal"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", 1f);
            mat.SetFloat("_DstBlend", 1f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetColor("_BaseColor", tint);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
        }
        else
            mat.SetColor("_TintColor", tint);

        if (tex != null)
            mat.mainTexture = tex;

        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Loads or creates alpha-blended particle material for the dark smoke pass.</summary>
    private static Material MakeAlphaMat(string matName, Color tint)
    {
        var assetPath = $"{MatFolder}/{matName}.mat";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlameSpritePath);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Alpha Blended");
            mat = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(mat, assetPath);
        }

        if (mat.shader.name.StartsWith("Universal"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", 5f);
            mat.SetFloat("_DstBlend", 10f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetColor("_BaseColor", tint);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
        }

        if (tex != null)
            mat.mainTexture = tex;

        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Gradients / curves ───────────────────────────────────────────────────

    private static Gradient FireGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0f), 0.28f),
                new GradientColorKey(new Color(0.9f, 0.15f, 0f), 0.65f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    private static AnimationCurve Bell(float peak = 0.38f)
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(peak, 1f),
            new Keyframe(1f, 0f, -2f, 0f));
    }

    private static AnimationCurve DecayToZero()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -1f, 0f));
    }

    // ── Layers ───────────────────────────────────────────────────────────────

    /// <summary>Bright core: tight cone, turbulence, trails — reads as the “body” of the fire.</summary>
    private static void SetupFireCore(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.4f), new Color(1f, 0.35f, 0f));
        main.gravityModifier = -0.08f;
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 32f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 14f;
        sh.radius = 0.14f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FireGradient());

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, Bell(0.36f));

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.y = new ParticleSystem.MinMaxCurve(1.1f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.42f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 0.4f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.28f);
        trails.minVertexDistance = 0.08f;
        trails.dieWithParticles = true;
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(
            new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.45f, 0f), 0f),
                    new GradientColorKey(new Color(0.35f, 0.05f, 0f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.75f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            });
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, DecayToZero());

        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = MakeAdditiveMat("FlameSprite_Fire_Core_mat", new Color(1f, 0.55f, 0.12f, 1f));
        r.trailMaterial = MakeAdditiveMat("FlameSprite_Fire_CoreTrail_mat", new Color(1f, 0.3f, 0.05f, 1f));
        r.sortingFudge = -2f;
    }

    /// <summary>Softer outer sheet for volume and flicker.</summary>
    private static void SetupFireOuter(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.45f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.15f, 0.85f), new Color(1f, 0.25f, 0f, 0.65f));
        main.gravityModifier = -0.05f;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 14f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 22f;
        sh.radius = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FireGradient());

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, Bell(0.42f));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;

        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = MakeAdditiveMat("FlameSprite_Fire_Outer_mat", new Color(1f, 0.4f, 0.08f, 0.75f));
        r.sortingFudge = -1f;
    }

    /// <summary>Small fast particles — sparks that still read the same sprite.</summary>
    private static void SetupFireEmbers(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.8f, 0.2f), new Color(1f, 0.2f, 0f));
        main.gravityModifier = 0.35f;
        main.maxParticles = 70;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 10f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 38f;
        sh.radius = 0.1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(
            new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.25f), 0f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 0.7f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.18f);
        trails.minVertexDistance = 0.04f;
        trails.dieWithParticles = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, DecayToZero());

        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = MakeAdditiveMat("FlameSprite_Fire_Ember_mat", new Color(1f, 0.65f, 0.1f, 1f));
        r.trailMaterial = MakeAdditiveMat("FlameSprite_Fire_EmberTrail_mat", new Color(1f, 0.35f, 0f, 1f));
    }

    /// <summary>Dark, slow alpha layer — same silhouette as smoke wisps.</summary>
    private static void SetupFireSmoke(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startDelay = new ParticleSystem.MinMaxCurve(0.15f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.22f, 0.2f, 0.2f, 0.35f),
            new Color(0.1f, 0.1f, 0.1f, 0.2f));
        main.gravityModifier = -0.04f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 4.5f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 18f;
        sh.radius = 0.16f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(
            new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.32f, 0.3f), 0f),
                    new GradientColorKey(new Color(0.12f, 0.12f, 0.12f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.45f, 0.2f),
                    new GradientAlphaKey(0.25f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1.85f));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.18f;

        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = MakeAlphaMat("FlameSprite_Fire_Smoke_mat", new Color(0.25f, 0.23f, 0.23f, 0.4f));
        r.sortingFudge = 3f;
    }
}
