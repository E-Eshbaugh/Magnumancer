using UnityEngine;

/// <summary>
/// Waves rolling up out of the ground and surging forward (Tidal Surge). Each wave is
/// a curved wall of water spanning the cone: it rises at the caster's feet, rolls out
/// with a curling white crest throwing spray, then crashes back down at full range.
/// Several waves follow each other out.
/// </summary>
public class WaveSurgeFx : MonoBehaviour
{
    public int waves = 3;
    public float waveGap = 0.13f;       // seconds between waves
    public float waveTime = 0.55f;      // each wave's life, rim to crash
    public float maxHeight = 1.4f;
    public float thickness = 0.9f;      // back of the wave to its face
    public int arcSegments = 22;

    Vector3 origin, dir;
    float range, cone, born;
    Color deep, foam;
    Wave[] waveList;
    ParticleSystem spray;

    static Material waterMat, sprayMat;

    // Cross-section of a wave, back → front: (distance offset in thicknesses, height fraction)
    static readonly Vector2[] Profile =
    {
        new Vector2(-1.0f, 0.00f),  // back, at the ground
        new Vector2(-0.55f, 0.45f), // back slope
        new Vector2(-0.15f, 0.90f),
        new Vector2(0.05f, 1.00f),  // crest
        new Vector2(0.28f, 0.88f),  // curling lip
        new Vector2(0.22f, 0.55f),  // under the curl
        new Vector2(0.10f, 0.20f),  // steep face
        new Vector2(0.12f, 0.00f),  // front, at the ground
    };

    class Wave
    {
        public float start;
        public Mesh mesh;
        public Vector3[] verts;
        public Color[] colors;
        public MeshRenderer renderer;
    }

    public static WaveSurgeFx Spawn(Vector3 origin, Vector3 dir, float range, float coneDegrees, Color color)
    {
        var go = new GameObject("TidalSurge");
        var fx = go.AddComponent<WaveSurgeFx>();
        fx.origin = AbilityKit.Ground(origin + Vector3.up);
        fx.dir = dir;
        fx.range = range;
        fx.cone = coneDegrees;
        fx.born = Time.time;
        Color c = GlowLine.Brighten(color);
        fx.deep = new Color(c.r * 0.55f, c.g * 0.7f, c.b, 1f);
        fx.foam = Color.Lerp(c, Color.white, 0.8f);
        return fx;
    }

    const float StartDistance = 0.5f;

    // 0..1 progress of a wave across its range (eases out): t(1.4 - 0.4t)
    static float Travel(float t) => t * (1.4f - 0.4f * t);

    /// Seconds until the first wave's face reaches `distance` (inverse of Travel, to sync the hit)
    public float ArrivalTime(float distance)
    {
        float x = Mathf.Clamp01((distance - StartDistance) / Mathf.Max(0.01f, range - StartDistance));
        float t = (1.4f - Mathf.Sqrt(1.96f - 1.6f * x)) / 0.8f;
        return Mathf.Clamp01(t) * waveTime;
    }

    void Start()
    {
        if (waterMat == null) waterMat = GlowLine.CreateMaterial(1.3f, additive: false);
        waveList = new Wave[waves];
        for (int i = 0; i < waves; i++) waveList[i] = BuildWave(i);
        BuildSpray();
    }

    Wave BuildWave(int index)
    {
        var go = new GameObject("Wave");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = waterMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        int cols = arcSegments + 1, rows = Profile.Length;
        var w = new Wave
        {
            start = born + index * waveGap,
            mesh = new Mesh { name = "Wave" },
            verts = new Vector3[cols * rows],
            colors = new Color[cols * rows],
            renderer = mr
        };
        w.mesh.MarkDynamic();

        var tris = new int[arcSegments * (rows - 1) * 6];
        int k = 0;
        for (int a = 0; a < arcSegments; a++)
            for (int p = 0; p < rows - 1; p++)
            {
                int i0 = a * rows + p, i1 = i0 + 1, i2 = i0 + rows, i3 = i2 + 1;
                tris[k++] = i0; tris[k++] = i2; tris[k++] = i1;
                tris[k++] = i1; tris[k++] = i2; tris[k++] = i3;
            }
        w.mesh.vertices = w.verts;
        w.mesh.triangles = tris;
        mf.sharedMesh = w.mesh;
        mr.enabled = false;
        return w;
    }

