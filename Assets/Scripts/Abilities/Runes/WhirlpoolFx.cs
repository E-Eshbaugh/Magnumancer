using UnityEngine;

/// <summary>
/// A churning whirlpool filling a ground circle (Undertow): spiral arms of water that
/// wind in toward the center and spin, with foam that orbits, gets sucked inward and
/// whips around faster the closer it gets before vanishing into the eye.
/// Visual only — the ring's GroundHazard does the pulling and damage.
/// </summary>
public class WhirlpoolFx : MonoBehaviour
{
    [Header("Spiral arms")]
    public int arms = 5;
    public int pointsPerArm = 28;
    [Tooltip("How far each arm winds around (turns) from rim to eye")]
    public float twistTurns = 1.1f;
    [Tooltip("Spin speed in radians/sec (the arms rotate, so they read as flowing inward)")]
    public float spinSpeed = 2.6f;
    public float armWidth = 0.22f;

    [Header("Foam")]
    public float foamPerSecondPerUnit2 = 5f;

    float radius, until, born;
    Color water, foam;
    LineRenderer[] armLines, armCores;
    ParticleSystem foamPs;

    static Material foamMat;

    public static WhirlpoolFx Spawn(Vector3 center, float radius, float duration, Color color)
    {
        var go = new GameObject("Whirlpool");
        go.transform.position = center;
        var fx = go.AddComponent<WhirlpoolFx>();
        fx.radius = radius * 0.92f;
        fx.born = Time.time;
        fx.until = Time.time + duration;
        fx.water = GlowLine.Brighten(color);
        fx.foam = Color.Lerp(fx.water, Color.white, 0.75f);
        return fx;
    }

    void Start()
    {
        armLines = new LineRenderer[arms];
        armCores = new LineRenderer[arms];
        for (int i = 0; i < arms; i++)
        {
            armLines[i] = GlowLine.Make(transform, "Arm", pointsPerArm, armWidth, AbilityKit.Glow());
            armCores[i] = GlowLine.Make(transform, "ArmFoam", pointsPerArm, armWidth * 0.35f, AbilityKit.Glow());
            armLines[i].alignment = armCores[i].alignment = LineAlignment.TransformZ; // lie flat on the water
            armLines[i].transform.rotation = armCores[i].transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        BuildFoam();
    }

    void Update()
    {
        if (Time.time >= until)
        {
            if (foamPs != null) foamPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var l in armLines) l.enabled = false;
            foreach (var l in armCores) l.enabled = false;
            Destroy(gameObject, 1.5f);
            enabled = false;
            return;
        }

        float age = Time.time - born;
        float fade = Mathf.Clamp01(age / 0.35f) * Mathf.Clamp01((until - Time.time) / 0.45f);
        // spins up as it forms
        float spin = spinSpeed * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(age / 0.6f));
        float baseAngle = -age * spin; // clockwise from above

        Vector3 c = transform.position + Vector3.up * 0.06f;
        int n = pointsPerArm;
        for (int a = 0; a < arms; a++)
        {
            float armOffset = a * Mathf.PI * 2f / arms;
            // each arm breathes a little so the surface roils
            float wobble = 0.08f * Mathf.Sin(age * 3.1f + a * 1.7f);

            for (int j = 0; j < n; j++)
            {
                float t = j / (float)(n - 1);                       // 0 = rim, 1 = eye
                float r = radius * Mathf.Pow(1f - t, 0.85f) * (1f + wobble * (1f - t));
                float ang = baseAngle + armOffset - t * twistTurns * Mathf.PI * 2f;
                // dips slightly toward the eye, like a funnel
                Vector3 p = c + new Vector3(Mathf.Cos(ang) * r, -0.12f * t * t, Mathf.Sin(ang) * r);
                armLines[a].SetPosition(j, p);
                armCores[a].SetPosition(j, p + Vector3.up * 0.01f);
            }

            // bright in the middle of the arm, fading at the rim and into the eye
            var grad = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(water, 0f), new GradientColorKey(foam, 0.6f), new GradientColorKey(water, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f * fade, 0.25f),
                    new GradientAlphaKey(0.6f * fade, 0.8f), new GradientAlphaKey(0f, 1f)
                }
            };
            armLines[a].colorGradient = grad;
            armCores[a].colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(foam, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f * fade, 0.35f),
                    new GradientAlphaKey(0.5f * fade, 0.85f), new GradientAlphaKey(0f, 1f)
                }
            };
            // arms get thinner as they wind into the eye
            armLines[a].widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.25f));
        }
    }

    // Foam flecks that orbit and get dragged inward, spinning faster near the eye
    void BuildFoam()
    {
        var go = new GameObject("Foam");
        go.transform.SetParent(transform, false);
        foamPs = go.AddComponent<ParticleSystem>();
        foamPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float inwardSpeed = 1.6f;
        float life = radius / inwardSpeed;   // about the time to reach the center from the rim

        var main = foamPs.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(foam, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // orbit around this center
        main.maxParticles = 500;

        var emission = foamPs.emission;
        emission.rateOverTime = foamPerSecondPerUnit2 * Mathf.PI * radius * radius;

        // spawn mostly toward the rim, flat on the water
        var shape = foamPs.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.45f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.position = new Vector3(0f, 0.08f, 0f);

        var vel = foamPs.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        // spin around the vertical axis, faster over life (i.e. as it nears the eye)
        vel.orbitalY = new ParticleSystem.MinMaxCurve(-1f, new AnimationCurve(
            new Keyframe(0f, 1.2f), new Keyframe(0.6f, 2.6f), new Keyframe(1f, 5f)));
        // Unity requires all three orbital axes in the same mode: X/Z as (zero) curves too
        var none = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, none);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, none);
        vel.radial = new ParticleSystem.MinMaxCurve(-inwardSpeed);

        var size = foamPs.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.1f)));

        var col = foamPs.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(water, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.6f, 0.75f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch; // flecks streak along their swirl
        rend.velocityScale = 0.08f;
        rend.lengthScale = 1.2f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (foamMat == null) foamMat = GlowLine.CreateParticleMaterial(1.8f, additive: true);
        rend.sharedMaterial = foamMat;

        foamPs.Play();
    }
}
