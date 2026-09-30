using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A thicket bursting out of a ground circle (Overgrowth): thorny vines erupt and curl
/// upward, swaying; any enemy standing inside gets roots lashing up to coil around
/// their legs; pollen drifts up; at the end everything withdraws into the earth.
/// Visual only — the ring's GroundHazard does the slowing and damage.
/// </summary>
public class OvergrowthFx : MonoBehaviour
{
    public int vines = 14;
    public int segments = 12;
    public float growTime = 0.45f;
    public float withdrawTime = 0.4f;
    public float vineHeightMin = 0.7f, vineHeightMax = 1.7f;
    public float vineWidth = 0.14f;
    [Tooltip("Roots that coil around each enemy standing inside")]
    public int rootsPerEnemy = 3;

    GameObject owner;
    float radius, born, until;
    Color leaf, bark;

    class Vine
    {
        public LineRenderer line;
        public Vector3 root;
        public float height, curl, phase, delay, lean;
        public Vector3 leanDir;
    }

    readonly List<Vine> thicket = new();
    readonly Dictionary<GameObject, List<Vine>> wraps = new();
    ParticleSystem pollen;

    static Material vineMat, pollenMat;

    public static OvergrowthFx Spawn(GameObject owner, Vector3 center, float radius, float duration, Color color)
    {
        var go = new GameObject("Overgrowth");
        go.transform.position = center;
        var fx = go.AddComponent<OvergrowthFx>();
        fx.owner = owner;
        fx.radius = radius * 0.9f;
        fx.born = Time.time;
        fx.until = Time.time + duration;
        fx.leaf = GlowLine.Brighten(color);
        fx.bark = new Color(0.28f, 0.2f, 0.12f, 1f);
        return fx;
    }

    void Start()
    {
        if (vineMat == null) vineMat = GlowLine.CreateMaterial(1.2f, additive: false);
        for (int i = 0; i < vines; i++)
        {
            // spread through the circle, a few more toward the edge like a hedge
            Vector2 p = Random.insideUnitCircle.normalized * Mathf.Sqrt(Random.Range(0.1f, 1f)) * radius;
            thicket.Add(NewVine(transform.position + new Vector3(p.x, 0f, p.y),
                                Random.Range(vineHeightMin, vineHeightMax),
                                Random.Range(0f, 0.12f)));
        }
        BuildPollen();
    }

    Vine NewVine(Vector3 root, float height, float delay)
    {
        var lr = GlowLine.Make(transform, "Vine", segments, vineWidth, vineMat);
        lr.alignment = LineAlignment.View;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.45f), new Keyframe(1f, 0.05f));
        lr.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(bark, 0f), new GradientColorKey(Color.Lerp(bark, leaf, 0.6f), 0.5f), new GradientColorKey(leaf, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        };
        lr.enabled = false;
        Vector2 lean = Random.insideUnitCircle.normalized;
        return new Vine
        {
            line = lr, root = root, height = height, delay = delay,
            curl = Random.Range(0.8f, 1.8f) * (Random.value < 0.5f ? 1f : -1f),
            phase = Random.Range(0f, 10f),
            lean = Random.Range(0.2f, 0.6f),
            leanDir = new Vector3(lean.x, 0f, lean.y)
        };
    }

    void Update()
    {
        float now = Time.time;
        bool ending = now >= until;
        float withdraw = ending ? Mathf.Clamp01((now - until) / withdrawTime) : 0f;

        foreach (var v in thicket)
        {
            float grow = Mathf.Clamp01((now - born - v.delay) / growTime);
            grow = 1f - (1f - grow) * (1f - grow);                 // burst up fast, ease in
            DrawVine(v, grow * (1f - withdraw), null);
        }

        if (!ending) UpdateWraps(now);
        foreach (var kv in wraps)
            foreach (var v in kv.Value)
            {
                float grow = Mathf.Clamp01((now - v.delay) / 0.2f);  // lashes up quickly
                DrawVine(v, grow * (1f - withdraw), kv.Key);
            }

        if (ending)
        {
            if (pollen != null) pollen.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (withdraw >= 1f) { Destroy(gameObject, 1f); enabled = false; }
        }
    }

    // A vine curling up from its root, swaying; `wrapTarget` makes it coil around someone
    void DrawVine(Vine v, float amount, GameObject wrapTarget)
    {
        var lr = v.line;
        if (amount <= 0.01f) { lr.enabled = false; return; }
        lr.enabled = true;

        float t = Time.time;
        int n = lr.positionCount;
        Vector3 center = wrapTarget != null ? wrapTarget.transform.position : v.root;
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1) * amount;     // only the grown part exists
            Vector3 p;
            if (wrapTarget == null)
            {
                // rising spiral, leaning outward, tip swaying in the wind
                float ang = f * v.curl * Mathf.PI * 2f + v.phase;
                float r = 0.12f + 0.18f * f;
                p = v.root + new Vector3(Mathf.Cos(ang) * r, v.height * f, Mathf.Sin(ang) * r)
                    + v.leanDir * (v.lean * f * f)
                    + new Vector3(Mathf.Sin(t * 2.2f + v.phase), 0f, Mathf.Cos(t * 1.7f + v.phase)) * (0.12f * f * f);
            }
            else
            {
                // from the ground beside them, spiralling up around their legs and waist
                float ang = v.phase + f * v.curl * Mathf.PI * 2f * 1.6f + t * 1.5f;
                float r = Mathf.Lerp(0.9f, 0.45f, Mathf.Clamp01(f * 2.5f));
                p = center + new Vector3(Mathf.Cos(ang) * r, v.height * f, Mathf.Sin(ang) * r);
                if (i == 0) p = new Vector3(p.x, v.root.y, p.z); // anchored at the ground
            }
            lr.SetPosition(i, p);
        }
    }

    // Roots find every enemy standing in the thicket and coil around them
    void UpdateWraps(float now)
    {
        var inside = AbilityKit.Enemies(transform.position, radius, owner);
        foreach (var e in inside)
        {
            if (wraps.ContainsKey(e)) continue;
            var list = new List<Vine>();
            for (int i = 0; i < rootsPerEnemy; i++)
            {
                var v = NewVine(transform.position, Random.Range(0.9f, 1.3f), now + i * 0.06f);
                v.phase = i * Mathf.PI * 2f / rootsPerEnemy;
                v.curl = Mathf.Abs(v.curl);
                list.Add(v);
            }
            wraps[e] = list;
            RockDebris.Dust(e.transform.position, 1f, 6, 0.3f);
        }

        // escaped (or gone): those roots sink back
        var gone = new List<GameObject>();
        foreach (var kv in wraps)
            if (kv.Key == null || !inside.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var g in gone)
        {
            foreach (var v in wraps[g]) Destroy(v.line.gameObject);
            wraps.Remove(g);
        }
    }

    // Pollen and bits of leaf drifting up out of the thicket
    void BuildPollen()
    {
        var go = new GameObject("Pollen");
        go.transform.SetParent(transform, false);
        pollen = go.AddComponent<ParticleSystem>();
        pollen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = pollen.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(leaf, Color.Lerp(leaf, Color.white, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var em = pollen.emission;
        em.rateOverTime = 6f * Mathf.PI * radius * radius / 4f;

        var shape = pollen.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var vel = pollen.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        vel.y = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        var noise = pollen.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1f;

        var col = pollen.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (pollenMat == null) pollenMat = GlowLine.CreateParticleMaterial(1.8f, additive: true);
        rend.sharedMaterial = pollenMat;
        pollen.Play();
    }
}