    void Update()
    {
        bool anyAlive = false;
        foreach (var w in waveList)
            anyAlive |= UpdateWave(w);
        if (!anyAlive && Time.time > born + waves * waveGap + waveTime)
        {
            if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 1f);
            enabled = false;
        }
    }

    bool UpdateWave(Wave w)
    {
        float t = (Time.time - w.start) / waveTime;
        if (t < 0f) return true;
        if (t >= 1f) { w.renderer.enabled = false; return false; }

        // rolls out, easing a little as it goes; rises fast, crashes at the end
        float dist = Mathf.Lerp(StartDistance, range, Travel(t));
        float rise = Mathf.Clamp01(t / 0.22f);
        float crash = Mathf.Clamp01((t - 0.72f) / 0.28f);
        float height = maxHeight * rise * (1f - crash * crash) * (1f + 0.1f * Mathf.Sin(t * 20f));
        float thick = thickness * Mathf.Lerp(0.6f, 1.2f, t);
        float alpha = Mathf.Clamp01(t / 0.1f) * (1f - crash);

        int rows = Profile.Length;
        float half = cone * 0.5f;
        for (int a = 0; a <= arcSegments; a++)
        {
            float u = a / (float)arcSegments;                    // across the cone
            float ang = Mathf.Lerp(-half, half, u);
            Vector3 d = Quaternion.Euler(0f, ang, 0f) * dir;
            // lower at the edges of the cone, so the wave's ends sink into the ground
            float across = Mathf.Sin(u * Mathf.PI);
            float h = height * Mathf.Lerp(0.25f, 1f, across) * (1f + 0.12f * Mathf.Sin(u * 13f + t * 9f));
            for (int p = 0; p < rows; p++)
            {
                Vector2 prof = Profile[p];
                int i = a * rows + p;
                w.verts[i] = origin + d * (dist + prof.x * thick) + Vector3.up * (prof.y * h + 0.03f);
                // deep water at the base, white foam along the crest
                Color c = Color.Lerp(deep, foam, Mathf.SmoothStep(0.45f, 1f, prof.y));
                c.a = alpha * Mathf.Lerp(0.55f, 0.95f, prof.y) * Mathf.Lerp(0.3f, 1f, across);
                w.colors[i] = c;
            }

            // spray flying off the crest
            if (spray != null && rise >= 1f && crash < 0.6f && Random.value < 0.35f)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = origin + d * (dist + 0.1f * thick) + Vector3.up * h,
                    velocity = d * Random.Range(2f, 4f) + Vector3.up * Random.Range(1.5f, 3.5f)
                };
                spray.Emit(ep, 1);
            }
        }
        w.mesh.vertices = w.verts;
        w.mesh.colors = w.colors;
        w.mesh.RecalculateBounds();
        w.renderer.enabled = true;
        return true;
    }

    // Foam spray thrown off the crests; falls back as droplets
    void BuildSpray()
    {
        var go = new GameObject("Spray");
        go.transform.SetParent(transform, false);
        spray = go.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = spray.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(foam, Color.white);
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        var emission = spray.emission;
        emission.enabled = false; // emitted by hand along the crests

        var size = spray.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

        var col = spray.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(foam, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (sprayMat == null) sprayMat = GlowLine.CreateParticleMaterial(1.6f, additive: true);
        rend.sharedMaterial = sprayMat;
        spray.Play();
    }

    void OnDestroy()
    {
        if (waveList == null) return;
        foreach (var w in waveList) if (w != null && w.mesh != null) Destroy(w.mesh);
    }
}
