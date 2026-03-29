using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that populates the "lab effects" scene with 5 professional particle effects.
/// Run via menu: Effects > Create Lab Effects
///
/// Effects created:
///   1. FX_HellFire        – Multi-layer fire with embers, smoke, and warm point light
///   2. FX_ChainLightning  – Ribbon-trail electric bolts with glow and sparks
///   3. FX_ArcaneVortex    – Orbital magic vortex with a glowing core
///   4. FX_Explosion       – Burst explosion with flash, fire, debris, and smoke
///   5. FX_FrostStorm      – Ice crystal storm with frost dust and falling snowflakes
/// </summary>
public static class LabEffectsCreator
{
    // ── Paths ─────────────────────────────────────────────────────────────────
    private const string ScenePath      = "Assets/lab effects.unity";
    private const string MatFolder      = "Assets/Materials/Effects";

    // Professional VFX textures already in the project
    private const string TexFire        = "Assets/Vefects/Free Fire VFX/Textures/T_VFX_Fire_Mask_01.tga";
    private const string TexSmoke       = "Assets/Vefects/Free Fire VFX/Textures/T_VFX_Smoke_01.tga";
    private const string TexAsh         = "Assets/Vefects/Free Fire VFX/Textures/T_VFX_Ash_01.tga";
    private const string TexNoise       = "Assets/Vefects/Free Fire VFX/Textures/T_VFX_Noise_01.tga";
    private const string TexDust        = "Assets/Vefects/Free Fire VFX/Textures/T_VFX_Dust_01.tga";
    private const string TexSoftParticle = "Assets/UnityTechnologies/EffectExamples/FireExplosionEffects/Textures/RoundSoftParticle.tif";
    private const string TexLightning   = "Assets/UnityTechnologies/EffectExamples/FireExplosionEffects/Textures/LightningParticle.tif";

    // User-provided sprites
    private const string TexLightPoint  = "Assets/Sprites/UI/light point.png";

    // ── Entry Point ───────────────────────────────────────────────────────────

    [MenuItem("Effects/Create Lab Effects")]
    public static void CreateLabEffects()
    {
        EnsureSceneOpen();
        EnsureFolder("Assets/Materials");
        EnsureFolder(MatFolder);

        // Remove any previously created effects so the menu item is idempotent
        DestroyIfExists("FX_HellFire");
        DestroyIfExists("FX_ChainLightning");
        DestroyIfExists("FX_ArcaneVortex");
        DestroyIfExists("FX_Explosion");
        DestroyIfExists("FX_FrostStorm");

        // Create all 5 effects spread along the X axis
        BuildHellFire(new Vector3(-8f, 0f, 0f));
        BuildChainLightning(new Vector3(-4f, 0f, 0f));
        BuildArcaneVortex(new Vector3(0f, 0f, 0f));
        BuildExplosion(new Vector3(4f, 0f, 0f));
        BuildFrostStorm(new Vector3(8f, 0f, 0f));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[LabEffects] Created 5 effects: HellFire · ChainLightning · ArcaneVortex · Explosion · FrostStorm");
    }

    // ── Scene / Folder Helpers ────────────────────────────────────────────────

    private static void EnsureSceneOpen()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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

    private static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    // ── Material Factory ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates and saves an additive URP particle material.
    /// Falls back to legacy Particles/Additive if URP is unavailable.
    /// </summary>
    private static Material MakeAdditiveMat(string matName, Color color, string texPath = null)
    {
        var assetPath = $"{MatFolder}/{matName}.mat";

        // Delete stale asset so we always start fresh
        if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Additive");

        var mat = new Material(shader) { name = matName };

        if (shader.name.StartsWith("Universal"))
        {
            // URP: transparent additive surface
            mat.SetFloat("_Surface", 1f);      // Transparent
            mat.SetFloat("_Blend",   2f);      // Additive
            mat.SetFloat("_SrcBlend", 1f);     // One
            mat.SetFloat("_DstBlend", 1f);     // One
            mat.SetFloat("_ZWrite",   0f);
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
        }
        else
        {
            // Legacy fallback
            mat.SetColor("_TintColor", color);
        }

        // Assign texture if provided and found
        if (texPath != null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
                mat.mainTexture = tex;
        }

        mat.renderQueue = 3000;
        AssetDatabase.CreateAsset(mat, assetPath);
        return mat;
    }

