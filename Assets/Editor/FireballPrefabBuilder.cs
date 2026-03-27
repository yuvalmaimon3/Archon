using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility that builds the "Fireball" VFX prefab.
/// Run via: Tools ▶ Arcon ▶ Build Fireball Prefab
///
/// Fantasy-realistic design principles used here:
///   - Real fire color temperature: blue-white core → yellow → orange → deep red edge
///   - Multiple overlapping layers give perceived volume/depth (not achievable with one PS)
///   - World-space particles trail behind a moving fireball (physically correct)
///   - Local-space particles stay glued to the fireball (magical core, wisps)
///   - Dual light setup: warm outer glow + intense blue-white inner core (bloom-ready)
///   - Rotation on all systems breaks the flat-billboard look
///
/// Hierarchy:
///   Fireball
///   ├── FireLight         ← warm orange outer point light (flickered by FireballController)
///   ├── InnerCoreLight    ← intense blue-white core light (magical heartbeat)
///   ├── PS_MagicCore      ← blue/cyan supernatural heart (local)
///   ├── PS_Core           ← white-yellow hot center (local)
///   ├── PS_Flames         ← main orange-red fire cone (world)
///   ├── PS_Embers         ← sparks with bright trails (world + gravity)
///   ├── PS_MagicWisps     ← energy tendrils orbiting the fireball (local, trails)
///   ├── PS_HeatRing       ← base energy-gathering ring (local)
///   └── PS_Smoke          ← dark warm wisps rising behind (world)
/// </summary>
public static class FireballPrefabBuilder
{
    private const string k_PrefabPath  = "Assets/Prefabs/VFX/Fireball.prefab";
    private const string k_MatFolder   = "Assets/Prefabs/VFX/Materials";
    private const string k_MatAdditive = "Assets/Prefabs/VFX/Materials/Mat_Fireball_Additive.mat";
    private const string k_MatSmoke    = "Assets/Prefabs/VFX/Materials/Mat_Fireball_Smoke.mat";

    // ── Entry Point ───────────────────────────────────────────────────────────

    [MenuItem("Tools/Arcon/Build Fireball Prefab")]
    public static void Build()
    {
        EnsureFolders();

        Material matAdd   = GetOrCreateAdditiveMaterial();
        Material matSmoke = GetOrCreateSmokeMaterial();

        GameObject root = new GameObject("Fireball");
        root.AddComponent<FireballController>();

        // Lights first so FireballController.Start() finds them by name.
        BuildFireLight      (root);
        BuildInnerCoreLight (root);

        // Particle systems — order affects draw call sort.
        BuildPS_MagicCore   (root, matAdd);
        BuildPS_Core        (root, matAdd);
        BuildPS_Flames      (root, matAdd);
        BuildPS_Embers      (root, matAdd);
        BuildPS_MagicWisps  (root, matAdd);
        BuildPS_HeatRing    (root, matAdd);
        BuildPS_Smoke       (root, matSmoke);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;

        Debug.Log($"[FireballPrefabBuilder] Prefab saved → {k_PrefabPath}");
    }

    // ── Folders ───────────────────────────────────────────────────────────────

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
    /// Additive URP particle material — used for all fire, magic, and ember layers.
    /// Additive blending makes overlapping particles accumulate brightness toward white,
    /// which is physically correct for hot glowing fire.
    /// Always recreated on Build() to pick up the Default-Particle soft-circle texture.
    /// </summary>
    private static Material GetOrCreateAdditiveMaterial()
    {
        // Force recreate so the texture is always applied fresh.
        if (AssetDatabase.LoadAssetAtPath<Material>(k_MatAdditive) != null)
            AssetDatabase.DeleteAsset(k_MatAdditive);

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Additive");

        var mat = new Material(shader) { name = "Mat_Fireball_Additive" };
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   1f); // Additive
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        // The Default-Particle built-in texture is a soft radial gradient circle.
        // Without it every particle renders as a hard white square.
        ApplyDefaultParticleTexture(mat);

