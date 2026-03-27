using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor-only utility that procedurally builds the "Fireball" prefab.
///
/// Run via: Tools ▶ Arcon ▶ Build Fireball Prefab
///
/// Prefab hierarchy:
///   Fireball                    ← root — has FireballController
///   ├── FireLight               ← Point Light driven by FireballController
///   ├── PS_Core                 ← white/yellow hot core  (additive, local space)
///   ├── PS_Flames               ← main orange/red fire   (additive, world space)
///   ├── PS_Embers               ← sparks with trails     (additive, world space)
///   ├── PS_MagicOrbs            ← anime cyan/blue swirl  (additive, local space)
///   └── PS_Smoke                ← dark rising wisps      (alpha-blend, world space)
///
/// Two persistent material assets are created/reused:
///   Assets/Prefabs/VFX/Materials/Mat_Fireball_Additive.mat
///   Assets/Prefabs/VFX/Materials/Mat_Fireball_Smoke.mat
/// </summary>
public static class FireballPrefabBuilder
{
    private const string k_PrefabPath   = "Assets/Prefabs/VFX/Fireball.prefab";
    private const string k_MatFolder    = "Assets/Prefabs/VFX/Materials";
    private const string k_MatAdditive  = "Assets/Prefabs/VFX/Materials/Mat_Fireball_Additive.mat";
    private const string k_MatSmoke     = "Assets/Prefabs/VFX/Materials/Mat_Fireball_Smoke.mat";

    // ── Entry Point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds (or rebuilds) the Fireball prefab.
    /// Existing prefab at the same path is overwritten.
    /// </summary>
    [MenuItem("Tools/Arcon/Build Fireball Prefab")]
    public static void Build()
    {
        EnsureFolders();

        // Persistent materials — created once, reused on rebuild.
        Material matAdditive = GetOrCreateAdditiveMaterial();
        Material matSmoke    = GetOrCreateSmokeMaterial();

        // Build scene hierarchy.
        GameObject root = new GameObject("Fireball");
        root.AddComponent<FireballController>();

        BuildFireLight  (root);
        BuildPS_Core    (root, matAdditive);
        BuildPS_Flames  (root, matAdditive);
        BuildPS_Embers  (root, matAdditive);
        BuildPS_MagicOrbs(root, matAdditive);
        BuildPS_Smoke   (root, matSmoke);

        // Save as prefab and clean up the temporary scene object.
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Highlight in Project window for easy drag-to-scene.
        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;

        Debug.Log($"[FireballPrefabBuilder] Prefab saved → {k_PrefabPath}");
    }

    // ── Folder Setup ──────────────────────────────────────────────────────────

    /// <summary>Creates the VFX prefab and materials folders if they don't exist yet.</summary>
    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/VFX"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "VFX");
        if (!AssetDatabase.IsValidFolder(k_MatFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs/VFX", "Materials");
    }

    // ── Materials ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the additive particle material, creating and saving it to disk if needed.
    /// Used for: core, flames, embers, magic orbs.
    /// Additive blending makes overlapping fire look brighter — correct fire behaviour.
    /// </summary>
    private static Material GetOrCreateAdditiveMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(k_MatAdditive);
        if (existing != null) return existing;

        // Try URP particle shader first; fall back to legacy for non-URP projects.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Additive");

        var mat = new Material(shader) { name = "Mat_Fireball_Additive" };

        // URP Particles/Unlit blend settings.
        mat.SetFloat("_Surface", 1f);  // Transparent
        mat.SetFloat("_Blend",   1f);  // Additive (matches VFXMaterialProvider convention)
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        AssetDatabase.CreateAsset(mat, k_MatAdditive);
        return mat;
    }

    /// <summary>
    /// Returns the alpha-blend smoke material, creating it if needed.
    /// Alpha blend (not additive) lets the dark smoke actually darken — important for realism.
    /// </summary>
    private static Material GetOrCreateSmokeMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(k_MatSmoke);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        var mat = new Material(shader) { name = "Mat_Fireball_Smoke" };

