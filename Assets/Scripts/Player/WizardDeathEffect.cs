using System.Collections;
using UnityEngine;

/// <summary>
/// The wizard going out: the mirror image of their arrival (WizardSpawnEffect). They
/// burst in a fireball of their theme color, their bolt shoots back up into the sky,
/// shock rings roll out with their element's particle burst, and a scorch mark is left
/// on the ground. A final elimination is bigger and slows time for a beat; losing a
/// single life plays a smaller version where they fell.
/// </summary>
public class WizardDeathEffect : MonoBehaviour
{
    [Header("Final elimination")]
    public float slowMoScale = 0.3f;
    [Tooltip("Real-time seconds the slow-mo lasts")]
    public float slowMoTime = 0.5f;

    [Header("Soul bolt")]
    public float boltHeight = 16f;
    public float boltRiseTime = 0.15f;
    public float boltLingerTime = 0.4f;
    public float boltWidth = 0.4f;
    public int boltSegments = 14;
    public float boltJitter = 0.55f;

    [Header("Scorch")]
    public float scorchHold = 3f;
    public float scorchFade = 1.5f;

    static Material fireMat, hotMat, smokeMat, darkLineMat;
    static float slowMoOwnerScale = -1f; // the time scale we set, so we only undo our own

    Color color;
    float size;
    bool final;
    WizardData wizard;

    /// Plays the death at `position` (the wizard's feet). final = out of lives.
    public static WizardDeathEffect Play(Vector3 position, WizardData wizard, bool final)
    {
        var host = new GameObject(final ? "WizardDeath" : "WizardLifeLost");
        host.transform.position = position;
        var fx = host.AddComponent<WizardDeathEffect>();
        fx.wizard = wizard;
        fx.final = final;
        fx.size = final ? 1f : 0.6f;
        fx.color = GlowLine.Brighten(WizardSpawnEffect.ThemeColorOf(wizard));
        fx.StartCoroutine(fx.Run());
        return fx;
    }

    IEnumerator Run()
    {
        Vector3 ground = AbilityKit.Ground(transform.position + Vector3.up * 0.5f);
        Vector3 chest = ground + Vector3.up * 1f;
        EnsureMaterials();

        if (final) StartCoroutine(SlowMo());

        // 1) Boom: fireball, sparks, smoke, flash
        Fireball(chest);
        var flash = MakeFlash(chest);
        CameraShake.Shake(final ? 0.35f : 0.2f, final ? 0.5f : 0.25f);
        Rumble.Blast(chest, 8f * size, final ? 0.8f : 0.4f);

        // 2) Same flourishes as the arrival: rings and the element burst
        AbilityKit.Shockwave(ground, 5f * size, color, 0.55f);
        if (wizard != null && wizard.spawnEffectPrefab != null)
        {
            var burst = Instantiate(wizard.spawnEffectPrefab, ground + Vector3.up * 0.2f, Quaternion.identity);
            Destroy(burst, 4f);
        }
        var scorch = MakeScorch(ground);

        // 3) The bolt that brought them in carries them back out
        var outline = GlowLine.Make(transform, "BoltOutline", boltSegments + 1, boltWidth * 1.6f, darkLineMat);
        var bolt = GlowLine.Make(transform, "Bolt", boltSegments + 1, boltWidth, AbilityKit.Glow());
        var core = GlowLine.Make(transform, "BoltCore", boltSegments + 1, boltWidth * 0.35f, AbilityKit.Glow());
        Color rim = Color.Lerp(color, Color.black, 0.8f);
        Color hot = Color.Lerp(color, Color.white, 0.75f);

        float t = 0f, flicker = 0f;
        bool secondRing = false;
        float total = boltRiseTime + boltLingerTime;
        while (t < total)
        {
            t += Time.deltaTime;
            flicker -= Time.deltaTime;

            float reach = Mathf.Clamp01(t / boltRiseTime);
            float fade = t <= boltRiseTime ? 1f : 1f - (t - boltRiseTime) / boltLingerTime;
            if (flicker <= 0f)
            {
                ShapeBolt(outline, bolt, core, ground, reach);
                flicker = 0.045f;
            }
            GlowLine.SetColor(outline, rim, 0.7f * fade);
            GlowLine.SetColor(bolt, color, fade);
            GlowLine.SetColor(core, hot, fade);
            bolt.widthMultiplier = core.widthMultiplier = outline.widthMultiplier = Mathf.Lerp(0.4f, 1f, fade) * size;

            if (!secondRing && t > 0.12f)
            {
                secondRing = true;
                AbilityKit.Shockwave(ground, 3.2f * size, hot, 0.45f);
            }
            if (flash != null) flash.intensity = 14f * size * Mathf.Clamp01(1f - t / total);
            yield return null;
        }
        outline.enabled = bolt.enabled = core.enabled = false;
        if (flash != null) flash.enabled = false;

        // 4) Scorch lingers, then fades; particles finish on their own
        yield return FadeScorch(scorch);
        Destroy(gameObject, 1.5f);
    }

    // Time slows for a beat on a final elimination
    IEnumerator SlowMo()
    {
        if (GamePause.IsPaused || !Mathf.Approximately(Time.timeScale, 1f)) yield break;
        Time.timeScale = slowMoOwnerScale = slowMoScale;
        yield return new WaitForSecondsRealtime(slowMoTime);
        EndSlowMo();
    }