        AssetDatabase.CreateAsset(mat, k_MatAdditive);
        return mat;
    }

    /// <summary>
    /// Alpha-blend URP particle material — used only for smoke.
    /// Alpha blend allows dark particles to darken the scene (additive cannot).
    /// </summary>
    private static Material GetOrCreateSmokeMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(k_MatSmoke) != null)
            AssetDatabase.DeleteAsset(k_MatSmoke);

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        var mat = new Material(shader) { name = "Mat_Fireball_Smoke" };
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f); // Alpha
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        ApplyDefaultParticleTexture(mat);

        AssetDatabase.CreateAsset(mat, k_MatSmoke);
        return mat;
    }

    /// <summary>
    /// Assigns Unity's built-in "Default-Particle" soft radial-gradient circle texture
    /// to both the URP _BaseMap slot and the legacy _MainTex slot.
    /// Without this every particle billboard renders as a hard solid square.
    /// </summary>
    private static void ApplyDefaultParticleTexture(Material mat)
    {
        // Resources.GetBuiltinResource loads from unity_builtin_extra — always available.
        var tex = Resources.GetBuiltinResource<Texture2D>("Default-Particle.png");

        if (tex == null)
        {
            Debug.LogWarning("[FireballPrefabBuilder] Default-Particle.png not found. " +
                             "Particles will render as hard squares.");
            return;
        }

        mat.SetTexture("_BaseMap",  tex); // URP slot
        mat.SetTexture("_MainTex",  tex); // legacy / fallback slot
    }

    // ── Lights ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Warm outer point light — flickered by FireballController at ~6 Hz.
    /// Large range illuminates surrounding environment for environment story-telling.
    /// </summary>
    private static void BuildFireLight(GameObject root)
    {
        var go = new GameObject("FireLight");
        go.transform.SetParent(root.transform, false);

        var light = go.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(1f, 0.42f, 0.06f); // deep warm orange
        light.intensity = 4f;
        light.range     = 8f;
        light.shadows   = LightShadows.None;
    }

    /// <summary>
    /// Intense blue-white inner core light — driven by FireballController at ~14 Hz.
    /// Very small range keeps it local to the magical core.
    /// High intensity (HDR) creates a sharp bloom spike around the center when post-processing is on.
    /// </summary>
    private static void BuildInnerCoreLight(GameObject root)
    {
        var go = new GameObject("InnerCoreLight");
        go.transform.SetParent(root.transform, false);

        var light = go.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(0.55f, 0.85f, 1.0f); // cool blue-white
        light.intensity = 8f;
        light.range     = 1.8f;  // tight — only the magical core glows this way
        light.shadows   = LightShadows.None;
    }

    // ── PS_MagicCore ──────────────────────────────────────────────────────────

    /// <summary>
    /// Supernatural blue-white core — the magical "soul" inside the fire.
    /// Real acetylene/plasma fire is blue-white at the hottest point.
    /// These tiny bright particles are the first thing the eye locks onto —
    /// they signal "this is not ordinary fire."
    /// Local space — always centred inside the fireball.
    /// </summary>
    private static void BuildPS_MagicCore(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_MagicCore");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles    = 150;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.15f, 0.60f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.20f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.95f, 1.0f, 1f),  // ice blue-white
            new Color(0.3f, 0.75f, 1.0f, 1f)   // electric blue
        );
        // Random start rotation — removes the "grid of circles" look.
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = -0.08f;

        SetEmission(ps, 80f);
        SetShapeSphere(ps, 0.08f);

        // Color: bright cyan-blue → white → fade (magical heat blowout).
        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(0.5f, 0.9f, 1f),   0.00f),
                GCK(new Color(1f,   1f,   1f),   0.30f), // blows out to white at peak
                GCK(new Color(0.3f, 0.6f, 1f),   0.65f),
            },
            new[] { GAK(1f, 0f), GAK(0.9f, 0.3f), GAK(0f, 1f) }
        ));

        SetSizeOverLifetime(ps, MakeCurve(0.1f, 1.3f, 0.20f, 0f));

        // Rotation over lifetime — particles spin like magical energy.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z       = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        SetNoise(ps, strength: 0.4f, frequency: 2.0f, scrollSpeed: 0.6f, octaves: 1);

        ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard, ParticleSystemSortMode.OldestInFront);
    }

    // ── PS_Core ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Hot yellow-white secondary core layer.
    /// Slightly larger and slower than PS_MagicCore, bridging the magical centre
    /// to the outer orange flames. Additive overlap makes the centre blow out
    /// to near-white — physically correct for intense heat.
    /// </summary>
    private static void BuildPS_Core(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_Core");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles    = 180;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.20f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.25f, 1.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.10f, 0.35f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f,   0.92f, 1f), // near-white
            new Color(1f, 0.92f, 0.15f, 1f) // hot yellow
        );
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = -0.10f;

        SetEmission(ps, 70f);
        SetShapeSphere(ps, 0.12f);

        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(1f, 1f,   0.9f),  0.00f),
                GCK(new Color(1f, 0.95f, 0.3f), 0.35f),
                GCK(new Color(1f, 0.55f, 0.0f), 0.65f),
            },
            new[] { GAK(1f, 0f), GAK(0.85f, 0.4f), GAK(0f, 1f) }
        ));

        SetSizeOverLifetime(ps, MakeCurve(0.1f, 1.2f, 0.22f, 0f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.0f, 1.0f);

        SetNoise(ps, strength: 0.35f, frequency: 1.5f, scrollSpeed: 0.5f, octaves: 1);

        ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard, ParticleSystemSortMode.OldestInFront);
    }

    // ── PS_Flames ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Main fire body — the largest, most visible layer.
    /// World space: particles detach from the root and drift upward/behind,
    /// which creates natural trailing fire when the fireball moves.
    /// 3-octave noise gives a turbulent, living flame silhouette.
    /// Colour follows real fire: bright orange-yellow core fading to deep crimson-red at the edges.
    /// </summary>
    private static void BuildPS_Flames(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_Flames");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 300;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.25f, 0.90f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.55f, 0.05f, 1f), // bright orange
            new Color(0.9f, 0.18f, 0.00f, 1f)  // deep crimson
        );
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = -0.20f; // strong upward rise

        SetEmission(ps, 90f);

        // Upward cone — classic flame silhouette.
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Cone;
        shape.angle           = 22f;
        shape.radius          = 0.18f;
        shape.radiusThickness = 1f;

        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(1.0f, 0.88f, 0.25f), 0.00f), // bright golden-orange
                GCK(new Color(1.0f, 0.38f, 0.00f), 0.30f), // orange
                GCK(new Color(0.85f, 0.08f, 0.0f), 0.62f), // deep crimson
                GCK(new Color(0.35f, 0.02f, 0.0f), 0.88f), // near-black red (dying ember)
            },
            new[] { GAK(0.9f, 0f), GAK(0.75f, 0.35f), GAK(0.4f, 0.7f), GAK(0f, 1f) }
        ));

        // Size: flame tongue — expand then taper.
        SetSizeOverLifetime(ps, MakeCurve(0.25f, 1.0f, 0.28f, 0f));

        // Rotation: slow drift — makes billboards feel 3D.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        // 3-octave noise: the key to a living flame silhouette.
        SetNoise(ps, strength: 0.7f, frequency: 0.65f, scrollSpeed: 0.45f, octaves: 3);

        // Radial spread fills the flame volume.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.radial  = new ParticleSystem.MinMaxCurve(0.35f);

        ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard, ParticleSystemSortMode.OldestInFront);
    }

    // ── PS_Embers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Flying sparks — tiny hot particles launched outward by the fire.
    /// World space + real gravity gives each spark an authentic arc trajectory.
    /// The trail module adds bright streaks: in a fantasy game these are iconic
    /// and signal "dangerous — do not touch."
    /// </summary>
    private static void BuildPS_Embers(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_Embers");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 100;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.7f, 2.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.8f, 5.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 1.0f, 0.85f, 1f), // near-white hot
            new Color(1.0f, 0.72f, 0.12f, 1f)  // golden yellow
        );
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = 0.45f; // fall under gravity — real physics

        SetEmission(ps, 25f);

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 40f;
        shape.radius    = 0.15f;

        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(1f, 1f,   0.9f),   0.00f),
                GCK(new Color(1f, 0.65f, 0.0f),  0.28f),
                GCK(new Color(0.9f, 0.1f, 0.0f), 0.65f),
            },
            new[] { GAK(1f, 0f), GAK(0.9f, 0.35f), GAK(0f, 1f) }
        ));

        SetSizeOverLifetime(ps, MakeCurve(1f, 0.7f, 0.4f, 0f));

        // Fast spin — each spark tumbles independently.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);

        SetNoise(ps, strength: 1.0f, frequency: 2.5f, scrollSpeed: 1.0f, octaves: 1);

        // Glowing streak trails — the iconic fantasy spark signature.
        var trails = ps.trails;
        trails.enabled              = true;
        trails.mode                 = ParticleSystemTrailMode.PerParticle;
        trails.ratio                = 0.85f;
        trails.lifetime             = new ParticleSystem.MinMaxCurve(0.18f);
        trails.dieWithParticles     = true;
        trails.minVertexDistance    = 0.015f;
        trails.widthOverTrail       = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 0f)));
        trails.inheritParticleColor = true;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.material      = mat;
        rend.trailMaterial = mat;
        rend.renderMode    = ParticleSystemRenderMode.Billboard;
        rend.sortMode      = ParticleSystemSortMode.OldestInFront;
    }

    // ── PS_MagicWisps ─────────────────────────────────────────────────────────

    /// <summary>
    /// Orbiting energy tendrils — the visual "magic" that separates this from plain fire.
    /// Local space keeps them circling the fireball as it moves.
    ///
    /// Rendering trick: billboard particles with long trail streaks.
    /// OrbitalY velocity makes them orbit, the trail follows the arc → cyan energy ribbon effect.
    /// Two-colour gradient (cyan → blue → purple) gives a cool magical contrast
    /// against the warm orange/red fire underneath.
    /// </summary>
    private static void BuildPS_MagicWisps(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_MagicWisps");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles    = 50;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 5.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.1f, 0.9f, 1.0f, 1f),  // bright cyan
            new Color(0.6f, 0.2f, 1.0f, 1f)   // electric purple
        );
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        SetEmission(ps, 15f);

        // Circle edge — wisps spiral out from the ring.
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = 0.40f;
        shape.radiusThickness = 0f;

        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(0.2f, 1.0f, 1.0f), 0.00f),  // bright cyan
                GCK(new Color(0.1f, 0.5f, 1.0f), 0.40f),  // electric blue
                GCK(new Color(0.7f, 0.1f, 1.0f), 0.75f),  // violet
            },
            new[] { GAK(1f, 0f), GAK(0.8f, 0.45f), GAK(0f, 1f) }
        ));

        // Pulse size: bloom then second bloom → "breathing" magic energy.
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0.00f, 0.2f),
            new Keyframe(0.22f, 1.3f),
            new Keyframe(0.55f, 0.6f),
            new Keyframe(0.80f, 1.0f),
            new Keyframe(1.00f, 0.0f)
        ));

        // Orbital velocity — spin around Y axis to create the swirl.
        var vel = ps.velocityOverLifetime;
        vel.enabled  = true;
        vel.space    = ParticleSystemSimulationSpace.Local;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(4.0f);

        // Long trails turn the orbital motion into visible energy ribbons.
        var trails = ps.trails;
        trails.enabled              = true;
        trails.mode                 = ParticleSystemTrailMode.PerParticle;
        trails.ratio                = 1.0f;  // every wisp gets a trail
        trails.lifetime             = new ParticleSystem.MinMaxCurve(0.25f);
        trails.dieWithParticles     = true;
        trails.minVertexDistance    = 0.02f;
        trails.widthOverTrail       = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 0f)));
        trails.inheritParticleColor = true;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.material      = mat;
        rend.trailMaterial = mat;
        rend.renderMode    = ParticleSystemRenderMode.Billboard;
        rend.sortMode      = ParticleSystemSortMode.OldestInFront;
    }

    // ── PS_HeatRing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Base energy-gathering ring — a flat halo of hot orange particles at the bottom
    /// of the fireball that fly inward and upward, feeding the flame.
    /// This is pure fantasy: it implies the fireball is actively drawing energy from
    /// the air, which reads immediately as "magical."
    /// Emitted from the bottom face of a circle, angled to spiral up.
    /// </summary>
    private static void BuildPS_HeatRing(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_HeatRing");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles    = 60;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.70f, 0.10f, 1f), // hot amber
            new Color(0.4f, 0.85f, 1.0f, 1f)   // cyan-tinted (mixing the two palettes)
        );
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = -0.3f; // float upward into the fire

        SetEmission(ps, 20f);

        // Flat circle, rotated to face upward so particles spray outward from a ring.
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = 0.45f;
        shape.radiusThickness = 0.3f;
        shape.rotation        = new Vector3(90f, 0f, 0f); // lie flat

        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(1f, 0.9f, 0.4f),  0.00f), // bright amber
                GCK(new Color(1f, 0.4f, 0.0f),  0.45f), // orange
            },
            new[] { GAK(0.9f, 0f), GAK(0.5f, 0.5f), GAK(0f, 1f) }
        ));

        SetSizeOverLifetime(ps, MakeCurve(0.5f, 1.0f, 0.3f, 0f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2.0f, 2.0f);

        SetNoise(ps, strength: 0.3f, frequency: 1.0f, scrollSpeed: 0.5f, octaves: 1);

        ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard, ParticleSystemSortMode.OldestInFront);
    }

    // ── PS_Smoke ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Dark warm smoke wisps — the only alpha-blend layer, so they can actually darken the scene.
    /// Slightly warm-tinted (brownish-grey) rather than pure grey: hot smoke absorbs heat and
    /// takes on a yellow-brown cast before cooling to grey. Very subtle — smoke should
    /// support the fire, never overpower it.
    /// World space so wisps hang in the air as the fireball passes through.
    /// </summary>
    private static void BuildPS_Smoke(GameObject root, Material mat)
    {
        var ps = CreatePS(root, "PS_Smoke");

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 40;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.8f, 3.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.3f, 0.85f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.10f, 0.05f, 0.02f, 0.22f), // warm near-black
            new Color(0.16f, 0.08f, 0.03f, 0.12f)  // dark warm brown
        );
        main.startRotation   = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = -0.04f;

        SetEmission(ps, 8f);

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 10f;
        shape.radius    = 0.10f;

        SetColorOverLifetime(ps, new Gradient().WithKeys(
            new[] {
                GCK(new Color(0.12f, 0.06f, 0.01f), 0.00f),
                GCK(new Color(0.07f, 0.03f, 0.01f), 0.50f),
                GCK(new Color(0.03f, 0.01f, 0.00f), 1.00f),
            },
            new[] { GAK(0f, 0f), GAK(0.18f, 0.12f), GAK(0.12f, 0.55f), GAK(0f, 1f) }
        ));

        // Size: expand as smoke billows outward.
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.3f, 1.5f, 0.5f, 2.5f));

        // Slow rotation — gives the wisp a gentle spiral twist.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        SetNoise(ps, strength: 0.45f, frequency: 0.22f, scrollSpeed: 0.18f, octaves: 1);

        ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard, ParticleSystemSortMode.OldestInFront);
    }

    // ── Shared Setup Helpers ──────────────────────────────────────────────────

    /// <summary>Creates a child GameObject, attaches a ParticleSystem, and returns the PS.</summary>
    private static ParticleSystem CreatePS(GameObject root, string childName)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(root.transform, false);
        return go.AddComponent<ParticleSystem>();
    }

    /// <summary>Sets emission rate over time.</summary>
    private static void SetEmission(ParticleSystem ps, float rate)
    {
        var emit = ps.emission;
        emit.enabled      = true;
        emit.rateOverTime = rate;
    }

    /// <summary>Configures the shape module as a sphere.</summary>
    private static void SetShapeSphere(ParticleSystem ps, float radius)
    {
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = radius;
        shape.radiusThickness = 1f;
    }

    /// <summary>Applies a fully built gradient to the color over lifetime module.</summary>
    private static void SetColorOverLifetime(ParticleSystem ps, Gradient gradient)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(gradient);
    }

    /// <summary>Applies an animation curve to the size over lifetime module.</summary>
    private static void SetSizeOverLifetime(ParticleSystem ps, AnimationCurve curve)
    {
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size    = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    /// <summary>Enables and configures the noise (turbulence) module.</summary>
    private static void SetNoise(ParticleSystem ps,
        float strength, float frequency, float scrollSpeed, int octaves)
    {
        var noise = ps.noise;
        noise.enabled      = true;
        noise.strength     = new ParticleSystem.MinMaxCurve(strength);
        noise.frequency    = frequency;
        noise.scrollSpeed  = new ParticleSystem.MinMaxCurve(scrollSpeed);
        noise.octaveCount  = octaves;
    }

    /// <summary>Sets the ParticleSystemRenderer material and render mode.</summary>
    private static void ConfigureRenderer(ParticleSystem ps, Material mat,
        ParticleSystemRenderMode renderMode, ParticleSystemSortMode sortMode)
    {
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.material   = mat;
        rend.renderMode = renderMode;
        rend.sortMode   = sortMode;
    }

    // ── Curve / Gradient Helpers ──────────────────────────────────────────────

    /// <summary>Creates an AnimationCurve: start → peak at peakTime → end.</summary>
    private static AnimationCurve MakeCurve(float start, float peak, float peakTime, float end)
        => new AnimationCurve(
            new Keyframe(0f, start), new Keyframe(peakTime, peak), new Keyframe(1f, end));

    /// <summary>Short-hand: new GradientColorKey(color, time).</summary>
    private static GradientColorKey GCK(Color c, float t) => new GradientColorKey(c, t);

    /// <summary>Short-hand: new GradientAlphaKey(alpha, time).</summary>
    private static GradientAlphaKey GAK(float a, float t) => new GradientAlphaKey(a, t);
}

/// <summary>
/// Extension on Gradient to support a fluent .WithKeys() builder pattern,
/// keeping the particle system setup methods readable.
/// </summary>
internal static class GradientExtensions
{
    public static Gradient WithKeys(this Gradient g,
        GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
    {
        g.SetKeys(colorKeys, alphaKeys);
        return g;
    }
}
