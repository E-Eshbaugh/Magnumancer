using UnityEngine;

/// <summary>
/// One Void Rift portal: a black eye with dark purple arms spiralling inward and glowing
/// pink streaks, motes sucked in toward the eye and little shards of void spat back out.
/// Dormant (dim, slow) until its twin is placed, then it flares open.
/// </summary>
public class VoidPortalFx : MonoBehaviour
{
    public int arms = 4;
    public int pointsPerArm = 22;
    public float twistTurns = 1.3f;
    public float spinSpeed = 4.5f;

    static readonly Color VoidBlack = new Color(0.02f, 0f, 0.04f, 1f);
    static readonly Color VoidPurple = new Color(0.32f, 0.06f, 0.45f, 1f);
    static readonly Color VoidPink = new Color(1f, 0.3f, 0.85f, 1f);
    static readonly Color VoidViolet = new Color(0.6f, 0.2f, 1f, 1f);

    float radius, born, closeStart = -1f, closeTime = 0.35f;
    bool open;          // linked to its twin
    float openedAt;
    LineRenderer[] armLines, armCores;
    LineRenderer rim;
    Transform eye;
    ParticleSystem inward, spit;

    static Material darkMat, glowMat, eyeMat, sparkMat;

    public static VoidPortalFx Spawn(Vector3 at, float radius)
    {
        var go = new GameObject("VoidPortal");
        go.transform.position = at + Vector3.up * 0.05f;
        var fx = go.AddComponent<VoidPortalFx>();
        fx.radius = radius;
        fx.born = Time.time;
        return fx;
    }

    /// Its twin is down: flare open
    public void Open()
    {
        if (open) return;
        open = true;
        openedAt = Time.time;
        if (spit != null) spit.Emit(25);
    }