        // Alpha blend: smoke should not brighten — it darkens and fades.
        mat.SetFloat("_Surface", 1f);  // Transparent
        mat.SetFloat("_Blend",   0f);  // Alpha
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        AssetDatabase.CreateAsset(mat, k_MatSmoke);
        return mat;
    }

    // ── Fire Light ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the FireLight child — a warm point light that FireballController will flicker.
    /// Shadows disabled for performance; range and intensity tuned for a room-scale fireball.
    /// </summary>
    private static void BuildFireLight(GameObject root)
    {
        var go = new GameObject("FireLight");
        go.transform.SetParent(root.transform, false);

        var light = go.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(1f, 0.45f, 0.08f); // deep warm orange
        light.intensity = 3.5f;
        light.range     = 6f;
        light.shadows   = LightShadows.None; // no dynamic shadows — keep framerate clean
    }

    // ── PS_Core ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Inner hot core: tiny near-white particles in local space.
    /// Very short lifetime, very fast turnover — creates the "burning heart" of the fireball.
    /// Additive blending makes overlapping particles blow out to white — physically correct.
    /// </summary>
    private static void BuildPS_Core(GameObject root, Material mat)
    {
        var go = new GameObject("PS_Core");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        // Main — tight, hot, fast.
        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // stays inside fireball
        main.maxParticles    = 120;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.15f, 0.40f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.20f, 0.80f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 1.0f, 0.95f, 1f), // near white
            new Color(1.0f, 0.90f, 0.15f, 1f)  // hot yellow
        );
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.05f); // slight upward drift

        // Emission — high rate for dense core.
        var emit = ps.emission;
        emit.enabled      = true;
        emit.rateOverTime = 60f;

        // Shape — tiny sphere at the centre.
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.06f;
        shape.radiusThickness = 1f;

        // Color over lifetime: white-hot → yellow → orange → fade.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 1f,   1f),    0.00f),
                new GradientColorKey(new Color(1f, 0.95f, 0.3f), 0.40f),
                new GradientColorKey(new Color(1f, 0.50f, 0.0f), 0.70f),
            },
            new[] {
                new GradientAlphaKey(1f,   0.00f),
                new GradientAlphaKey(0.8f, 0.50f),
                new GradientAlphaKey(0f,   1.00f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Size over lifetime — quick bloom then shrink (hot burst feel).
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.1f, 1.2f, 0.2f, 0f));

        // Noise — organic turbulence breaks up any grid pattern.
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = new ParticleSystem.MinMaxCurve(0.30f);
        noise.frequency   = 1.5f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.5f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material   = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortMode   = ParticleSystemSortMode.OldestInFront;
    }

    // ── PS_Flames ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Main fire body: larger orange-to-red particles rising in a cone.
    /// World space so they trail behind the fireball when it moves.
    /// Noise turbulence gives that characteristic flickering flame silhouette.
    /// </summary>
    private static void BuildPS_Flames(GameObject root, Material mat)
    {
        var go = new GameObject("PS_Flames");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // drift behind on movement
        main.maxParticles    = 250;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 3.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.35f, 0.01f, 1f), // deep orange
            new Color(1.0f, 0.15f, 0.00f, 1f)  // red-orange
        );
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.15f); // fire rises

        var emit = ps.emission;
        emit.enabled      = true;
        emit.rateOverTime = 70f;

        // Upward cone — classic flame silhouette.
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Cone;
        shape.angle           = 20f;
        shape.radius          = 0.15f;
        shape.radiusThickness = 1f;

        // Color: bright orange-yellow → deep red → dark → fade.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1.0f, 0.80f, 0.20f), 0.00f), // bright orange-yellow
                new GradientColorKey(new Color(1.0f, 0.30f, 0.00f), 0.35f), // orange
                new GradientColorKey(new Color(0.8f, 0.05f, 0.00f), 0.70f), // deep red
            },
            new[] {
                new GradientAlphaKey(0.9f, 0.00f),
                new GradientAlphaKey(0.6f, 0.40f),
                new GradientAlphaKey(0.0f, 1.00f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Size: expand into a tongue shape then shrink.
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.3f, 1.0f, 0.25f, 0f));

        // Noise: 2-octave turbulence — key to realistic flame motion.
        var noise = ps.noise;
        noise.enabled      = true;
        noise.strength     = new ParticleSystem.MinMaxCurve(0.6f);
        noise.frequency    = 0.6f;
        noise.scrollSpeed  = new ParticleSystem.MinMaxCurve(0.4f);
        noise.octaveCount  = 2;

        // Radial spread keeps the flame full-bodied.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.radial  = new ParticleSystem.MinMaxCurve(0.3f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material   = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortMode   = ParticleSystemSortMode.OldestInFront;
    }

    // ── PS_Embers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Flying sparks: tiny hot particles with short light-trails for the anime spark effect.
    /// World space + gravity gives them a realistic arcing trajectory.
    /// Trail module adds glowing streaks behind each ember — key to the anime look.
    /// </summary>
    private static void BuildPS_Embers(GameObject root, Material mat)
    {
        var go = new GameObject("PS_Embers");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 80;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 1.0f, 0.8f, 1f),  // near-white hot
            new Color(1.0f, 0.7f, 0.1f, 1f)   // golden yellow
        );
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.4f); // fall with real gravity

        var emit = ps.emission;
        emit.enabled      = true;
        emit.rateOverTime = 20f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 35f;
        shape.radius    = 0.12f;

        // Color: hot white → yellow → orange → transparent.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 1f,   0.9f),   0.00f),
                new GradientColorKey(new Color(1f, 0.6f, 0.00f),  0.30f),
                new GradientColorKey(new Color(0.8f, 0.1f, 0.0f), 0.70f),
            },
            new[] {
                new GradientAlphaKey(1.0f, 0.00f),
                new GradientAlphaKey(0.8f, 0.40f),
                new GradientAlphaKey(0.0f, 1.00f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Size: cool and shrink over time.
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(1f, 0.6f, 0.5f, 0f));

        // Noise: chaotic — each ember tumbles independently.
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = new ParticleSystem.MinMaxCurve(0.8f);
        noise.frequency   = 2.0f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);

        // Trails: glowing streaks behind each spark — the anime signature touch.
        var trails = ps.trails;
        trails.enabled           = true;
        trails.mode              = ParticleSystemTrailMode.PerParticle;
        trails.ratio             = 0.80f;          // 80% of sparks get a trail
        trails.lifetime          = new ParticleSystem.MinMaxCurve(0.12f);
        trails.dieWithParticles  = true;
        trails.minVertexDistance = 0.02f;
        trails.widthOverTrail    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f)));
        trails.inheritParticleColor = true;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material      = mat;
        rend.trailMaterial = mat;
        rend.renderMode    = ParticleSystemRenderMode.Billboard;
        rend.sortMode      = ParticleSystemSortMode.OldestInFront;
    }

    // ── PS_MagicOrbs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Anime magical energy: glowing cyan/blue/purple orbs that orbit the fireball.
    /// Emitted from a circle edge so they spiral around the outside.
    /// OrbitalY velocity makes them swirl — this is what separates a magic fireball
    /// from a plain fire VFX.
    /// </summary>
    private static void BuildPS_MagicOrbs(GameObject root, Material mat)
    {
        var go = new GameObject("PS_MagicOrbs");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // orbit the fireball root
        main.maxParticles    = 40;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.0f, 0.9f, 1.0f, 1f),  // bright cyan
            new Color(0.5f, 0.2f, 1.0f, 1f)   // electric purple
        );

        var emit = ps.emission;
        emit.enabled      = true;
        emit.rateOverTime = 12f;

        // Circle edge — particles spring outward from the ring perimeter.
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = 0.35f;
        shape.radiusThickness = 0f; // edge-only emission

        // Color: cyan → blue → purple → transparent (cool-toned magic contrast with warm fire).
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.2f, 1.0f, 1.0f),  0.00f), // bright cyan
                new GradientColorKey(new Color(0.1f, 0.5f, 1.0f),  0.40f), // electric blue
                new GradientColorKey(new Color(0.6f, 0.1f, 1.0f),  0.75f), // violet
            },
            new[] {
                new GradientAlphaKey(1.0f, 0.00f),
                new GradientAlphaKey(0.7f, 0.50f),
                new GradientAlphaKey(0.0f, 1.00f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Size: orbs pulse — bloom then shrink then bloom again. Feels alive.
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var pulseCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.2f),
            new Keyframe(0.25f, 1.2f), // first bloom
            new Keyframe(0.55f, 0.6f),
            new Keyframe(0.80f, 0.9f), // second bloom
            new Keyframe(1.00f, 0.0f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, pulseCurve);

        // Orbital velocity — makes them swirl around the fireball's Y axis.
        var vel = ps.velocityOverLifetime;
        vel.enabled  = true;
        vel.space    = ParticleSystemSimulationSpace.Local;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(3.5f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material   = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortMode   = ParticleSystemSortMode.OldestInFront;
    }

    // ── PS_Smoke ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Dark smoke wisps: semi-transparent near-black particles that billow upward.
    /// Uses alpha-blend (not additive) so they create actual darkness — crucial for realism.
    /// World space so they hang in the air as the fireball moves through.
    /// Very low emission rate to keep them subtle — smoke shouldn't overpower the fire.
    /// </summary>
    private static void BuildPS_Smoke(GameObject root, Material mat)
    {
        var go = new GameObject("PS_Smoke");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop            = true;
        main.duration        = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 35;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.25f, 0.70f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.08f, 0.04f, 0.01f, 0.25f), // near-black, 25% alpha
            new Color(0.12f, 0.06f, 0.02f, 0.15f)  // dark brown,  15% alpha
        );
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.05f); // slow upward rise

        var emit = ps.emission;
        emit.enabled      = true;
        emit.rateOverTime = 7f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 8f;
        shape.radius    = 0.10f;

        // Fade in, hold briefly, then fully transparent (wispy billow).
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.10f, 0.05f, 0.00f), 0.00f),
                new GradientColorKey(new Color(0.06f, 0.03f, 0.00f), 0.50f),
                new GradientColorKey(new Color(0.03f, 0.01f, 0.00f), 1.00f),
            },
            new[] {
                new GradientAlphaKey(0.00f, 0.00f), // fade in from invisible
                new GradientAlphaKey(0.20f, 0.15f), // peak opacity — still subtle
                new GradientAlphaKey(0.00f, 1.00f), // fade to nothing
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Size: smoke expands as it rises.
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.3f, 1.2f, 0.5f, 2.0f));

        // Large slow noise for wispy organic billowing.
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = new ParticleSystem.MinMaxCurve(0.4f);
        noise.frequency   = 0.25f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.2f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material   = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortMode   = ParticleSystemSortMode.OldestInFront;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an AnimationCurve with start → peak at peakTime → end.
    /// Mirrors the helper in ElementVFXBuilder for consistency.
    /// </summary>
    private static AnimationCurve MakeCurve(float start, float peak, float peakTime, float end)
    {
        return new AnimationCurve(
            new Keyframe(0f,        start),
            new Keyframe(peakTime,  peak),
            new Keyframe(1f,        end)
        );
    }
}
