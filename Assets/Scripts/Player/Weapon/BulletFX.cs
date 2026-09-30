using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bullet visuals in the shooter's wizard colors, so you can tell your rounds from
/// everyone else's: a tracer streak with a fading trail, element-flavored bits shed
/// along the way (embers, frost, toxic drips, void wisps...), a muzzle flash and an
/// impact burst.
///
/// Readability: bloom is off, so additive "glow" alone just clips to white and
/// vanishes on bright floors (ice). Every tracer is layered — a dark outline, a
/// saturated alpha-blended body and a hot additive core — so it reads on dark ground
/// and on white alike. Tracers also sort above other transparent effects (poison
/// clouds, water) so they never disappear inside them.
///
/// Runs on its own object that follows the bullet, so the trail can finish fading
/// after the bullet is destroyed.
/// </summary>
public class BulletFX : MonoBehaviour
{
    public enum Flavor { Sparks, Embers, Frost, Toxic, Void, Water, Spores, Grit, Volt }

    public class Style
    {
        public Color color;    // saturated body
        public Color core;     // hot, near-white center
        public Color outline;  // dark rim for bright floors
        public Flavor flavor;
    }

    // Sorting within the transparent queue: outline under body under core
    const int OrderOutline = 10, OrderBody = 11, OrderCore = 12;

    // ---------- per-bullet ----------

    Bullet bullet;
    Style style;
    Vector3 dir;
    Vector3 head, spawnPos;
    float length, width;
    float shedPerUnit, shedCarry;
    bool ended;
    float tailCatchUp;          // after the bullet ends, how far the tail has closed in
    float nextJitter;
    Vector3[] kinks;            // Voltborn: per-point offsets, re-rolled as it crackles

    LineRenderer outline, body, core;
    TrailRenderer trail;

    /// Dresses up a freshly fired bullet. Call after owner/damage are set.
    public static BulletFX Attach(Bullet bullet, Vector3 dir)
    {
        var style = StyleOf(bullet.owner, bullet.GetComponentInChildren<Light>());

        // The tracer replaces the little brass mesh
        foreach (var r in bullet.GetComponentsInChildren<MeshRenderer>())
            r.enabled = false;

        // Tint the bullet's light: a pool of the wizard's color slides along the floor
        var light = bullet.GetComponentInChildren<Light>();
        if (light != null)
        {
            light.color = style.color;
            light.intensity = Mathf.Max(light.intensity, 2f);
        }

        var go = new GameObject("BulletFX");
        var fx = go.AddComponent<BulletFX>();
        fx.Init(bullet, style, dir);
        return fx;
    }

    void Init(Bullet b, Style s, Vector3 d)
    {
        bullet = b;
        style = s;
        dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        head = spawnPos = b.transform.position;
        transform.position = head;

        // Faster rounds streak longer; harder-hitting rounds are fatter
        float speed = Mathf.Max(1f, b.speed);
        length = Mathf.Clamp(speed * 0.05f, 0.5f, 1.6f);
        width = Mathf.Clamp(0.8f + b.damage / 60f, 0.85f, 1.6f);
        shedPerUnit = ShedRate(s.flavor);

        EnsureMaterials();
        int points = s.flavor == Flavor.Volt ? 5 : 2;
        kinks = new Vector3[points];
        outline = GlowLine.Make(transform, "Outline", points, 0.24f * width, blendMat);
        body = GlowLine.Make(transform, "Body", points, 0.15f * width, blendMat);
        core = GlowLine.Make(transform, "Core", points, 0.06f * width, addMat);
        outline.sortingOrder = OrderOutline;
        body.sortingOrder = OrderBody;
        core.sortingOrder = OrderCore;
        body.endWidth = 0.05f * width;       // taper toward the tail
        core.endWidth = 0.02f * width;
        outline.endWidth = 0.1f * width;
        GlowLine.SetColor(outline, style.outline, style.outline.a);
        body.startColor = style.color;
        body.endColor = new Color(style.color.r, style.color.g, style.color.b, 0.6f);
        core.startColor = style.core;
        core.endColor = new Color(style.core.r, style.core.g, style.core.b, 0.3f);

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = blendMat;
        trail.time = Mathf.Clamp(2.5f / speed, 0.06f, 0.2f);
        trail.minVertexDistance = 0.1f;
        trail.widthMultiplier = 0.13f * width;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.colorGradient = Fade(style.color, 0.75f);
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sortingOrder = OrderOutline;

        Draw();
    }

