using UnityEngine;

/// <summary>
/// A wizard's health, shown on the ground around them so nobody has to look away to the
/// corners: a ring at their feet, outside the charge clock (WizardChargeAura), split into
/// four quarters of their current life in their shade color. Damage eats it from the end
/// back toward 12 o'clock; the lost stretch lingers in white for a beat, then drains. Hits
/// flash it and kick it outward. Below lowHealth it turns ember-red and throbs like a
/// heartbeat, and the wizard starts trailing smoke. At full health and quiet it dims so
/// the arena doesn't get busy.
///
/// It's world-space geometry (not UI), so it goes through the pixel-art camera with the
/// rest of the scene and always sits where the wizard is.
/// </summary>
public class WizardLifeRing : MonoBehaviour
{
    [Header("Shape")]
    public float radius = 1.15f;
    public float width = 0.1f;
    [Tooltip("Gap between quarters, in degrees")]
    public float gapDegrees = 9f;

    [Header("Feel")]
    public float chipDelay = 0.35f;     // lost stretch hangs this long...
    public float chipDrainSpeed = 1.2f; // ...then drains (fraction of a life per second)
    public float flashTime = 0.18f;
    [Tooltip("How far a hit kicks the ring outward (fraction of radius) for the biggest hits")]
    public float hitKick = 0.15f;
    [Range(0f, 1f)] public float lowHealth = 0.3f;
    [Tooltip("Opacity at full health when not recently hit")]
    [Range(0f, 1f)] public float idleAlpha = 0.45f;
    [Range(0f, 1f)] public float trackAlpha = 0.35f;
    public static Color LowColor = new Color(1f, 0.25f, 0.12f);

    [Header("Low Health Smoke")]
    public float smokeRate = 10f;   // puffs/sec at 1 HP (none above lowHealth)

    const int Segments = 4;
    const int PointsPerSegment = 16;

    PlayerHealthControl health;
    Transform follow;   // the wizard, or a decoy standing in for them
    Color color;

    LineRenderer[] track = new LineRenderer[Segments];
    LineRenderer[] chip = new LineRenderer[Segments];
    LineRenderer[] fill = new LineRenderer[Segments];
    Material glowMat, trackMat, smokeMat;
    ParticleSystem smoke;
    ParticleSystem.EmissionModule smokeEmission;

    float frac = 1f, chipFrac = 1f, chipHoldUntil;
    float flashT, kick, alpha, lastChange = -99f;

    public static WizardLifeRing AddTo(PlayerHealthControl health)
    {
        if (health == null) return null;
        var ring = health.GetComponent<WizardLifeRing>();
        if (ring == null) ring = health.gameObject.AddComponent<WizardLifeRing>();
        ring.Bind(health, health.transform, false);
        return ring;
    }

    /// The same ring around decoy (Shadow Clone), showing the real wizard's health, so the
    /// clone can't be picked out by a missing ring. Goes when the decoy does.
    public static WizardLifeRing AddDecoy(PlayerHealthControl health, Transform decoy)
    {
        if (health == null || decoy == null) return null;
        var ring = decoy.gameObject.AddComponent<WizardLifeRing>();
        ring.Bind(health, decoy, true);
        return ring;
    }

    void Bind(PlayerHealthControl h, Transform t, bool decoy)
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
        health = h;
        follow = t;
        color = GlowLine.Brighten(WizardShade.Of(h.gameObject));
        frac = chipFrac = Fraction(h.currentHealth, h.maxHealth);
        health.OnHealthChanged += HandleHealthChanged;
        if (glowMat == null) Build();

        // a decoy picks up exactly where the real ring is, so they look the same from the first frame
        var real = decoy ? h.GetComponent<WizardLifeRing>() : null;
        if (real != null)
        {
            chipFrac = real.chipFrac;
            chipHoldUntil = real.chipHoldUntil;
            alpha = real.alpha;
            lastChange = real.lastChange;
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
        if (glowMat != null) Destroy(glowMat);
        if (trackMat != null) Destroy(trackMat);
        if (smokeMat != null) Destroy(smokeMat);
    }

    void Build()
    {
        glowMat = GlowLine.CreateMaterial(2f);
        trackMat = GlowLine.CreateMaterial(1f, additive: false);   // a dark groove where health is missing
        for (int i = 0; i < Segments; i++)
        {
            track[i] = GlowLine.Make(transform, $"LifeTrack{i}", PointsPerSegment + 1, width, trackMat);
            chip[i] = GlowLine.Make(transform, $"LifeChip{i}", PointsPerSegment + 1, width, glowMat);
            fill[i] = GlowLine.Make(transform, $"LifeFill{i}", PointsPerSegment + 1, width, glowMat);
            // drawn in order, over the charge ring's floor glow
            track[i].sortingOrder = 0;
            chip[i].sortingOrder = 1;
            fill[i].sortingOrder = 2;
        }
        BuildSmoke();
    }

