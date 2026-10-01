using UnityEngine;

/// <summary>
/// A cone of billowing mist with fast streaks through it, rushing out from a muzzle
/// (Frost Cannon's freezing blast, Gale Horn's gust). Same recipe as Winter's Wind.
/// </summary>
public static class ConeBlast
{
    static Material smokeMat, streakMat;

    public static void Spawn(Vector3 origin, Vector3 dir, float range, float coneDegrees, float speed,
                             Color mist, Color streaks, int puffs = 70)
    {
        var go = new GameObject("ConeBlast");
        go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir, Vector3.up));
        float travel = range / speed;
        Smoke(go, travel, speed, coneDegrees, mist, puffs);
        Streaks(go, travel, speed, coneDegrees, streaks);
        Object.Destroy(go, 2.5f);
    }

    static void Smoke(GameObject go, float travel, float speed, float cone, Color color, int count)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(travel * 1.1f, travel * 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.4f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 250;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)(count * 0.45f)),
            new ParticleSystem.Burst(0.08f, (short)(count * 0.35f)),
            new ParticleSystem.Burst(0.16f, (short)(count * 0.2f)),
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = cone * 0.4f;
        shape.radius = 0.3f;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = speed * 0.25f;
        limit.dampen = 0.12f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1.8f), new Keyframe(1f, 3f)));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.1f),
                new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0f, 1f)
            }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (smokeMat == null) smokeMat = GlowLine.CreateParticleMaterial(1.1f, additive: false);
        rend.sharedMaterial = smokeMat;
        ps.Play();
    }

    static void Streaks(GameObject parent, float travel, float speed, float cone, Color color)
    {
        var go = new GameObject("Streaks");
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(travel * 0.6f, travel * 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 1.2f, speed * 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20), new ParticleSystem.Burst(0.1f, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = cone * 0.35f;
        shape.radius = 0.5f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.12f;
        rend.lengthScale = 2f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (streakMat == null) streakMat = GlowLine.CreateParticleMaterial(1.8f, additive: true);
        rend.sharedMaterial = streakMat;
        ps.Play();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { smokeMat = null; streakMat = null; }
}