    void LateUpdate()
    {
        if (!ended && (bullet == null || !bullet.isActiveAndEnabled))
            End();

        Vector3 prev = head;
        if (!ended)
        {
            head = bullet.transform.position;
            transform.position = head;
            Shed(prev, head);
        }
        else
        {
            // tail snaps into the impact point, then we wait for the trail to fade
            tailCatchUp += Time.deltaTime * 25f;
        }

        Draw();
    }

    void Draw()
    {
        float tailLen = Mathf.Min(length, Vector3.Distance(spawnPos, head)) - tailCatchUp;
        bool show = tailLen > 0.02f;
        outline.enabled = body.enabled = core.enabled = show;
        if (!show) return;

        Vector3 tail = head - dir * tailLen;
        int n = body.positionCount;

        // Voltborn rounds crackle: the streak re-kinks every few frames
        if (n > 2 && Time.time >= nextJitter)
        {
            nextJitter = Time.time + 0.03f;
            for (int i = 1; i < n - 1; i++)
                kinks[i] = Random.insideUnitSphere * 0.12f * width;
        }

        for (int i = 0; i < n; i++)
        {
            float f = n > 1 ? i / (float)(n - 1) : 0f;
            Vector3 p = Vector3.Lerp(head, tail, f) + kinks[i];
            outline.SetPosition(i, p);
            body.SetPosition(i, p);
            core.SetPosition(i, p);
        }
    }

    /// Bullet hit something: burst, then let the trail fade out
    public void Impact(Vector3 point, Vector3 normal, bool hitCombatant)
    {
        if (ended) return;
        Shed(head, point);
        head = point;
        transform.position = point;
        Burst(style, point, normal, dir, width, hitCombatant);
        End();
    }

    void End()
    {
        ended = true;
        if (trail != null) trail.emitting = false;
        Destroy(gameObject, (trail != null ? trail.time : 0f) + 0.1f);
    }

    // Element bits dropped along the flight path
    void Shed(Vector3 from, Vector3 to)
    {
        float dist = Vector3.Distance(from, to);
        if (dist < 1e-4f) return;
        shedCarry += dist * shedPerUnit;
        int count = Mathf.Min(8, (int)shedCarry);
        shedCarry -= (int)shedCarry;
        for (int i = 0; i < count; i++)
            FlavorBit(style, Vector3.Lerp(from, to, Random.value), dir, 1f);
    }

    // ---------- styles ----------

    static readonly Dictionary<WizardData, Style> styles = new();

    public static Style StyleOf(GameObject owner, Light fallbackLight = null)
    {
        var wizard = AbilityKit.Wizard(owner);
        if (wizard == null)
            return MakeStyle(fallbackLight != null ? fallbackLight.color : new Color(1f, 0.75f, 0.2f), Flavor.Sparks);
        if (!styles.TryGetValue(wizard, out var s))
            styles[wizard] = s = MakeStyle(WizardSpawnEffect.ThemeColorOf(wizard), FlavorOf(wizard.passive));
        return s;
    }

    static Style MakeStyle(Color theme, Flavor flavor)
    {
        Color.RGBToHSV(theme, out float h, out float s, out float v);
        // Saturated enough to tint a white floor, bright enough to pop on a dark one
        var color = Color.HSVToRGB(h, Mathf.Clamp(s, 0.55f, 0.95f), 1f);
        var rim = Color.HSVToRGB(h, 0.85f, 0.16f);
        rim.a = 0.8f;
        return new Style
        {
            color = color,
            core = Color.Lerp(color, Color.white, 0.6f),
            outline = rim,
            flavor = flavor
        };
    }

