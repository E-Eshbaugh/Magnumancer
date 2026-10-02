using System.Collections;
using UnityEngine;

/// <summary>
/// The Singularity Launcher's black hole: a dark core with spinning rings and streaks of
/// light being sucked in. For a moment it drags enemies toward its center (heavier guns
/// resist), then pops: void damage, a hard shove outward, and a heavy hit that sets off
/// Shatter on the frozen.
/// </summary>
public class Singularity : MonoBehaviour
{
    public static float PullRadius = 7f;
    public static float PullTime = 1.6f;
    public static float MaxPull = 11f;
    public static float PopRadius = 4f;
    public static float PopDamage = 40f;
    public static float PopShove = 24f;

    GameObject owner;
    Color color;
    Transform core;
    LineRenderer ringA, ringB;
    Light glow;
    static Material darkMaterial;

    public static void Launch(GameObject owner, Vector3 from, Vector3 to, Color color)
    {
        var go = new GameObject("Singularity");
        go.transform.position = from;
        var s = go.AddComponent<Singularity>();
        s.owner = owner;
        s.color = color;
        s.StartCoroutine(s.Run(from, to));
    }

    IEnumerator Run(Vector3 from, Vector3 to)
    {
        // the grenade
        var shell = AbilityKit.GlowOrb(color, 0.35f);
        shell.transform.SetParent(transform, false);
        float flight = Mathf.Lerp(0.3f, 0.6f, Mathf.Clamp01(Vector3.Distance(from, to) / SingularityLauncher.Range));
        yield return AbilityKit.Lob(transform, from, to, flight, 2f);
        Destroy(shell);

        Vector3 center = to + Vector3.up * 1f;
        transform.position = center;
        Build();
        Sfx.Play(SfxId.CastVoid, to, 1f, 0.75f);
        AbilityKit.Shockwave(to, PullRadius, color, 0.4f);
        CameraShake.Shake(0.1f, 0.2f);

        float t = 0f, nextRumble = 0f;
        while (t < PullTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / PullTime);
            core.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.3f, k) * (1f + 0.06f * Mathf.Sin(t * 30f));
            ringA.transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.Self);
            ringB.transform.Rotate(Vector3.forward, -540f * Time.deltaTime, Space.Self);
            ringA.widthMultiplier = ringB.widthMultiplier = Mathf.Lerp(0.5f, 1.4f, k);
            glow.intensity = Mathf.Lerp(2f, 7f, k);
            // a low drone that winds up until it pops
            Sfx.StartLoop(this, SfxId.EmberSpinLoop);
            Sfx.SetLoop(this, 0.4f + 0.6f * k, Mathf.Lerp(0.25f, 0.6f, k * k));

            bool rumble = Time.time >= nextRumble;
            if (rumble) nextRumble = Time.time + 0.2f;
            foreach (var e in AbilityKit.Enemies(to, PullRadius, owner))
            {
                Vector3 d = to - e.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.05f) continue;
                float strength = Mathf.Lerp(MaxPull, MaxPull * 0.45f, dist / PullRadius);
                if (dist < 0.8f) strength *= dist / 0.8f;   // settle in the middle instead of jittering
                AbilityKit.Knockback(e, d / dist * strength);
                if (rumble) Rumble.Play(e, 0.25f, 0.35f, 0.18f, fade: false);
            }
            yield return null;
        }

        Pop(to);
        Destroy(gameObject);
    }

    void Pop(Vector3 ground)
    {
        Vector3 at = ground + Vector3.up;
        Sfx.StopLoop(this);
        Explosions.AffectWorld(ground, PopRadius, PopDamage, gameObject);
        foreach (var e in AbilityKit.Enemies(ground, PopRadius, owner))
        {
            Vector3 d = e.transform.position - ground; d.y = 0f;
            float k = 1f - Mathf.Clamp01(d.magnitude / PopRadius);
            float dmg = PopDamage * Mathf.Lerp(0.5f, 1f, k);
            DamageEvents.Deal(e, dmg, owner);
            ElementReactions.AbilityHit(e, owner, Element.Void, dmg, heavy: true);
            if (d.sqrMagnitude < 0.01f) d = Random.insideUnitSphere;
            d.y = 0f;
            AbilityKit.Knockback(e, d.normalized * PopShove * Mathf.Lerp(0.6f, 1f, k) + Vector3.up * 4f);
        }
        ElementReactions.OnElementArea(ground, PopRadius, owner, Element.Void);

        AbilityKit.Shockwave(ground, PopRadius * 1.2f, color, 0.45f);
        AbilityKit.Shockwave(ground, PopRadius * 0.6f, Color.white, 0.3f);
        PowerFx.Flash(at, color, 14f, 10f, 0.45f);
        PowerFx.Sparks(at, color, 60, 11f, 0.7f, 0.1f, 0.4f);
        PowerFx.Sparks(at, Color.white, 25, 7f, 0.4f, 0.06f, 0.2f);
        CameraShake.Shake(0.35f, 0.3f);
    }

    void Build()
    {
        if (darkMaterial == null)
        {
            darkMaterial = GlowLine.CreateMaterial(1f, additive: false);
            darkMaterial.name = "SingularityCore";
        }
        var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(c.GetComponent<Collider>());
        c.name = "Core";
        c.transform.SetParent(transform, false);
        var r = c.GetComponent<MeshRenderer>();
        r.sharedMaterial = darkMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0.02f, 0f, 0.05f, 0.95f));
        block.SetColor("_Color", new Color(0.02f, 0f, 0.05f, 0.95f));
        r.SetPropertyBlock(block);
        core = c.transform;

        ringA = LocalRing("RingA", 1.4f, color, new Vector3(70f, 0f, 0f));
        ringB = LocalRing("RingB", 1.1f, Color.Lerp(color, Color.white, 0.5f), new Vector3(-30f, 40f, 0f));

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = color; glow.range = PullRadius; glow.shadows = LightShadows.None;

        Infall();
    }

    LineRenderer LocalRing(string name, float radius, Color c, Vector3 tilt)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(transform, false);
        holder.localRotation = Quaternion.Euler(tilt);
        var lr = GlowLine.Make(holder, "Line", 36, 0.08f, AbilityKit.Glow());
        lr.useWorldSpace = false;
        lr.loop = true;
        for (int i = 0; i < 36; i++)
        {
            float a = i / 36f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
        GlowLine.SetColor(lr, c, 0.9f);
        return lr;
    }

    // Streaks of light falling into the core
    void Infall()
    {
        var go = new GameObject("Infall");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = PullTime;
        main.startLifetime = 0.5f;
        main.startSpeed = -PullRadius / 0.55f;   // inward, reaching the core as they fade
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 90f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = PullRadius * 0.85f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0f, 1f) }
        });
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.06f;
        rend.lengthScale = 2f;
        rend.sharedMaterial = PowerFx.SparkMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => darkMaterial = null;
}