    /// <summary>Creates an alpha-blended (non-additive) URP particle material for smoke / debris.</summary>
    private static Material MakeAlphaMat(string matName, Color color, string texPath = null)
    {
        var assetPath = $"{MatFolder}/{matName}.mat";

        if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Alpha Blended");

        var mat = new Material(shader) { name = matName };

        if (shader.name.StartsWith("Universal"))
        {
            mat.SetFloat("_Surface",  1f);     // Transparent
            mat.SetFloat("_Blend",    0f);     // Alpha
            mat.SetFloat("_SrcBlend", 5f);     // SrcAlpha
            mat.SetFloat("_DstBlend", 10f);    // OneMinusSrcAlpha
            mat.SetFloat("_ZWrite",   0f);
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
        }

        if (texPath != null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
                mat.mainTexture = tex;
        }

        mat.renderQueue = 3000;
        AssetDatabase.CreateAsset(mat, assetPath);
        return mat;
    }

    // ── Gradient / Curve Helpers ──────────────────────────────────────────────

    /// <summary>Builds a Gradient from parallel color and alpha arrays.</summary>
    private static Gradient Grad(
        (Color c, float t)[] cols,
        (float a, float t)[] alps)
    {
        var g  = new Gradient();
        var ck = new GradientColorKey[cols.Length];
        var ak = new GradientAlphaKey[alps.Length];

        for (int i = 0; i < cols.Length; i++)
            ck[i] = new GradientColorKey(cols[i].c, cols[i].t);

        for (int i = 0; i < alps.Length; i++)
            ak[i] = new GradientAlphaKey(alps[i].a, alps[i].t);

        g.SetKeys(ck, ak);
        return g;
    }