    /// Each wizard's element flavor (also their Element, see Elements.Of)
    public static Flavor FlavorOf(PassiveType passive) => passive switch
    {
        PassiveType.BrandOfFlereous => Flavor.Embers,    // Emberguard
        PassiveType.FractalshotShield => Flavor.Frost,   // Frostwarden
        PassiveType.VirulentShroud => Flavor.Toxic,      // Blightward
        PassiveType.LastRites => Flavor.Void,            // The Hollow
        PassiveType.Undercurrent => Flavor.Water,        // Tidebound
        PassiveType.VerdantResurgence => Flavor.Spores,  // Verdant Circle
        PassiveType.Stonebind => Flavor.Grit,            // Granite Vow
        PassiveType.LightningReflex => Flavor.Volt,      // Voltborn
        _ => Flavor.Sparks
    };

    static float ShedRate(Flavor f) => f switch
    {
        Flavor.Void => 3f,
        Flavor.Grit => 2.5f,
        Flavor.Volt => 2.5f,
        Flavor.Sparks => 1.5f,
        _ => 4f
    };

    static readonly Dictionary<(Flavor, Color), Style> moteStyles = new();

    /// One element bit outside of a bullet (status indicators, reactions), through the
    /// same shared particle systems. scale > 1 = bigger and faster.
    public static void Mote(Flavor flavor, Color color, Vector3 p, float scale = 1f)
    {
        if (!moteStyles.TryGetValue((flavor, color), out var s))
            moteStyles[(flavor, color)] = s = MakeStyle(color, flavor);
        FlavorBit(s, p, Vector3.up, scale);
    }

    // One element bit. scale > 1 for impact bursts (bigger, faster).
    static void FlavorBit(Style s, Vector3 p, Vector3 dir, float scale)
    {
        Vector3 r = Random.insideUnitSphere;
        Color hot = Color.Lerp(s.color, s.core, Random.value);
        switch (s.flavor)
        {
            case Flavor.Embers:   // glowing embers that float up, the odd falling spark
                Emit(Glow, p, (Vector3.up * Random.Range(0.6f, 1.6f) + r * 0.6f) * scale, Random.Range(0.1f, 0.18f) * scale, Random.Range(0.3f, 0.55f), hot);
                if (Random.value < 0.35f)
                    Emit(Sparks, p, (r * 2f + Vector3.up) * scale, 0.05f, Random.Range(0.2f, 0.35f), s.core);
                break;
            case Flavor.Frost:    // cold motes that hang in the air and twinkle
                Emit(Glow, p, r * 0.35f * scale, Random.Range(0.08f, 0.14f) * scale, Random.Range(0.45f, 0.7f), s.core);
                if (Random.value < 0.3f)
                    Emit(Flash, p, Vector3.zero, 0.12f * scale, 0.15f, Color.white);
                break;
            case Flavor.Toxic:    // dripping sludge
                Emit(Sparks, p, (Vector3.down * 0.4f + r * 0.4f) * scale, Random.Range(0.08f, 0.12f) * scale, Random.Range(0.35f, 0.55f), s.color);
                if (Random.value < 0.25f)
                    Emit(Smoke, p, r * 0.2f, 0.3f * scale, 0.5f, WithAlpha(s.color, 0.3f));
                break;
            case Flavor.Void:     // shadowy wisps with a purple glint
                Emit(Smoke, p, r * 0.3f * scale, Random.Range(0.25f, 0.4f) * scale, Random.Range(0.3f, 0.45f), WithAlpha(s.outline, 0.7f));
                if (Random.value < 0.4f)
                    Emit(Glow, p, r * 0.5f * scale, 0.1f * scale, 0.3f, s.color);
                break;
            case Flavor.Water:    // droplets flung off the sides
                Emit(Sparks, p, (new Vector3(r.x, 0f, r.z) * 1.6f + Vector3.up * 1.2f) * scale, Random.Range(0.06f, 0.1f) * scale, Random.Range(0.3f, 0.45f), hot);
                break;
            case Flavor.Spores:   // drifting spores
                Emit(Glow, p, (Vector3.up * 0.4f + r * 0.4f) * scale, Random.Range(0.07f, 0.12f) * scale, Random.Range(0.5f, 0.8f), hot);
                break;
            case Flavor.Grit:     // dust puffs and falling chips
                Emit(Smoke, p, r * 0.25f * scale, Random.Range(0.2f, 0.3f) * scale, 0.45f, WithAlpha(Color.Lerp(s.color, Color.gray, 0.5f), 0.4f));
                if (Random.value < 0.5f)
                    Emit(Sparks, p, (r * 1.2f + Vector3.up * 0.8f) * scale, 0.07f, 0.35f, s.color);
                break;
            case Flavor.Volt:     // fast sideways sparks
                Emit(Sparks, p, r.normalized * Random.Range(3f, 6f) * scale, 0.05f, Random.Range(0.08f, 0.15f), s.core);
                break;
            default:
                Emit(Sparks, p, (r * 1.5f + Vector3.up * 0.5f) * scale, 0.05f, Random.Range(0.15f, 0.3f), hot);
                break;
        }
    }

