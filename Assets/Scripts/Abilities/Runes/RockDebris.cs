using UnityEngine;

/// <summary>
/// Stone chunks and dust for Granite Vow's earth magic. Chunks fly on their own simple
/// ballistic arc (no physics colliders, so they never snag players or bullets), tumble,
/// bounce once off the ground and crumble away. Dust is soft brown smoke.
/// </summary>
public class RockDebris : MonoBehaviour
{
    Vector3 velocity, spin;
    float life, age, groundY, size;
    bool bounced;

    static Material stoneMat, dustMat;
    static readonly Color Stone = new Color(0.42f, 0.36f, 0.3f);
    static readonly Color DustColor = new Color(0.55f, 0.46f, 0.36f);

    public static Material StoneMaterial(Color tint)
    {
        if (stoneMat == null)
        {
            stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "EarthStone" };
            stoneMat.color = Stone;
            if (stoneMat.HasProperty("_Smoothness")) stoneMat.SetFloat("_Smoothness", 0.1f);
        }
        return stoneMat;
    }

    /// One tumbling chunk
    public static void Chunk(Vector3 at, Vector3 velocity, float size, float life = 1.2f)
        => Chunk(at, velocity, size, life, null, default);

    /// A chunk made of any material (ice shards use a translucent tint)
    public static void Chunk(Vector3 at, Vector3 velocity, float size, float life, Material material, Color tint)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "RockChunk";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = at;
        go.transform.rotation = Random.rotation;
        // irregular, not a perfect cube
        go.transform.localScale = new Vector3(size * Random.Range(0.7f, 1.3f), size * Random.Range(0.6f, 1.1f), size * Random.Range(0.7f, 1.3f));
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material != null ? material : StoneMaterial(Stone);
        if (material != null) PowerFx.Tint(r, tint);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        var d = go.AddComponent<RockDebris>();
        d.velocity = velocity;
        d.spin = Random.insideUnitSphere * 720f;
        d.life = life * Random.Range(0.8f, 1.2f);
        d.groundY = AbilityKit.Ground(at + Vector3.up).y;
        d.size = size;
    }

    /// A spray of chunks bursting up out of the ground
    public static void Burst(Vector3 at, int count, float power, float size, Vector3 bias = default)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 c = Random.insideUnitCircle;
            Vector3 v = new Vector3(c.x, 0f, c.y) * power * 0.6f + Vector3.up * power * Random.Range(0.7f, 1.2f) + bias;
            Chunk(at + new Vector3(c.x, 0.1f, c.y) * 0.4f, v, size * Random.Range(0.6f, 1.3f));
        }
    }

    /// A puff of dust
    public static void Dust(Vector3 at, float radius, int puffs, float lift = 0.6f)
    {
        var go = new GameObject("Dust");
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = DustColor;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, puffs) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(lift * 0.5f, lift);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.15f;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.1f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (dustMat == null) dustMat = GlowLine.CreateParticleMaterial(1f, additive: false);
        rend.sharedMaterial = dustMat;
        ps.Play();
        Object.Destroy(go, 2f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        velocity += Physics.gravity * 1.6f * dt;
        transform.position += velocity * dt;
        transform.Rotate(spin * dt, Space.World);

        // one bounce off the floor, then it skids and settles
        if (transform.position.y < groundY + size * 0.4f && velocity.y < 0f)
        {
            var p = transform.position; p.y = groundY + size * 0.4f; transform.position = p;
            if (!bounced) { velocity = new Vector3(velocity.x * 0.5f, -velocity.y * 0.35f, velocity.z * 0.5f); spin *= 0.5f; bounced = true; }
            else { velocity = new Vector3(velocity.x * 0.8f, 0f, velocity.z * 0.8f); spin *= 0.9f; }
        }

        // crumble away at the end
        float left = life - age;
        if (left < 0.3f) transform.localScale *= Mathf.Max(0f, 1f - dt / Mathf.Max(0.01f, left));
        if (age >= life) Destroy(gameObject);
    }
}
