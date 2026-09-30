using UnityEngine;

/// <summary>
/// Static crackling across a ground circle (Static Field): short jagged arcs snap up
/// out of the ground or hop between nearby points, re-striking constantly, while
/// sparks spray up and fall back. Visual only — the ring's GroundHazard does the work.
/// </summary>
public class StaticSparks : MonoBehaviour
{
    [Header("Arcs")]
    public int arcCount = 9;
    public float arcLifeMin = 0.05f, arcLifeMax = 0.14f;
    [Range(0f, 1f)] public float hopChance = 0.45f;   // ground-to-ground hop vs. upward snap
    public float arcHeightMin = 0.35f, arcHeightMax = 1.3f;
    public float arcWidth = 0.09f;

    [Header("Sparks")]
    public float sparksPerSecondPerUnit2 = 3.5f;

    float radius, until, fadeStart;
    Color color, core;
    LineRenderer[] arcs;
    float[] arcEnds;
    ParticleSystem sparks;

    static Material sparkMat;

    public static StaticSparks Spawn(Vector3 center, float radius, float duration, Color color)
    {
        var go = new GameObject("StaticSparks");
        go.transform.position = center;
        var s = go.AddComponent<StaticSparks>();
        s.radius = radius * 0.92f;
        s.until = Time.time + duration;
        s.fadeStart = s.until - 0.4f;
        s.color = GlowLine.Brighten(color);
        s.core = Color.Lerp(s.color, Color.white, 0.65f);
        return s;
    }

    void Start()
    {
        arcs = new LineRenderer[arcCount];
        arcEnds = new float[arcCount];
        for (int i = 0; i < arcCount; i++)
        {
            arcs[i] = GlowLine.Make(transform, "Arc", 7, arcWidth, AbilityKit.Glow());
            arcs[i].enabled = false;
            arcEnds[i] = Time.time + Random.Range(0f, 0.15f); // staggered first strikes
        }
        BuildSparks();
    }

    void Update()
    {
        if (Time.time >= until)
        {
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var a in arcs) a.enabled = false;
            Destroy(gameObject, 0.6f);
            enabled = false;
            return;
        }

        float fade = Time.time > fadeStart ? Mathf.Clamp01((until - Time.time) / 0.4f) : 1f;

        for (int i = 0; i < arcs.Length; i++)
        {
            var lr = arcs[i];
            if (Time.time >= arcEnds[i])
            {
                // occasionally skip a beat so it crackles irregularly
                if (Random.value < 0.25f * (2f - fade)) { lr.enabled = false; arcEnds[i] = Time.time + Random.Range(0.03f, 0.1f); continue; }
                Strike(lr);
                arcEnds[i] = Time.time + Random.Range(arcLifeMin, arcLifeMax);
            }
            // flicker brightness while it lives
            float a = fade * Random.Range(0.55f, 1f);
            lr.startColor = new Color(core.r, core.g, core.b, a);
            lr.endColor = new Color(color.r, color.g, color.b, a * 0.7f);
        }
    }

    void Strike(LineRenderer lr)
    {
        Vector3 c = transform.position + Vector3.up * 0.05f;
        Vector3 a = c + RandomInCircle();
        Vector3 b;
        if (Random.value < hopChance)
        {
            // hop along the ground to a nearby point, arcing up a little
            Vector3 off = RandomInCircle().normalized * Random.Range(0.6f, 1.6f);
            b = a + off;
            if (Flat(b - c) > radius) b = c + (b - c).normalized * radius;
            GlowLine.Bolt(lr, a, b, 0.18f);
            // lift the middle so it arcs
            int n = lr.positionCount;
            for (int i = 1; i < n - 1; i++)
            {
                float t = i / (float)(n - 1);
                lr.SetPosition(i, lr.GetPosition(i) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.35f);
            }
        }
        else
        {
            // snap straight up out of the ground
            b = a + Vector3.up * Random.Range(arcHeightMin, arcHeightMax)
                  + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
            GlowLine.Bolt(lr, a, b, 0.14f);
        }
        lr.widthMultiplier = Random.Range(0.7f, 1.3f);
        lr.enabled = true;
    }

    Vector3 RandomInCircle()
    {
        Vector2 p = Random.insideUnitCircle * radius;
        return new Vector3(p.x, 0f, p.y);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // bright sparks spraying up out of the ground and falling back
    void BuildSparks()
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sparks.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(core, color);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        var emission = sparks.emission;
        emission.rateOverTime = sparksPerSecondPerUnit2 * Mathf.PI * radius * radius;
        // little bursts on top of the steady fizz
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(4, 10), 0, 0.12f) { probability = 0.5f } });

        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        // tilt velocities upward-ish (circle emits radially; the -90 X rotation turns that into a spray)
        shape.randomDirectionAmount = 0.35f;

        var vel = sparks.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = sparks.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.5f), new GradientColorKey(color, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch; // streaky sparks
        rend.velocityScale = 0.06f;
        rend.lengthScale = 1.5f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (sparkMat == null) sparkMat = GlowLine.CreateParticleMaterial(3f, additive: true);
        rend.sharedMaterial = sparkMat;

        sparks.Play();
    }
}