    /// Collapse and remove
    public void Close()
    {
        if (closeStart >= 0f) return;
        closeStart = Time.time;
        if (inward != null) inward.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (spit != null) { spit.Emit(20); spit.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        Destroy(gameObject, closeTime + 1f);
    }

    void Start()
    {
        if (darkMat == null) darkMat = GlowLine.CreateMaterial(1f, additive: false);
        if (glowMat == null) glowMat = GlowLine.CreateMaterial(2.8f, additive: true);
        if (eyeMat == null) eyeMat = GlowLine.CreateParticleMaterial(1f, additive: false);
        if (sparkMat == null) sparkMat = GlowLine.CreateParticleMaterial(2.8f, additive: true);

        BuildEye();
        armLines = new LineRenderer[arms];
        armCores = new LineRenderer[arms];
        for (int i = 0; i < arms; i++)
        {
            armLines[i] = FlatLine("VoidArm", darkMat, 0.3f);
            armCores[i] = FlatLine("VoidStreak", glowMat, 0.07f);
        }
        rim = FlatLine("VoidRim", glowMat, 0.1f);
        rim.positionCount = 40;
        rim.loop = true;

        inward = BuildInward();
        spit = BuildSpit();
    }

    LineRenderer FlatLine(string name, Material mat, float width)
    {
        var lr = GlowLine.Make(transform, name, pointsPerArm, width, mat);
        lr.alignment = LineAlignment.TransformZ; // lie flat
        lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return lr;
    }

    void BuildEye()
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "VoidEye";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localPosition = Vector3.up * 0.02f;
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = eyeMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(VoidBlack.r, VoidBlack.g, VoidBlack.b, 0.95f));
        block.SetColor("_Color", new Color(VoidBlack.r, VoidBlack.g, VoidBlack.b, 0.95f));
        r.SetPropertyBlock(block);
        eye = q.transform;
    }

    void Update()
    {
        float age = Time.time - born;
        float grow = Mathf.Clamp01(age / 0.3f);
        float close = closeStart >= 0f ? Mathf.Clamp01((Time.time - closeStart) / closeTime) : 0f;
        float flare = open ? Mathf.Clamp01(1f - (Time.time - openedAt) / 0.4f) : 0f;
        float power = (open ? 1f : 0.45f) * grow * (1f - close);   // dormant portals are dimmer
        float size = radius * grow * (1f - close * close) * (1f + 0.25f * flare);
        float spin = spinSpeed * (open ? 1f : 0.4f) * (1f + flare);
        float baseAngle = -age * spin;

        eye.localScale = Vector3.one * size * (1.25f + 0.08f * Mathf.Sin(age * 6f));

        Vector3 c = transform.position + Vector3.up * 0.04f;
        int n = pointsPerArm;
        for (int a = 0; a < arms; a++)
        {
            float off = a * Mathf.PI * 2f / arms;
            for (int j = 0; j < n; j++)
            {
                float t = j / (float)(n - 1);                 // 0 = rim, 1 = eye
                float r = size * Mathf.Pow(1f - t, 0.8f);
                float ang = baseAngle + off - t * twistTurns * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(ang) * r, -0.05f * t, Mathf.Sin(ang) * r);
                armLines[a].SetPosition(j, p);
                armCores[a].SetPosition(j, p + Vector3.up * 0.01f);
            }
            armLines[a].colorGradient = Grad(VoidPurple, VoidBlack, 0.85f * power);
            armCores[a].colorGradient = Grad(VoidPink, VoidViolet, 0.9f * power);
            armLines[a].widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.2f));
        }

        AbilityKit.Circle(rim, c, size * (1f + 0.05f * Mathf.Sin(age * 9f)));
        GlowLine.SetColor(rim, Color.Lerp(VoidViolet, VoidPink, 0.5f + 0.5f * Mathf.Sin(age * 3f)), power);

        if (inward != null)
        {
            var em = inward.emission;
            em.rateOverTimeMultiplier = 30f * power;
        }
        if (spit != null)
        {
            var em = spit.emission;
            em.rateOverTimeMultiplier = 12f * power;
        }
    }

    static Gradient Grad(Color rimColor, Color eyeColor, float alpha) => new Gradient
    {
        colorKeys = new[] { new GradientColorKey(rimColor, 0f), new GradientColorKey(rimColor, 0.5f), new GradientColorKey(eyeColor, 1f) },
        alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.2f), new GradientAlphaKey(alpha, 0.8f), new GradientAlphaKey(0f, 1f) }
    };

    // Motes spiralling in from the rim and vanishing into the eye
    ParticleSystem BuildInward()
    {
        var ps = NewSystem("Inward", ParticleSystemSimulationSpace.Local, sparkMat);
        float inwardSpeed = 1.3f;
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(radius / inwardSpeed * 0.6f, radius / inwardSpeed);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(VoidPink, VoidViolet);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.3f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        // all three orbital axes must share a mode (curves)
        var none = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, none);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(-1f, new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(1f, 7f)));
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, none);
        vel.radial = new ParticleSystem.MinMaxCurve(-inwardSpeed);

        Fade(ps, 0.9f);
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.1f;
        rend.lengthScale = 1.5f;
        ps.Play();
        return ps;
    }

    // Little shards of void flicked up and out of the eye
    ParticleSystem BuildSpit()
    {
        var ps = NewSystem("Spit", ParticleSystemSimulationSpace.World, sparkMat);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(VoidPink, VoidViolet);
        main.gravityModifier = 0.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = radius * 0.25f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // up out of the eye

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(VoidPink, 0.3f), new GradientColorKey(VoidPurple, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.06f;
        rend.lengthScale = 1.5f;
        ps.Play();
        return ps;
    }

    ParticleSystem NewSystem(string name, ParticleSystemSimulationSpace space, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = space;
        main.maxParticles = 300;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.sharedMaterial = mat;
        return ps;
    }

    static void Fade(ParticleSystem ps, float peak)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.2f), new GradientAlphaKey(0f, 1f) }
        });
    }
}