    void BuildSmoke()
    {
        var go = new GameObject("LowHealthSmoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 1.2f;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        smoke = go.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = smoke.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startColor = new Color(0.12f, 0.1f, 0.1f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // trails behind when they run
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 60;

        smokeEmission = smoke.emission;
        smokeEmission.rateOverTime = 0f;

        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        var size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        var col = smoke.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }
        });

        var r = go.GetComponent<ParticleSystemRenderer>();
        smokeMat = GlowLine.CreateParticleMaterial(1f, additive: false);
        r.sharedMaterial = smokeMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        smoke.Play();
    }

    void HandleHealthChanged(float current, float max)
    {
        float next = Fraction(current, max);
        if (next < frac - 1e-4f)
        {
            // hit: flash, kick outward harder for bigger chunks, lost stretch lingers then drains
            float lost = frac - next;
            flashT = flashTime;
            kick = Mathf.Max(kick, Mathf.Clamp01(0.3f + lost * 3f));
            chipFrac = Mathf.Max(chipFrac, frac);
            chipHoldUntil = Time.time + chipDelay;
        }
        else if (next > frac + 0.5f)
        {
            // new life: straight back to full
            chipFrac = next;
            flashT = flashTime;
        }
        frac = next;
        lastChange = Time.time;
    }

    static float Fraction(float current, float max) => max > 0f ? Mathf.Clamp01(current / max) : 0f;

    void LateUpdate()
    {
        if (health == null) { Destroy(this); return; }

        bool show = !health.IsDead && health.isActiveAndEnabled
                    && !WizardSpawnEffect.IsArriving(health.transform);
        if (!show)
        {
            SetVisible(false);
            alpha = 0f;
            smokeEmission.rateOverTime = 0f;
            return;
        }

        float dt = Time.deltaTime;
        flashT = Mathf.Max(0f, flashT - dt);
        kick = Mathf.MoveTowards(kick, 0f, dt * 6f);

        if (Time.time >= chipHoldUntil)
            chipFrac = Mathf.MoveTowards(chipFrac, frac, chipDrainSpeed * dt);
        if (chipFrac < frac) chipFrac = frac;

        // dim when healthy and quiet
        bool calm = frac >= 0.999f && Time.time - lastChange > 2f;
        alpha = Mathf.MoveTowards(alpha, calm ? idleAlpha : 1f, dt * 3f);

        // low: ember red with a heartbeat (two quick beats, a rest), quicker the lower it gets
        bool low = frac <= lowHealth;
        float beat = 0f;
        Color c = color;
        if (low)
        {
            float danger = 1f - frac / Mathf.Max(0.01f, lowHealth);
            float phase = Mathf.Repeat(Time.time * Mathf.Lerp(1.4f, 2.4f, danger), 1f);
            beat = Mathf.Max(Pulse(phase, 0f), 0.7f * Pulse(phase, 0.22f));
            c = Color.Lerp(LowColor, Color.white, 0.35f * beat);
        }
        float flash = flashT / flashTime;
        c = Color.Lerp(c, Color.white, 0.85f * flash);

        float r = radius * (1f + hitKick * kick * kick + 0.05f * beat);
        float w = width * (1f + 0.6f * flash + 0.5f * beat);
        Vector3 center = follow.position + Vector3.up * 0.06f;
        float a = alpha;

        float gap = gapDegrees * Mathf.Deg2Rad;
        float quarter = Mathf.PI * 0.5f;
        for (int i = 0; i < Segments; i++)
        {
            // segment i covers health [i/4, (i+1)/4], clockwise from 12 o'clock
            float start = Mathf.PI * 0.5f - i * quarter - gap * 0.5f;
            float span = quarter - gap;
            float fillK = Mathf.Clamp01(frac * Segments - i);
            float chipK = Mathf.Clamp01(chipFrac * Segments - i);

            Arc(track[i], center, r, start, span, 1f, w);
            GlowLine.SetColor(track[i], Color.black, trackAlpha * a);
            Arc(chip[i], center, r, start, span, chipK, w);
            GlowLine.SetColor(chip[i], new Color(1f, 0.95f, 0.85f), 0.9f * a);
            Arc(fill[i], center, r, start, span, fillK, w);
            GlowLine.SetColor(fill[i], c, a);
        }

        smokeEmission.rateOverTime = low ? smokeRate * Mathf.Lerp(0.3f, 1f, 1f - frac / Mathf.Max(0.01f, lowHealth)) : 0f;
    }

    // a quick rise and fall centred on `at` within a 0..1 cycle
    static float Pulse(float phase, float at)
    {
        float d = Mathf.Abs(phase - at);
        return Mathf.Clamp01(1f - d / 0.09f);
    }

    static void Arc(LineRenderer lr, Vector3 center, float r, float start, float span, float k, float w)
    {
        if (k <= 0.001f) { lr.enabled = false; return; }
        int n = Mathf.Max(2, Mathf.CeilToInt(k * PointsPerSegment) + 1);
        lr.positionCount = n;
        float sweep = span * k;
        for (int i = 0; i < n; i++)
        {
            float a = start - sweep * i / (n - 1);
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
        }
        lr.startWidth = lr.endWidth = w;
        lr.enabled = true;
    }

    void SetVisible(bool on)
    {
        for (int i = 0; i < Segments; i++)
        {
            if (track[i]) track[i].enabled = on;
            if (chip[i]) chip[i].enabled = on;
            if (fill[i]) fill[i].enabled = on;
        }
    }
}
