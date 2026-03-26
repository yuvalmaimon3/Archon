using UnityEngine;

/// <summary>
/// Static factory that builds all element VFX using Unity Particle System.
/// Four factory methods — one for each VFX layer:
///
///   1. BuildProjectileVFX — body + trail particles on a flying projectile
///   2. BuildHitVFX        — one-shot impact burst at hit point
///   3. BuildReactionVFX   — stronger one-shot burst when two elements react
///   4. BuildElementAura   — looping particles showing which element sticks to a target
///
/// All particle systems share one material (VFXMaterialProvider) and use minimal
/// particle counts for mobile performance. No VFX Graph, no custom shaders.
/// </summary>
public static class ElementVFXBuilder
{
    // ────────────────────────────────────────────────────────────────────────
    // 1. PROJECTILE VFX
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates body + trail particle system for a projectile.
    /// Combines archetype shape/motion with element colors/behavior.
    /// Returns the root GameObject parented to the projectile transform.
    /// </summary>
    public static GameObject BuildProjectileVFX(ProjectileShape shape, ElementType element, Transform parent)
    {
        var archetype = ProjectileArchetypeConfig.Get(shape);
        var elemCfg   = ElementVisualConfig.Get(element);

        // Root container
        var root = new GameObject($"VFX_{shape}_{element}");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        // Body particle system on the root
        var ps = root.AddComponent<ParticleSystem>();
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ConfigureProjectileBody(ps, archetype, elemCfg);
        ConfigureProjectileTrail(ps, archetype, elemCfg);

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = archetype.renderMode;
        if (archetype.renderMode == ParticleSystemRenderMode.Stretch)
        {
            renderer.velocityScale = archetype.stretchSpeed;
            renderer.lengthScale   = archetype.stretchLength;
        }

        // Optional secondary emitter (embers, sparks)
        if (archetype.hasSecondary)
            CreateSecondaryEmitter(root.transform, archetype, elemCfg);

        ps.Play(true);
        return root;
    }

