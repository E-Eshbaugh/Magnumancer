using UnityEngine;

/// <summary>
/// Punchy one-shot feedback for abilities: light flashes, spark bursts, smoke puffs,
/// ice/stone shards and the wizard's own CFXR prefabs. Everything cleans itself up.
/// </summary>
public static class PowerFx
{
    static Material sparkMat, smokeMat, iceMat;

    public static Material SparkMaterial() => sparkMat != null ? sparkMat : (sparkMat = GlowLine.CreateParticleMaterial(3f, additive: true));
    public static Material SmokeMaterial() => smokeMat != null ? smokeMat : (smokeMat = GlowLine.CreateParticleMaterial(1f, additive: false));

    /// Translucent unlit material for ice meshes (tint per renderer with Tint())
    public static Material IceMaterial() => iceMat != null ? iceMat : (iceMat = GlowLine.CreateMaterial(1.4f, additive: false));

    public static void Tint(Renderer r, Color c)
    {
        var b = new MaterialPropertyBlock();
        r.GetPropertyBlock(b);
        b.SetColor("_BaseColor", c);
        b.SetColor("_Color", c);
        r.SetPropertyBlock(b);
    }

    /// A bright point light that fades out
    public static void Flash(Vector3 pos, Color color, float intensity = 8f, float range = 6f, float time = 0.35f)
    {
        var go = new GameObject("Flash");
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        go.AddComponent<FadingLight>().Init(intensity, time);
    }

    /// Glowing sparks flying out (dir = zero for all directions)
    public static void Sparks(Vector3 pos, Color color, int count, float speed, float life = 0.5f,
                              float size = 0.08f, float gravity = 1f, Vector3 dir = default, float coneDegrees = 180f)
    {
        var ps = OneShot(pos, "Sparks", SparkMaterial(), life);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, 0.6f), color);
        main.gravityModifier = gravity;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Clamp(coneDegrees * 0.5f, 0f, 90f);
        shape.radius = 0.1f;
        if (coneDegrees >= 180f) shape.shapeType = ParticleSystemShapeType.Sphere;
        if (dir != Vector3.zero) ps.transform.rotation = Quaternion.LookRotation(dir);

        FadeOut(ps, 1f, Color.white, color);
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.05f;
        rend.lengthScale = 1.5f;
        Emit(ps, count);
    }

    /// Soft puffs of smoke/mist (additive = glowing mist)
    public static void Puffs(Vector3 pos, Color color, int count, float speed, float size, float life, bool additive = false, float lift = 0.5f)
    {
        var ps = OneShot(pos, "Puffs", additive ? SparkMaterial() : SmokeMaterial(), life);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = -lift * 0.1f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = speed * 0.2f;
        limit.dampen = 0.12f;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f)));
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        FadeOut(ps, additive ? 0.8f : 0.6f, Color.white, Color.white);
        Emit(ps, count);
    }

    /// The wizard's CFXR prefab (spawn/passive effect) at a spot, cleaned up after `life`
    public static void Prefab(GameObject prefab, Vector3 pos, float life = 3f, float scale = 1f)
    {
        if (prefab == null) return;
        var go = Object.Instantiate(prefab, pos, Quaternion.identity);
        if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
        Object.Destroy(go, life);
    }

    /// Ice shards flying off (frozen enemies shattering)
    public static void IceShards(Vector3 pos, Color color, int count, float power, float size)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 v = Random.onUnitSphere * power * 0.6f + Vector3.up * power * Random.Range(0.4f, 1f);
            RockDebris.Chunk(pos + Random.insideUnitSphere * 0.3f, v, size * Random.Range(0.5f, 1.3f), 0.9f,
                             IceMaterial(), new Color(color.r, color.g, color.b, 0.75f));
        }
    }

    // ---------- helpers ----------

    static ParticleSystem OneShot(Vector3 pos, string name, Material mat, float life)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = mat;
        Object.Destroy(go, life + 0.5f);
        return ps;
    }

    static void Emit(ParticleSystem ps, int count)
    {
        ps.Play();
        ps.Emit(count);
    }

    static void FadeOut(ParticleSystem ps, float peak, Color start, Color end)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(peak, 0f), new GradientAlphaKey(peak * 0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }
        });
    }
}
