using UnityEngine;

/// <summary>
/// A low cloud of smoke that bursts from a point and rolls outward along the ground,
/// churning as it spreads and slowing to a thin lingering haze (Contagion).
/// </summary>
public static class RollingSmokeFx
{
    static Material smokeMat;

    /// radius: how far the front rolls. travelTime: seconds for the front to get there.
    public static GameObject Spawn(Vector3 center, float radius, float travelTime, Color dark, Color light)
    {
        var go = new GameObject("RollingSmoke");
        go.transform.position = AbilityKit.Ground(center + Vector3.up) + Vector3.up * 0.25f;

        if (smokeMat == null) smokeMat = GlowLine.CreateParticleMaterial(1f, additive: false);
        BuildFront(go, radius, travelTime, dark, light);
        BuildHaze(go, radius, travelTime, dark);
        Object.Destroy(go, travelTime + 3f);
        return go;
    }

    // The rolling front: a ring of big puffs pushed outward, decelerating as they spread
    static void BuildFront(GameObject go, float radius, float travelTime, Color dark, Color light)
    {
        var ps = NewSystem(go, "Front");
        var main = ps.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(travelTime * 1.1f, travelTime * 1.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(dark, light);
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 70),
            new ParticleSystem.Burst(0.07f, 50),
            new ParticleSystem.Burst(0.14f, 35),
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;
        shape.rotation = new Vector3(90f, 0f, 0f); // flat on the ground

        // outward roll: fast off the mark, slowing as it spreads, so the front reaches `radius`
        // (area under the curve ≈ 0.62 × peak × lifetime)
        float life = travelTime * 1.35f;
        float peak = radius / (0.62f * life);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);   // hugs the ground, lifts a touch
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.radial = new ParticleSystem.MinMaxCurve(peak, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 0.7f), new Keyframe(0.7f, 0.3f), new Keyframe(1f, 0.08f)));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1.8f), new Keyframe(1f, 2.6f)));

        // tumbling puffs, churned by noise so the edge rolls instead of expanding cleanly
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.9f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 1.2f;

        Fade(ps, 0.55f);
        ps.Play();
    }

    // A thin haze left behind that thins out over a couple of seconds
    static void BuildHaze(GameObject go, float radius, float travelTime, Color dark)
    {
        var ps = NewSystem(go, "Haze");
        var main = ps.main;
        main.duration = travelTime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = dark;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 40f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.8f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        Fade(ps, 0.25f);
        ps.Play();
    }

    static ParticleSystem NewSystem(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = smokeMat;
        return ps;
    }

    static void Fade(ParticleSystem ps, float peakAlpha)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f),
                new GradientAlphaKey(peakAlpha * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f)
            }
        });
    }
}