    /// <summary>Configures the main body particle system modules.</summary>
    private static void ConfigureProjectileBody(ParticleSystem ps,
        ProjectileArchetypeConfig.Config arc, ElementVisualConfig.Config elem)
    {
        // Main
        var main = ps.main;
        main.loop              = true;
        main.duration          = 1f;
        main.maxParticles      = arc.maxParticles;
        main.startLifetime     = arc.startLifetime;
        main.startSpeed        = new ParticleSystem.MinMaxCurve(arc.startSpeedMin, arc.startSpeedMax);
        main.startSize         = new ParticleSystem.MinMaxCurve(arc.startSizeMin, arc.startSizeMax);
        main.simulationSpace   = ParticleSystemSimulationSpace.Local;
        main.startColor        = new ParticleSystem.MinMaxGradient(elem.primary, elem.secondary);
        main.gravityModifier   = elem.gravityMod * arc.gravityMultiplier;

        // Emission
        var emission = ps.emission;
        emission.enabled    = true;
        emission.rateOverTime = arc.emissionRate * elem.emissionMult;

        // Shape
        var shape = ps.shape;
        shape.enabled       = true;
        shape.shapeType     = arc.shapeType;
        shape.radius        = arc.shapeRadius;
        shape.angle         = arc.shapeAngle;
        shape.radiusThickness = 0.2f;

        // Color over lifetime — fade in, hold, fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(ElementVisualConfig.MakeBodyGradient(elem));

        // Size over lifetime — hold then shrink
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.6f, 1f, 0.2f, 0f));

        // Noise (turbulence) — element-specific intensity, archetype-modified
        float noiseStr = elem.noiseStrength * arc.noiseMultiplier;
        if (noiseStr > 0.01f)
        {
            var noise = ps.noise;
            noise.enabled     = true;
            noise.strength    = noiseStr;
            noise.frequency   = elem.noiseFrequency;
            noise.octaveCount = 1; // mobile-friendly: single octave
        }

        // Orbital velocity (for spinning archetypes like Shard, SpinningDisc)
        if (arc.orbitalY > 0.01f)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled  = true;
            vel.orbitalY = arc.orbitalY;
        }
    }

    /// <summary>Configures the built-in trail module on body particles.</summary>
    private static void ConfigureProjectileTrail(ParticleSystem ps,
        ProjectileArchetypeConfig.Config arc, ElementVisualConfig.Config elem)
    {
        if (!arc.useTrail) return;

        var trails = ps.trails;
        trails.enabled       = true;
        trails.mode          = ParticleSystemTrailMode.PerParticle;
        trails.lifetime      = arc.trailLifetime;
        trails.dieWithParticles = true;
        trails.minVertexDistance = 0.02f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f,
            MakeCurve(arc.trailWidthStart, arc.trailWidthStart, 0.2f, arc.trailWidthEnd));
        trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(
            ElementVisualConfig.MakeTrailGradient(elem));
        trails.inheritParticleColor = false;

        // Trail renderer uses the same shared material
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.trailMaterial = VFXMaterialProvider.Get();
    }

    /// <summary>Creates a child particle emitter for secondary particles (embers, sparks, spray).</summary>
    private static void CreateSecondaryEmitter(Transform parent,
        ProjectileArchetypeConfig.Config arc, ElementVisualConfig.Config elem)
    {
        var go = new GameObject("SecondaryEmitter");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = true;
        main.maxParticles    = arc.secondaryMax;
        main.startLifetime   = arc.secondaryLifetime;
        main.startSpeed      = arc.secondarySpeed;
        main.startSize       = arc.secondarySize;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // detach from projectile
        main.startColor      = new ParticleSystem.MinMaxGradient(elem.primary, elem.secondary);
        main.gravityModifier = arc.secondaryGravity;

        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = arc.secondaryRate;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.05f;

        // Fade out over lifetime
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeAlphaGradient(0.8f, 0f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(1f, 0.8f, 0.3f, 0f));

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. HIT IMPACT VFX
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a one-shot element-colored impact burst at the given world position.
    /// Auto-destroys when particles finish.
    /// </summary>
    public static void BuildHitVFX(ElementType element, Vector3 worldPosition)
    {
        var elemCfg = ElementVisualConfig.Get(element);

        var go = new GameObject($"HitVFX_{element}");
        go.transform.position = worldPosition;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Main — short burst, no loop
        var main = ps.main;
        main.loop            = false;
        main.duration        = 0.3f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.maxParticles    = 25;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new ParticleSystem.MinMaxGradient(elemCfg.primary, elemCfg.hitFlash);
        main.gravityModifier = 0.4f;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        // Emission — single burst
        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 25) });

        // Shape — hemisphere upward
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius    = 0.15f;

        // Color — bright flash then fade
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(elemCfg.hitFlash, 0f),
                new GradientColorKey(elemCfg.primary, 0.2f),
                new GradientColorKey(elemCfg.primary * 0.5f, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Size — expand briefly then shrink
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.5f, 1.2f, 0.15f, 0f));

        // Slight noise for organic feel
        var noise = ps.noise;
        noise.enabled   = true;
        noise.strength  = 0.15f;
        noise.frequency = 2f;

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. REACTION VFX (stronger, more rewarding than hit VFX)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns an impressive reaction burst when two elements combine on the same target.
    /// Larger, more particles, longer duration than a normal hit.
    /// Auto-destroys when finished.
    /// </summary>
    public static void BuildReactionVFX(ReactionType reaction, Vector3 worldPosition)
    {
        var (primary, glow) = ElementVisualConfig.GetReactionColors(reaction);

        var go = new GameObject($"ReactionVFX_{reaction}");
        go.transform.position = worldPosition;

        // ── Primary burst (outward explosion) ──────────────────────────────
        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = false;
        main.duration        = 0.6f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.maxParticles    = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new ParticleSystem.MinMaxGradient(primary, glow);
        main.gravityModifier = 0.3f;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(glow, 0f),
                new GradientColorKey(primary, 0.25f),
                new GradientColorKey(primary * 0.4f, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.8f, 1.5f, 0.1f, 0f));

        // High noise for chaotic reaction feel
        var noise = ps.noise;
        noise.enabled   = true;
        noise.strength  = 0.35f;
        noise.frequency = 4f;

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // ── Secondary ring (expanding shockwave ring) ──────────────────────
        CreateReactionRing(go.transform, primary, glow);

        // ── Tertiary flash (bright center flash) ───────────────────────────
        CreateReactionFlash(go.transform, glow);

        ps.Play(true); // Play including children
    }

    /// <summary>Creates an expanding ring of particles for the reaction shockwave.</summary>
    private static void CreateReactionRing(Transform parent, Color primary, Color glow)
    {
        var go = new GameObject("ReactionRing");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = false;
        main.duration        = 0.3f;
        main.startLifetime   = 0.4f;
        main.startSpeed      = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.maxParticles    = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new ParticleSystem.MinMaxGradient(primary, glow);

        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 20) });

        // Circle edge shape — creates horizontal ring
        var shape = ps.shape;
        shape.enabled       = true;
        shape.shapeType     = ParticleSystemShapeType.Circle;
        shape.radius        = 0.1f;
        shape.radiusThickness = 0f; // emit from edge only
        // Rotate to be horizontal
        shape.rotation = new Vector3(90f, 0f, 0f);

        // Fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeAlphaGradient(0.9f, 0f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(1f, 0.5f, 0.5f, 0f));

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    /// <summary>Creates a brief bright flash at the center of a reaction.</summary>
    private static void CreateReactionFlash(Transform parent, Color glow)
    {
        var go = new GameObject("ReactionFlash");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = false;
        main.duration        = 0.15f;
        main.startLifetime   = 0.15f;
        main.startSpeed      = 0f;
        main.startSize       = 0.6f; // large single particle flash
        main.maxParticles    = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new Color(glow.r, glow.g, glow.b, 0.7f);

        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        // Rapid size pulse: expand and vanish
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.3f, 1f, 0.2f, 0.5f));

        // Rapid alpha fade
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeAlphaGradient(0.8f, 0f));

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. ELEMENT AURA (sticking to target)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a looping particle aura around a target showing which element sticks to it.
    /// Parented to the target — caller destroys it when the element clears.
    /// Returns the ParticleSystem so the caller can manage its lifetime.
    /// </summary>
    public static ParticleSystem BuildElementAura(ElementType element, Transform parent)
    {
        var elemCfg = ElementVisualConfig.Get(element);

        var go = new GameObject($"Aura_{element}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.5f;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = true;
        main.duration        = 1f;
        main.startLifetime   = 1.0f;
        main.startSpeed      = 0.2f;
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.maxParticles    = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor      = new ParticleSystem.MinMaxGradient(elemCfg.primary, elemCfg.secondary);
        main.gravityModifier = elemCfg.gravityMod;

        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = 12f;

        // Sphere shell around target
        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.45f;
        shape.radiusThickness = 0.1f;

        // Fade in then out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(ElementVisualConfig.MakeBodyGradient(elemCfg));

        // Shrink toward end
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0.4f, 1f, 0.2f, 0f));

        // Element-specific motion
        if (elemCfg.noiseStrength > 0.01f)
        {
            var noise = ps.noise;
            noise.enabled   = true;
            noise.strength  = elemCfg.noiseStrength * 0.7f; // slightly subdued for aura
            noise.frequency = elemCfg.noiseFrequency;
        }

        renderer.material   = VFXMaterialProvider.Get();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        return ps;
    }

    // ────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Creates an AnimationCurve with start → peak → end shape.</summary>
    private static AnimationCurve MakeCurve(float start, float peak, float peakTime, float end)
    {
        return new AnimationCurve(
            new Keyframe(0f, start),
            new Keyframe(peakTime, peak),
            new Keyframe(1f, end)
        );
    }

    /// <summary>Creates a gradient that only varies alpha (white color, alpha from start to end).</summary>
    private static Gradient MakeAlphaGradient(float startAlpha, float endAlpha)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) }
        );
        return g;
    }
}