    /// <summary>Simple ramp up-then-down bell curve (0→1→0).</summary>
    private static AnimationCurve Bell(float peak = 0.4f)
        => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(peak, 1f),
            new Keyframe(1f, 0f, -2f, 0f));

    /// <summary>Easing-out curve that starts at 1 and decays to a low value.</summary>
    private static AnimationCurve Decay(float end = 0.1f)
        => new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(1f, end, -1f, 0f));

    // ── Shared GameObject Factory ─────────────────────────────────────────────

    /// <summary>Creates a child GameObject with a ParticleSystem attached.</summary>
    private static GameObject ChildPS(GameObject parent, string childName)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<ParticleSystem>();
        return go;
    }

    /// <summary>Adds a coloured Point Light as a child of root.</summary>
    private static Light AddPointLight(GameObject root, Color color, float intensity, float range,
                                        Vector3 localPos = default)
    {
        var lg  = new GameObject("FX_Light");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = localPos;
        var lt  = lg.AddComponent<Light>();
        lt.type      = LightType.Point;
        lt.color     = color;
        lt.intensity = intensity;
        lt.range     = range;
        return lt;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECT 1 – HELL FIRE
    // Three-layer fire: core flame + embers + rising smoke with a warm point light
    // ═══════════════════════════════════════════════════════════════════════════

    private static void BuildHellFire(Vector3 pos)
    {
        var root = new GameObject("FX_HellFire");
        root.transform.position = pos;

        // Warm orange-red point light simulates fire illumination on surroundings
        AddPointLight(root, new Color(1f, 0.42f, 0.07f), 3.5f, 6f, new Vector3(0f, 1f, 0f));

        SetupFlameCore  (ChildPS(root, "Flame_Core"));
        SetupFlameEmbers(ChildPS(root, "Flame_Embers"));
        SetupFlameSmoke (ChildPS(root, "Flame_Smoke"));
    }

    private static void SetupFlameCore(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        // ── Main ──
        var main = ps.main;
        main.loop              = true;
        main.duration          = 5f;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize         = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startRotation     = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor        = new ParticleSystem.MinMaxGradient(
                                     new Color(1f, 0.9f, 0f), new Color(1f, 0.3f, 0f));
        main.gravityModifier   = -0.06f;  // Slight upward bias
        main.maxParticles      = 200;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;

        // ── Emission ──
        var em = ps.emission;
        em.enabled       = true;
        em.rateOverTime  = 28f;

        // ── Shape: tight upward cone ──
        var sh = ps.shape;
        sh.enabled    = true;
        sh.shapeType  = ParticleSystemShapeType.Cone;
        sh.angle      = 12f;
        sh.radius     = 0.18f;

        // ── Color over lifetime: yellow → orange → dark red → transparent ──
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 1f, 0.2f), 0f),
                    (new Color(1f, 0.55f, 0f), 0.3f),
                    (new Color(0.85f, 0.1f, 0f), 0.7f),
                    (new Color(0.15f, 0.05f, 0f), 1f) },
            new[] { (0f, 0f), (1f, 0.08f), (0.9f, 0.55f), (0f, 1f) }));

        // ── Size over lifetime: grow then taper ──
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, Bell(0.35f));

        // ── Velocity over lifetime: extra upward push ──
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.y       = new ParticleSystem.MinMaxCurve(0.8f);

        // ── Noise: organic turbulence ──
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.38f;
        noise.frequency   = 0.85f;
        noise.scrollSpeed = 0.45f;
        noise.damping     = true;
        noise.octaveCount = 2;
        noise.quality     = ParticleSystemNoiseQuality.Medium;

        // ── Trails: glowing fire wisps ──
        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.45f;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.35f);
        trails.minVertexDistance = 0.1f;
        trails.dieWithParticles  = true;
        trails.colorOverTrail    = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 0.5f, 0f), 0f), (new Color(0.5f, 0.05f, 0f), 1f) },
            new[] { (0.7f, 0f), (0f, 1f) }));
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f, Decay(0f));

        // ── Renderer ──
        r.renderMode    = ParticleSystemRenderMode.Billboard;
        r.material      = MakeAdditiveMat("Flame_Core_Mat",  new Color(1f, 0.5f, 0.1f), TexFire);
        r.trailMaterial = MakeAdditiveMat("Flame_Trail_Mat", new Color(1f, 0.25f, 0f));
        r.sortingFudge  = -1f;
    }

    private static void SetupFlameEmbers(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 0.75f, 0f), new Color(1f, 0.25f, 0f));
        main.gravityModifier = 0.3f;     // Sparks arc then fall
        main.maxParticles    = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 9f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 35f;
        sh.radius    = 0.12f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 0.85f, 0.2f), 0f),
                    (new Color(1f, 0.25f, 0f), 0.45f),
                    (new Color(0.4f, 0.08f, 0f), 1f) },
            new[] { (1f, 0f), (0.9f, 0.4f), (0f, 1f) }));

        // Trails: short bright streaks
        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.75f;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.2f);
        trails.minVertexDistance = 0.05f;
        trails.dieWithParticles  = true;
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f, Decay(0f));

        r.material      = MakeAdditiveMat("Ember_Mat",       new Color(1f, 0.6f, 0f), TexAsh);
        r.trailMaterial = MakeAdditiveMat("Ember_Trail_Mat", new Color(1f, 0.3f, 0f));
    }

    private static void SetupFlameSmoke(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.startDelay      = new ParticleSystem.MinMaxCurve(0.3f);
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.3f, 0.3f, 0.3f, 0.45f),
                                   new Color(0.12f, 0.12f, 0.12f, 0.2f));
        main.gravityModifier = -0.05f;
        main.maxParticles    = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 5f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 15f;
        sh.radius    = 0.18f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.45f, 0.4f, 0.38f), 0f),
                    (new Color(0.22f, 0.22f, 0.22f), 0.5f),
                    (new Color(0.08f, 0.08f, 0.08f), 1f) },
            new[] { (0f, 0f), (0.45f, 0.12f), (0.3f, 0.65f), (0f, 1f) }));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 2f));

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.22f;
        noise.frequency   = 0.35f;
        noise.scrollSpeed = 0.2f;

        r.material     = MakeAlphaMat("Smoke_Mat", new Color(0.3f, 0.3f, 0.3f, 0.4f), TexSmoke);
        r.sortingFudge = 2f;   // Render smoke behind the bright flame layers
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECT 2 – CHAIN LIGHTNING
    // Two ribbon-trail electric bolts + ambient electric glow
    // ═══════════════════════════════════════════════════════════════════════════

    private static void BuildChainLightning(Vector3 pos)
    {
        var root = new GameObject("FX_ChainLightning");
        root.transform.position = pos;

        // Cool blue-white light
        AddPointLight(root, new Color(0.5f, 0.8f, 1f), 2.5f, 5f);

        // Two bolts at different orientations create the "chain" look
        SetupLightningBolt(ChildPS(root, "Bolt_Primary"),   0f);
        SetupLightningBolt(ChildPS(root, "Bolt_Secondary"), 75f);
        SetupLightningGlow(ChildPS(root, "Electric_Glow"));
    }

    private static void SetupLightningBolt(GameObject go, float yRotationDeg)
    {
        go.transform.localRotation = Quaternion.Euler(0f, yRotationDeg, 0f);

        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(65f, 110f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.85f, 0.97f, 1f), new Color(0.55f, 0.8f, 1f));
        main.gravityModifier = 0f;
        main.maxParticles    = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 460f;

        // Very narrow cone so particles shoot in a focused beam
        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 7f;
        sh.radius    = 0.001f;

        // Speed decelerates → particles cluster near origin = denser at base
        var vel = ps.velocityOverLifetime;
        vel.enabled       = true;
        vel.space         = ParticleSystemSimulationSpace.Local;
        vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, Decay(0.25f));

        // Size tapers to zero at the tip of the bolt
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, Decay(0.05f));

        // Noise creates the jagged, chaotic lightning shape
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.45f;
        noise.frequency   = 6f;     // High frequency = many sharp bends
        noise.scrollSpeed = 2.5f;
        noise.damping     = false;
        noise.quality     = ParticleSystemNoiseQuality.High;

        // Ribbon trail connects all particles into a continuous bolt line
        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.Ribbon;
        trails.ribbonCount       = 1;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(1f);
        trails.minVertexDistance = 0.02f;
        trails.dieWithParticles  = true;
        trails.colorOverTrail    = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 1f, 1f), 0f),
                    (new Color(0.65f, 0.92f, 1f), 0.5f),
                    (new Color(0.25f, 0.55f, 1f), 1f) },
            new[] { (1f, 0f), (0.9f, 0.4f), (0f, 1f) }));
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.4f, 0.5f), new Keyframe(1f, 0f)));

        // Render mode None = only ribbon trail is visible, not the particle quads
        r.renderMode    = ParticleSystemRenderMode.None;
        r.trailMaterial = MakeAdditiveMat("Lightning_Trail_Mat",
                              new Color(0.7f, 0.9f, 1f), TexLightning);
    }

    private static void SetupLightningGlow(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.12f, 0.55f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.6f, 0.9f, 1f, 0.6f),
                                   new Color(0.3f, 0.6f, 1f, 0.25f));
        main.gravityModifier = 0f;
        main.maxParticles    = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 28f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.55f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.75f, 0.97f, 1f), 0f), (new Color(0.3f, 0.65f, 1f), 1f) },
            new[] { (0f, 0f), (0.85f, 0.2f), (0f, 1f) }));

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.3f;
        noise.frequency   = 2.2f;
        noise.scrollSpeed = 1.2f;

        r.material = MakeAdditiveMat("Lightning_Glow_Mat", new Color(0.5f, 0.8f, 1f, 0.5f), TexLightPoint);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECT 3 – ARCANE VORTEX
    // Orbiting particles using velocityOverLifetime.orbitalY + core glow + sparks
    // ═══════════════════════════════════════════════════════════════════════════

    private static void BuildArcaneVortex(Vector3 pos)
    {
        var root = new GameObject("FX_ArcaneVortex");
        root.transform.position = pos;

        // Mystical purple light
        AddPointLight(root, new Color(0.7f, 0.3f, 1f), 2.5f, 5.5f);

        SetupVortexOrbit (ChildPS(root, "Vortex_Orbit"));
        SetupVortexCore  (ChildPS(root, "Vortex_Core"));
        SetupMagicSparks (ChildPS(root, "Magic_Sparks"));
    }

    private static void SetupVortexOrbit(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0f, 0.15f); // Orbital velocity drives motion
        main.startSize       = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.8f, 0.25f, 1f), new Color(0.25f, 0.8f, 1f));
        main.gravityModifier = 0f;
        main.maxParticles    = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 32f;

        // Circle shape emits from a ring, making a natural orbit starting zone
        var sh = ps.shape;
        sh.enabled         = true;
        sh.shapeType       = ParticleSystemShapeType.Circle;
        sh.radius          = 1.5f;
        sh.radiusThickness = 0.18f;
        sh.arc             = 360f;

        // orbitalY spins all particles around the Y axis — this creates the vortex
        var vel = ps.velocityOverLifetime;
        vel.enabled  = true;
        vel.space    = ParticleSystemSimulationSpace.Local;
        vel.orbitalY = 2.8f;
        vel.radial   = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        // Colour shifts purple→cyan→purple for a magical cycling look
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.9f, 0.3f, 1f), 0f),
                    (new Color(0.3f, 0.9f, 1f), 0.5f),
                    (new Color(0.9f, 0.3f, 1f), 1f) },
            new[] { (0f, 0f), (1f, 0.15f), (0.85f, 0.7f), (0f, 1f) }));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, Bell(0.25f));

        // Subtle noise adds slight wobble to orbiting particles
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.12f;
        noise.frequency   = 1.5f;
        noise.scrollSpeed = 0.6f;

        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.6f;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.5f);
        trails.minVertexDistance = 0.06f;
        trails.dieWithParticles  = true;
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f, Decay(0f));

        r.material      = MakeAdditiveMat("Vortex_Orbit_Mat",  new Color(0.7f, 0.3f, 1f), TexNoise);
        r.trailMaterial = MakeAdditiveMat("Vortex_Trail_Mat",  new Color(0.4f, 0.2f, 0.9f, 0.7f));
    }

    private static void SetupVortexCore(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 3f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 0.6f, 1f), new Color(0.7f, 0.3f, 1f));
        main.maxParticles    = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 55f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.3f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 0.75f, 1f), 0f), (new Color(0.55f, 0.2f, 1f), 1f) },
            new[] { (0f, 0f), (1f, 0.08f), (0f, 1f) }));

        // Strong noise for the churning magical core look
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.55f;
        noise.frequency   = 2.2f;
        noise.scrollSpeed = 1.2f;

        r.material = MakeAdditiveMat("Vortex_Core_Mat", new Color(1f, 0.6f, 1f), TexLightPoint);
    }

    private static void SetupMagicSparks(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.85f, 1f, 1f), new Color(0.55f, 0.55f, 1f));
        main.gravityModifier = 0.12f;
        main.maxParticles    = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        // Periodic bursts of sparks give a "recharging" feel
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)8, (short)18, 0, 1.0f)
        });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 1.8f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.8f, 1f, 1f), 0f), (new Color(0.4f, 0.4f, 1f), 1f) },
            new[] { (1f, 0f), (0f, 1f) }));

        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.85f;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.3f);
        trails.minVertexDistance = 0.05f;
        trails.dieWithParticles  = true;
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f, Decay(0f));

        r.material      = MakeAdditiveMat("MagicSparks_Mat",       new Color(0.6f, 0.9f, 1f));
        r.trailMaterial = MakeAdditiveMat("MagicSparks_Trail_Mat",  new Color(0.5f, 0.5f, 1f, 0.7f));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECT 4 – IMPACT EXPLOSION
    // Flash → fire burst → debris → smoke cloud; loops every 3.5 s
    // ═══════════════════════════════════════════════════════════════════════════

    private static void BuildExplosion(Vector3 pos)
    {
        var root = new GameObject("FX_Explosion");
        root.transform.position = pos;

        // Bright orange burst light
        AddPointLight(root, new Color(1f, 0.6f, 0.2f), 6f, 9f);

        SetupExpFlash    (ChildPS(root, "Flash"));
        SetupExpFireBurst(ChildPS(root, "Fire_Burst"));
        SetupExpDebris   (ChildPS(root, "Debris"));
        SetupExpSmoke    (ChildPS(root, "Smoke_Cloud"));
    }

    private static void SetupExpFlash(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop          = true;
        main.duration      = 3.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f);
        main.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
        main.startSize     = new ParticleSystem.MinMaxCurve(1.8f, 4f);
        main.startColor    = new Color(1f, 0.97f, 0.85f, 1f);
        main.maxParticles  = 5;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1, (short)1, 0, 3.5f) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.001f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 1f, 0.95f), 0f), (new Color(1f, 0.7f, 0.3f), 1f) },
            new[] { (1f, 0f), (0.85f, 0.3f), (0f, 1f) }));

        // Flash expands quickly then fades
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 2f));

        r.material = MakeAdditiveMat("Exp_Flash_Mat", new Color(1f, 0.95f, 0.75f), TexSoftParticle);
    }

    private static void SetupExpFireBurst(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 3.5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(3.5f, 9f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0f));
        main.gravityModifier = 0.12f;
        main.maxParticles    = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)25, (short)40, 0, 3.5f) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.35f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 1f, 0.5f), 0f),
                    (new Color(1f, 0.45f, 0f), 0.3f),
                    (new Color(0.75f, 0.1f, 0f), 0.7f),
                    (new Color(0.18f, 0.08f, 0f), 1f) },
            new[] { (1f, 0f), (1f, 0.1f), (0.7f, 0.6f), (0f, 1f) }));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.15f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.4f)));

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.55f;
        noise.frequency   = 1.1f;
        noise.scrollSpeed = 0.8f;

        r.material = MakeAdditiveMat("Exp_Fire_Mat", new Color(1f, 0.5f, 0.1f), TexFire);
    }

    private static void SetupExpDebris(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 3.5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 7.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.55f, 0.32f, 0.1f), new Color(0.75f, 0.45f, 0.12f));
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 1.6f;     // Heavy debris falls fast
        main.maxParticles    = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)15, (short)25, 0, 3.5f) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.22f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.85f, 0.5f, 0.12f), 0f),
                    (new Color(0.4f, 0.22f, 0.06f), 1f) },
            new[] { (1f, 0f), (1f, 0.65f), (0f, 1f) }));

        // Spinning rotation for tumbling debris chunks (degrees per second)
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z       = new ParticleSystem.MinMaxCurve(-200f, 200f);

        // Short hot trail as chunks fly outward
        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.6f;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.18f);
        trails.minVertexDistance = 0.08f;
        trails.dieWithParticles  = true;
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f, Decay(0f));

        r.material      = MakeAlphaMat   ("Debris_Mat",       new Color(0.65f, 0.38f, 0.12f), TexAsh);
        r.trailMaterial = MakeAdditiveMat("Debris_Trail_Mat", new Color(1f, 0.5f, 0.1f, 0.6f));
    }

    private static void SetupExpSmoke(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 3.5f;
        main.startDelay      = new ParticleSystem.MinMaxCurve(0.08f);   // Slight delay after blast
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.6f, 2.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.55f, 0.5f, 0.45f, 0.55f),
                                   new Color(0.2f, 0.2f, 0.2f, 0.3f));
        main.gravityModifier = -0.1f;
        main.maxParticles    = 55;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0.1f, (short)10, (short)18, 0, 3.5f) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.55f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.65f, 0.55f, 0.42f), 0f),
                    (new Color(0.28f, 0.28f, 0.28f), 0.5f),
                    (new Color(0.1f, 0.1f, 0.1f), 1f) },
            new[] { (0f, 0f), (0.55f, 0.12f), (0.4f, 0.6f), (0f, 1f) }));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 2.2f));

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.32f;
        noise.frequency   = 0.4f;
        noise.scrollSpeed = 0.25f;

        r.material     = MakeAlphaMat("Exp_Smoke_Mat", new Color(0.4f, 0.4f, 0.4f, 0.5f), TexSmoke);
        r.sortingFudge = 3f;   // Smoke renders above fire / debris layers
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECT 5 – FROST STORM
    // Rising ice crystals + swirling frost dust + slowly falling snowflakes
    // ═══════════════════════════════════════════════════════════════════════════

    private static void BuildFrostStorm(Vector3 pos)
    {
        var root = new GameObject("FX_FrostStorm");
        root.transform.position = pos;

        // Cool icy blue light
        AddPointLight(root, new Color(0.55f, 0.82f, 1f), 1.8f, 5.5f);

        SetupIceCrystals(ChildPS(root, "Ice_Crystals"));
        SetupFrostDust  (ChildPS(root, "Frost_Dust"));
        SetupSnowflakes (ChildPS(root, "Snowflakes"));
    }

    private static void SetupIceCrystals(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1f, 3.2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.09f, 0.28f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.75f, 0.92f, 1f), new Color(0.92f, 0.97f, 1f));
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = -0.06f;   // Float gently upward
        main.maxParticles    = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 16f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 22f;
        sh.radius    = 0.35f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.95f, 0.97f, 1f), 0f),
                    (new Color(0.55f, 0.82f, 1f), 0.5f),
                    (new Color(0.35f, 0.65f, 1f), 1f) },
            new[] { (0f, 0f), (1f, 0.1f), (0.85f, 0.6f), (0f, 1f) }));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, Bell(0.3f));

        // Slow spin for the crystal shards (degrees per second)
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z       = new ParticleSystem.MinMaxCurve(-55f, 55f);

        // Noise gives crystals an organic drifting path
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.32f;
        noise.frequency   = 1.1f;
        noise.scrollSpeed = 0.55f;

        // Icy trails follow each crystal shard
        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.65f;
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.4f);
        trails.minVertexDistance = 0.06f;
        trails.dieWithParticles  = true;
        trails.colorOverTrail    = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(0.85f, 0.97f, 1f), 0f), (new Color(0.45f, 0.75f, 1f), 1f) },
            new[] { (0.8f, 0f), (0f, 1f) }));
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f, Decay(0f));

        r.material      = MakeAdditiveMat("IceCrystal_Mat",       new Color(0.75f, 0.92f, 1f), TexDust);
        r.trailMaterial = MakeAdditiveMat("IceCrystal_Trail_Mat", new Color(0.5f, 0.82f, 1f, 0.7f));
    }

    private static void SetupFrostDust(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.82f, 0.92f, 1f, 0.85f),
                                   new Color(1f, 1f, 1f, 0.5f));
        main.gravityModifier = -0.025f;
        main.maxParticles    = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 50f;

        // Box shape spreads dust across a wide area
        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale     = new Vector3(1.8f, 0.5f, 1.8f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 1f, 1f), 0f),
                    (new Color(0.65f, 0.88f, 1f), 0.5f),
                    (new Color(0.42f, 0.72f, 1f), 1f) },
            new[] { (0f, 0f), (0.8f, 0.15f), (0.5f, 0.65f), (0f, 1f) }));

        // Multi-octave noise creates swirling blizzard-like motion
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.55f;
        noise.frequency   = 0.55f;
        noise.scrollSpeed = 0.35f;
        noise.octaveCount = 3;
        noise.quality     = ParticleSystemNoiseQuality.Medium;

        r.material = MakeAdditiveMat("FrostDust_Mat", new Color(0.82f, 0.96f, 1f, 0.6f), TexLightPoint);
    }

    private static void SetupSnowflakes(GameObject go)
    {
        // Spawn slightly above the effect origin so they drift down naturally
        go.transform.localPosition = new Vector3(0f, 3.5f, 0f);

        var ps = go.GetComponent<ParticleSystem>();
        var r  = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.1f, 0.55f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.9f, 0.96f, 1f, 0.9f),
                                   new Color(1f, 1f, 1f, 0.6f));
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0.18f;    // Snowflakes slowly fall
        main.maxParticles    = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 8f;

        // Wide flat box = snowflakes spawn across the top and fall through
        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale     = new Vector3(3f, 0.1f, 3f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 1f, 1f), 0f), (new Color(0.82f, 0.92f, 1f), 1f) },
            new[] { (0f, 0f), (0.9f, 0.1f), (0.85f, 0.75f), (0f, 1f) }));

        // Gentle spin for a drifting snowflake look (degrees per second)
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z       = new ParticleSystem.MinMaxCurve(-35f, 35f);

        // Very gentle noise = realistic wind drift
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.18f;
        noise.frequency   = 0.3f;
        noise.scrollSpeed = 0.22f;

        r.material = MakeAdditiveMat("Snowflake_Mat", new Color(0.92f, 0.98f, 1f, 0.85f), TexLightPoint);
    }
}