    // ---------- muzzle flash & impacts ----------

    /// How big a shot's flash is, from the total damage it fires (pellets combined)
    public static float ShotPower(int totalDamage) => Mathf.Clamp(Mathf.Sqrt(totalDamage / 12f), 0.6f, 2f);

    public static void MuzzleFlash(GameObject owner, Vector3 pos, Vector3 dir, float power)
    {
        var s = StyleOf(owner);
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
        float jiggle = Random.Range(0.85f, 1.15f);

        // white-hot center and a colored bloom around it
        Emit(Flash, pos, Vector3.zero, 0.55f * power * jiggle, 0.05f, s.core);
        Emit(Glow, pos + dir * 0.15f * power, dir * 1.5f, 0.95f * power * jiggle, 0.07f, s.color);

        // flame tongues: fast, stretched, short-lived streaks in a tight cone
        int tongues = 3 + Mathf.RoundToInt(3f * power);
        for (int i = 0; i < tongues; i++)
        {
            Vector3 v = (dir + Random.insideUnitSphere * 0.3f).normalized * Random.Range(9f, 16f) * Mathf.Sqrt(power);
            Emit(Sparks, pos, v, 0.12f * power, Random.Range(0.04f, 0.08f), i % 2 == 0 ? s.core : s.color);
        }

        // a few stray sparks and a puff of smoke
        for (int i = 0; i < 2 + Mathf.RoundToInt(power * 2f); i++)
        {
            Vector3 v = (dir + Random.insideUnitSphere * 0.9f).normalized * Random.Range(2f, 6f);
            Emit(Sparks, pos, v, 0.05f, Random.Range(0.15f, 0.3f), s.color);
        }
        Emit(Smoke, pos + dir * 0.25f, dir * 1.2f + Vector3.up * 0.3f, 0.3f * power, 0.45f, new Color(0.6f, 0.6f, 0.6f, 0.25f));

        FlashLight(pos, s.color, 3f * power, 3.5f);
    }

    static void Burst(Style s, Vector3 point, Vector3 normal, Vector3 dir, float power, bool hitCombatant)
    {
        Vector3 away = normal.sqrMagnitude > 1e-4f ? normal.normalized : -dir;
        Vector3 bounce = normal.sqrMagnitude > 1e-4f ? Vector3.Reflect(dir, away) : -dir;
        Vector3 at = point + away * 0.05f;

        // flash
        Emit(Flash, at, Vector3.zero, (hitCombatant ? 1f : 0.7f) * power, 0.07f, s.core);
        Emit(Glow, at, Vector3.zero, (hitCombatant ? 1.8f : 1.3f) * power, 0.12f, s.color);

        // hot sparks spraying off the surface
        int sparks = Mathf.RoundToInt((hitCombatant ? 12 : 8) * power);
        for (int i = 0; i < sparks; i++)
        {
            Vector3 v = (bounce + Random.insideUnitSphere * 0.8f).normalized * Random.Range(3f, 8f);
            Emit(Sparks, at, v, Random.Range(0.06f, 0.1f), Random.Range(0.15f, 0.35f), Random.value < 0.5f ? s.core : s.color);
        }

        // an element splash
        for (int i = 0; i < 5; i++)
            FlavorBit(s, at + Random.insideUnitSphere * 0.1f, bounce, 1.6f);

        // walls and floors puff dust
        if (!hitCombatant)
            Emit(Smoke, at + away * 0.1f, away * 0.6f, 0.35f * power, 0.5f, new Color(0.55f, 0.55f, 0.55f, 0.3f));
    }