    static void EndSlowMo()
    {
        // Don't un-pause the game or stomp someone else's time scale
        if (slowMoOwnerScale > 0f && !GamePause.IsPaused && Mathf.Approximately(Time.timeScale, slowMoOwnerScale))
            Time.timeScale = 1f;
        slowMoOwnerScale = -1f;
    }

    // Scene changes mid slow-mo must not leave the next scene slowed down
    void OnDestroy() { if (final) EndSlowMo(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => slowMoOwnerScale = -1f;

    // ---------- Fireball ----------

    void Fireball(Vector3 center)
    {
        Color dark = Color.Lerp(color, Color.black, 0.75f);
        Color hot = Color.Lerp(color, Color.white, 0.7f);

        // rolling fire: white-hot, to theme color, to dark smoke
        var fire = Burst("Fire", fireMat, center, 0.4f * size, Mathf.RoundToInt(45 * size),
            new Vector2(3f, 9f) * size, new Vector2(1.2f, 2.2f) * size, new Vector2(0.55f, 1f), 0f, 3.5f,
            Gradient3(hot, color, dark, 1f));
        Grow(fire, 0.4f, 1.3f, 1.7f);

        // blinding core
        var core = Burst("Core", hotMat, center, 0.2f * size, 6,
            new Vector2(0f, 1f), new Vector2(2.5f, 3.5f) * size, new Vector2(0.12f, 0.2f), 0f, 0f,
            Gradient3(Color.white, hot, color, 1f));
        Grow(core, 0.6f, 1.2f, 1.4f);

        // embers flung out, falling back down
        var embers = Burst("Embers", fireMat, center, 0.3f * size, Mathf.RoundToInt(60 * size),
            new Vector2(8f, 16f) * size, new Vector2(0.08f, 0.16f), new Vector2(0.6f, 1.2f), 1.2f, 1.2f,
            Gradient3(hot, color, color, 1f));
        var r = embers.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1.5f;
        Grow(embers, 1f, 0.8f, 0.2f);

        // a smoke column that hangs after the flames
        var smoke = Burst("Smoke", smokeMat, center, 0.6f * size, Mathf.RoundToInt(16 * size),
            new Vector2(1f, 3f) * size, new Vector2(1.8f, 3f) * size, new Vector2(1.5f, 2.2f), -0.15f, 2f,
            Gradient3(Color.Lerp(dark, Color.gray, 0.3f), new Color(0.15f, 0.15f, 0.15f), new Color(0.1f, 0.1f, 0.1f), 0.55f),
            delay: 0.12f);
        Grow(smoke, 0.5f, 1.2f, 2f);
    }

    ParticleSystem Burst(string name, Material mat, Vector3 at, float radius, int count,
        Vector2 speed, Vector2 startSize, Vector2 life, float gravity, float drag, Gradient colors, float delay = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize.x, startSize.y);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.maxParticles = count + 8;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.01f, radius);

        if (drag > 0f)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
        }

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(colors);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sortingOrder = 10;

        ps.Play();
        return ps;
    }

    static void Grow(ParticleSystem ps, float start, float mid, float end)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, start), new Keyframe(0.3f, mid), new Keyframe(1f, end)));
    }

    static Gradient Gradient3(Color a, Color b, Color c, float alpha) => new Gradient
    {
        colorKeys = new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.25f), new GradientColorKey(c, 0.7f) },
        alphaKeys = new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.85f, 0.5f), new GradientAlphaKey(0f, 1f) }
    };

    // ---------- Bolt, light, scorch ----------

    void ShapeBolt(LineRenderer outline, LineRenderer bolt, LineRenderer core, Vector3 ground, float reach)
    {
        // rises from the ground up into the sky (the arrival, played backwards)
        Vector3 top = ground + Vector3.up * boltHeight;
        Vector3 tip = Vector3.Lerp(ground, top, reach);
        GlowLine.Bolt(bolt, ground, tip, boltJitter * size);
        for (int i = 0; i < bolt.positionCount; i++)
        {
            Vector3 p = bolt.GetPosition(i);
            core.SetPosition(i, p);
            outline.SetPosition(i, p);
        }
    }

    Light MakeFlash(Vector3 at)
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 10f * size;
        light.intensity = 14f * size;
        light.shadows = LightShadows.None;
        return light;
    }

    Renderer MakeScorch(Vector3 ground)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Scorch";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, true);
        go.transform.position = ground + Vector3.up * 0.03f;
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = Vector3.one * 3.2f * size;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = smokeMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SetScorchAlpha(r, 0.7f);
        return r;
    }

    IEnumerator FadeScorch(Renderer scorch)
    {
        yield return new WaitForSeconds(scorchHold);
        float t = 0f;
        while (t < scorchFade && scorch != null)
        {
            t += Time.deltaTime;
            SetScorchAlpha(scorch, 0.7f * (1f - t / scorchFade));
            yield return null;
        }
    }

    readonly MaterialPropertyBlock block = new();

    void SetScorchAlpha(Renderer r, float a)
    {
        var c = Color.Lerp(color, Color.black, 0.9f);
        c.a = a;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }

    static void EnsureMaterials()
    {
        if (fireMat == null) fireMat = GlowLine.CreateParticleMaterial(1.6f, false);
        if (hotMat == null) hotMat = GlowLine.CreateParticleMaterial(3f, true);
        if (smokeMat == null) smokeMat = GlowLine.CreateParticleMaterial(1f, false);
        if (darkLineMat == null) darkLineMat = GlowLine.CreateMaterial(1f, false);
    }
}
