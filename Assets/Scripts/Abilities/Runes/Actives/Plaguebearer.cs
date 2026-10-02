using UnityEngine;

/// The walking-plague state on a Blightward player
public class Plaguebearer : MonoBehaviour
{
    public float radius = 3.5f;
    public float speedBonus = 1.1f;

    float until;
    bool active;
    Color toxic, dark;
    ParticleSystem smoke, spores;
    LineRenderer ring, ring2;
    float nextTick;

    const string Key = "plaguebearer";
    static readonly Color Rot = new Color(0.18f, 0.24f, 0.08f, 1f);

    public void Begin(float duration, Color theme)
    {
        toxic = theme;
        dark = Color.Lerp(Rot, theme, 0.3f);
        until = Time.time + duration;
        if (!active)
        {
            active = true;
            GetComponent<PlayerMovement3D>()?.SetSpeedModifier(Key, speedBonus);
            Build();
        }

        // the eruption
        RollingSmokeFx.Spawn(transform.position, radius + 1.5f, 0.4f, dark, toxic);
        var wiz = GetComponent<PlayerMovement3D>()?.wizard;
        PowerFx.Prefab(wiz != null ? wiz.spawnEffectPrefab : null, transform.position + Vector3.up * 0.5f, 2.5f);
        PowerFx.Sparks(transform.position + Vector3.up, toxic, 30, 6f, 0.6f, 0.09f, 0.4f);
        PowerFx.Flash(transform.position + Vector3.up, toxic, 7f, 7f, 0.4f);
        AbilityKit.Shockwave(transform.position, radius + 1f, toxic, 0.4f);
        CameraShake.Shake(0.15f, 0.2f);
        Rumble.Play(gameObject, 0.6f, 0.5f, 0.3f);
        Sfx.Play(SfxId.GasBurst, transform.position);
    }

    void Build()
    {
        smoke = Emitter("PlagueSmoke", PowerFx.SmokeMaterial(), 28f);
        var m = smoke.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(dark, Color.Lerp(dark, toxic, 0.5f));
        var sh = smoke.shape;
        sh.radius = radius * 0.8f;
        var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
        var sz = smoke.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));
        Fade(smoke, 0.5f);

        spores = Emitter("PlagueSpores", PowerFx.SparkMaterial(), 22f);
        var s = spores.main;
        s.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        s.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        s.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        s.startColor = new ParticleSystem.MinMaxGradient(toxic, Color.Lerp(toxic, Color.white, 0.5f));
        s.gravityModifier = -0.15f;   // bubbles drifting up
        var ssh = spores.shape;
        ssh.radius = radius;
        var noise = spores.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 1.2f;
        Fade(spores, 0.9f);

        ring = GlowLine.Make(transform, "PlagueRing", 48, 0.1f, AbilityKit.Glow());
        ring2 = GlowLine.Make(transform, "PlagueRing2", 48, 0.05f, AbilityKit.Glow());
        ring.loop = ring2.loop = true;
    }

    ParticleSystem Emitter(string name, Material mat, float rate)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.3f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // it trails as you walk
        main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.rotation = new Vector3(90f, 0f, 0f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
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

    void Update()
    {
        if (!active) return;
        if (Time.time >= until) { End(); return; }

        float left = until - Time.time;
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
        if (left < 1f) pulse *= 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(Time.time * 25f));
        Vector3 c = AbilityKit.Ground(transform.position + Vector3.up) + Vector3.up * 0.06f;
        AbilityKit.Circle(ring, c, radius);
        AbilityKit.Circle(ring2, c, radius * (0.4f + 0.55f * Mathf.Repeat(Time.time * 0.8f, 1f)));
        GlowLine.SetColor(ring, toxic, 0.7f * pulse);
        GlowLine.SetColor(ring2, toxic, 0.4f * (1f - Mathf.Repeat(Time.time * 0.8f, 1f)));
        PassiveGlow.On(gameObject).Set(toxic, 0.7f, 6f);

        if (Time.time >= nextTick)
        {
            nextTick = Time.time + 0.5f;
            foreach (var e in AbilityKit.Enemies(transform.position, radius, gameObject))
                Plague.Afflict(e, gameObject, toxic);
        }
    }

    void End()
    {
        active = false;
        GetComponent<PlayerMovement3D>()?.ClearSpeedModifier(Key);
        PassiveGlow.On(gameObject).Set(toxic, 0f);
        if (smoke != null) { smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(smoke.gameObject, 2f); }
        if (spores != null) { spores.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(spores.gameObject, 2f); }
        if (ring != null) Destroy(ring.gameObject);
        if (ring2 != null) Destroy(ring2.gameObject);
        PowerFx.Puffs(transform.position + Vector3.up * 0.5f, dark, 8, 2f, 0.8f, 0.8f);
    }

    void OnDisable() { if (active) End(); }
}