    // ---------- shared resources ----------

    static Material addMat, blendMat;
    static Material addParticleMat, blendParticleMat, smokeParticleMat;
    static FXRoot root;

    static void EnsureMaterials()
    {
        if (addMat == null) addMat = GlowLine.CreateMaterial(2.5f, true);
        if (blendMat == null) blendMat = GlowLine.CreateMaterial(1.15f, false);
        if (addParticleMat == null) addParticleMat = GlowLine.CreateParticleMaterial(2.5f, true);
        if (blendParticleMat == null) blendParticleMat = GlowLine.CreateParticleMaterial(1.3f, false);
        if (smokeParticleMat == null) smokeParticleMat = GlowLine.CreateParticleMaterial(1f, false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        styles.Clear();
        moteStyles.Clear();
        root = null;
    }

    static FXRoot Root()
    {
        if (root != null) return root;
        EnsureMaterials();
        root = new GameObject("BulletFX Particles").AddComponent<FXRoot>();
        root.flash = Build("Flash", addParticleMat, 0f, false, true, OrderCore);
        root.glow = Build("Glow", blendParticleMat, 0f, false, false, OrderBody);
        root.sparks = Build("Sparks", blendParticleMat, 1.2f, true, false, OrderBody);
        root.smoke = Build("Smoke", smokeParticleMat, -0.05f, false, true, OrderOutline - 1);
        return root;
    }

    static ParticleSystem Flash => Root().flash;
    static ParticleSystem Glow => Root().glow;
    static ParticleSystem Sparks => Root().sparks;
    static ParticleSystem Smoke => Root().smoke;

    // A world-space system we only ever Emit() into
    static ParticleSystem Build(string name, Material mat, float gravity, bool stretch, bool grow, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 6000;
        main.gravityModifier = gravity;
        main.startSpeed = 0f;

        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, 1f, 0.6f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, grow
            ? AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f)
            : AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (stretch) { r.velocityScale = 0.03f; r.lengthScale = 1.5f; }
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = order;

        ps.Play();
        return ps;
    }

    static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color)
    {
        ps.Emit(new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startSize = size,
            startLifetime = life,
            startColor = color,
            applyShapeToPosition = false
        }, 1);
    }

    static void FlashLight(Vector3 pos, Color color, float intensity, float range)
        => Root().Fire(pos, color, intensity, range);

    static Gradient Fade(Color c, float alpha, float holdUntil = 0f) => new Gradient
    {
        colorKeys = new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
        alphaKeys = holdUntil > 0f
            ? new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, holdUntil), new GradientAlphaKey(0f, 1f) }
            : new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) }
    };

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    /// Holds the shared particle systems and a small pool of muzzle-flash lights
    [AddComponentMenu("")]
    class FXRoot : MonoBehaviour
    {
        public ParticleSystem flash, glow, sparks, smoke;

        const int LightCount = 6;
        const float LightFade = 0.07f;
        readonly Light[] lights = new Light[LightCount];
        readonly float[] peaks = new float[LightCount];
        int next;

        public void Fire(Vector3 pos, Color color, float intensity, float range)
        {
            if (lights[next] == null)
            {
                var go = new GameObject("FlashLight");
                go.transform.SetParent(transform, false);
                lights[next] = go.AddComponent<Light>();
                lights[next].type = LightType.Point;
                lights[next].shadows = LightShadows.None;
            }
            var l = lights[next];
            l.transform.position = pos;
            l.color = color;
            l.range = range;
            l.intensity = peaks[next] = intensity;
            l.enabled = true;
            next = (next + 1) % LightCount;
        }

        void Update()
        {
            for (int i = 0; i < LightCount; i++)
            {
                var l = lights[i];
                if (l == null || !l.enabled) continue;
                l.intensity -= peaks[i] * Time.deltaTime / LightFade;
                if (l.intensity <= 0f) { l.enabled = false; peaks[i] = 0f; }
            }
        }
    }
}
