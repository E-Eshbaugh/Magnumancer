using UnityEngine;

/// <summary>
/// The wizard's active ability charge, shown on the wizard, in their theme color:
///  • flames lick up around them (world space, so they stream behind when running)
///  • a glowing haze hugs their body (local space, so it stays locked to them)
///  • a soft halo glows behind them
///  • a ring at their feet fills like a clock as it charges
/// Everything grows as the ability recharges. The moment it's ready the ring snaps
/// shut with a shockwave and flash, and the whole aura keeps throbbing (like the
/// crest indicator) until it's used — so "loaded" is unmistakable.
/// The body effects follow the wizard's rendered model (not the collider), so they
/// stay centred on them from every angle.
/// </summary>
[RequireComponent(typeof(PlayerMovement3D))]
public class WizardChargeAura : MonoBehaviour
{
    [Header("Flames")]
    public float radius = 0.5f;
    public float minRate = 3f;         // particles/sec just after using the ability
    public float chargedRate = 30f;    // ...just before it's ready
    public float readyRate = 60f;      // ...when ready
    public float flameHeight = 2.2f;   // how high flames rise over their life
    public float flameSize = 0.42f;

    [Header("Body Aura")]
    [Tooltip("Haze particles/sec hugging the body when fully charged")]
    public float auraRate = 45f;
    public float auraSize = 0.55f;
    [Tooltip("Halo size behind the wizard (world units)")]
    public float haloSize = 2.6f;
    [Range(0f, 1f)] public float haloChargedAlpha = 0.35f;
    [Range(0f, 1f)] public float haloReadyAlpha = 0.7f;

    [Header("Glow")]
    public float lightRange = 4f;
    public float chargedLight = 1.2f;
    public float readyLight = 3f;

    [Header("Ready Pulse")]
    [Tooltip("Radians/sec — matches the crest glow's pulse feel")]
    public float pulseSpeed = 5f;

    [Header("Charge Ring")]
    public float ringRadius = 0.85f;
    public float chargingRingWidth = 0.07f;
    public float readyRingWidth = 0.16f;
    [Range(0f, 1f)] public float chargingRingAlpha = 0.45f;
    [Tooltip("How far the shockwave spreads when the ability becomes ready")]
    public float shockwaveRadius = 3f;
    public float shockwaveTime = 0.45f;

    WizardAbilityController ability;
    PlayerMovement3D movement;
    PlayerHealthControl health;
    ParticleSystem flames, aura;
    ParticleSystem.EmissionModule emission, auraEmission;
    ParticleSystem.MainModule main, auraMain;
    Light glow;
    Color color;
    Material flameMat, haloMat;
    Transform halo;
    MeshRenderer haloRenderer;
    MaterialPropertyBlock haloBlock;
    float bodyHeight = 1.8f, bodyCenter = 0.9f, bodyRadius = 0.4f;
    bool wasReady;
    float readySince;

    // visible body (skinned meshes; guns excluded), measured every frame
    Renderer[] bodyRenderers = new Renderer[0];
    Vector3 bodyWorldCenter;
    Vector3 bodyWorldSize = new Vector3(0.8f, 1.8f, 0.8f);

    LineRenderer chargeRing, shockwave;
    Material ringMat;
    float lastCharge, lastWave, lastFresh;
    bool lastReady, lastHidden;
    const int RingPoints = 64;


    public static WizardChargeAura AddTo(GameObject player, WizardData wizard)
    {
        if (player == null) return null;
        var aura = player.GetComponent<WizardChargeAura>();
        if (aura == null) aura = player.AddComponent<WizardChargeAura>();
        aura.SetColor(WizardShade.Of(player));
        return aura;
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        health = GetComponent<PlayerHealthControl>();
        ability = GetComponentInChildren<WizardAbilityController>();
        if (ability == null) ability = GetComponentInParent<WizardAbilityController>();
        MeasureBody();
        FindBodyRenderers();
        BuildFlames();
        BuildBodyAura();
        BuildHalo();
        BuildLight();
        BuildRings();
        SetColor(Color.white);
    }

    public void SetColor(Color theme)
    {
        color = GlowLine.Brighten(theme);
        if (glow) glow.color = color;
        if (flames)
        {
            // born hot (near white), burning into the theme color, then fading out
            var col = flames.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 0f),
                    new GradientColorKey(color, 0.35f),
                    new GradientColorKey(color * 0.8f, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.5f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            });
        }
        if (aura)
        {
            // fades in and out in place: a steady shimmer around the body
            var col = aura.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.35f), 0f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                }
            });
        }
    }

    void Update()
    {
        if (flames == null) return;

        bool hidden = health != null && !health.IsStanding;
        float charge = ability != null ? ability.Charge : 0f;
        bool ready = !hidden && charge >= 1f;

        if (ready && !wasReady)
        {
            // "Loaded": burst of flame, shockwave, flash, and a da-dum on the controller
            readySince = Time.time;
            flames.Emit(45);
            if (aura) aura.Emit(30);
            var pad = movement != null ? movement.gamepad : null;
            Rumble.Play(pad, 0.5f, 0.9f, 0.12f, fade: false);
            Rumble.Play(pad, 0.3f, 0.6f, 0.35f);
        }
        wasReady = ready;

        float rate, light, size, body, haloA;
        float wave = 0f, fresh = 0f;
        if (hidden)
        {
            rate = 0f; light = 0f; size = 1f; body = 0f; haloA = 0f;
        }
        else if (ready)
        {
            // throb: sharp peaks like the crest glow, a little stronger right after it charges
            wave = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((Time.time - readySince) * pulseSpeed), 2f);
            fresh = Mathf.Clamp01(1f - (Time.time - readySince) / 1.5f);
            rate = readyRate * Mathf.Lerp(0.6f, 1.3f, wave);
            light = readyLight * Mathf.Lerp(0.45f, 1f, wave) + fresh * readyLight * 2f;
            size = Mathf.Lerp(1f, 1.35f, wave) + fresh * 0.4f;
            body = Mathf.Lerp(0.75f, 1.25f, wave) + fresh * 0.5f;
            haloA = haloReadyAlpha * Mathf.Lerp(0.55f, 1f, wave) + fresh * 0.3f;
        }
        else
        {
            // builds as it charges, most of it in the last stretch
            float k = Mathf.Pow(charge, 1.5f);
            rate = Mathf.Lerp(minRate, chargedRate, k);
            light = chargedLight * k;
            size = Mathf.Lerp(0.6f, 1f, k);
            body = k * 0.7f;
            haloA = haloChargedAlpha * k;
        }

        emission.rateOverTime = rate;
        main.startSizeMultiplier = flameSize * size;
        auraEmission.rateOverTime = auraRate * body;
        auraMain.startSizeMultiplier = auraSize * Mathf.Max(0.6f, body);
        glow.intensity = light;
        glow.enabled = light > 0.01f;

        // positions are placed in LateUpdate, after animation has posed the body
        lastCharge = charge; lastReady = ready; lastHidden = hidden;
        lastWave = wave; lastFresh = fresh;
        haloAlpha = haloA; haloSizeK = size;
    }

    float haloAlpha, haloSizeK;

    void LateUpdate()
    {
        if (flames == null) return;
        TrackBody();
        UpdateHalo(haloAlpha, haloSizeK);
        UpdateRings();
    }

    // ---------- Following the visible body ----------

    void FindBodyRenderers()
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (!IsGunPart(r)) list.Add(r);
        if (list.Count == 0)
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                if (!IsGunPart(r) && !r.name.StartsWith("Aura")) list.Add(r);
        bodyRenderers = list.ToArray();
        bodyWorldCenter = transform.position + Vector3.up * bodyCenter;
    }

    static bool IsGunPart(Renderer r) => r.GetComponentInParent<GunOrbitController>() != null;

    void TrackBody()
    {
        bool any = false;
        Bounds b = default;
        foreach (var r in bodyRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        if (any)
        {
            bodyWorldCenter = b.center;
            bodyWorldSize = b.size;
        }
        else
        {
            // hidden (spawn bolt): keep the last size, follow the player
            bodyWorldCenter = transform.position + Vector3.up * (bodyWorldSize.y * 0.5f);
        }

        // keep the body haze centred on (and sized to) the model
        if (aura != null)
        {
            var t = aura.transform;
            t.position = bodyWorldCenter;
            Vector3 ls = transform.lossyScale;
            float w = Mathf.Max(bodyWorldSize.x, bodyWorldSize.z);
            var shape = aura.shape;
            shape.scale = new Vector3(
                w * 1.15f / Mathf.Max(0.01f, ls.x),
                bodyWorldSize.y * 1.05f / Mathf.Max(0.01f, ls.y),
                w * 1.15f / Mathf.Max(0.01f, ls.z));
        }
        if (glow != null) glow.transform.position = bodyWorldCenter;
    }

    // ---------- Charge ring ----------

    void BuildRings()
    {
        ringMat = GlowLine.CreateMaterial(2.5f);
        chargeRing = GlowLine.Make(transform, "AuraChargeRing", RingPoints + 1, chargingRingWidth, ringMat);
        shockwave = GlowLine.Make(transform, "AuraShockwave", RingPoints, readyRingWidth, ringMat);
        shockwave.loop = true;
        shockwave.enabled = false;
        chargeRing.enabled = false;
    }

    void UpdateRings()
    {
        Vector3 feet = transform.position + Vector3.up * 0.06f;

        if (lastHidden || lastCharge <= 0.001f)
        {
            chargeRing.enabled = false;
        }
        else if (!lastReady)
        {
            // fills clockwise like a clock hand as the ability charges
            int n = Mathf.Max(2, Mathf.CeilToInt(lastCharge * RingPoints) + 1);
            chargeRing.loop = false;
            chargeRing.positionCount = n;
            float sweep = lastCharge * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 0.5f - sweep * i / (n - 1);
                chargeRing.SetPosition(i, feet + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius);
            }
            chargeRing.startWidth = chargeRing.endWidth = chargingRingWidth;
            GlowLine.SetColor(chargeRing, color, chargingRingAlpha * Mathf.Lerp(0.5f, 1f, lastCharge));
            chargeRing.enabled = true;
        }
        else
        {
            // ready: a closed, bright, throbbing ring
            chargeRing.loop = true;
            chargeRing.positionCount = RingPoints;
            float r = ringRadius * (1f + 0.08f * lastWave + 0.15f * lastFresh);
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f;
                chargeRing.SetPosition(i, feet + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
            float w = readyRingWidth * Mathf.Lerp(0.8f, 1.4f, lastWave) + lastFresh * readyRingWidth;
            chargeRing.startWidth = chargeRing.endWidth = w;
            GlowLine.SetColor(chargeRing, Color.Lerp(color, Color.white, 0.25f * lastWave + 0.5f * lastFresh),
                Mathf.Lerp(0.7f, 1f, lastWave));
            chargeRing.enabled = true;
        }

        // shockwave rolling out the moment it became ready
        float since = Time.time - readySince;
        if (lastReady && since < shockwaveTime)
        {
            float t = since / shockwaveTime;
            float eased = 1f - (1f - t) * (1f - t);
            float r = Mathf.Lerp(ringRadius, shockwaveRadius, eased);
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f;
                shockwave.SetPosition(i, feet + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
            shockwave.startWidth = shockwave.endWidth = Mathf.Lerp(readyRingWidth * 2.5f, readyRingWidth * 0.5f, t);
            GlowLine.SetColor(shockwave, Color.Lerp(Color.white, color, t), 1f - t);
            shockwave.enabled = true;
        }
        else shockwave.enabled = false;
    }

    // ---------- Builders ----------

    void BuildFlames()
    {
        var go = new GameObject("ChargeAura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.1f;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward

        flames = go.AddComponent<ParticleSystem>();
        flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        main = flames.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; // flames trail when you run
        main.maxParticles = 400;
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        emission = flames.emission;
        emission.rateOverTime = 0f;

        var shape = flames.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.3f; // mostly around the edge, a ring of fire

        // rise and taper like flames
        var vel = flames.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.y = new ParticleSystem.MinMaxCurve(flameHeight * 0.8f, flameHeight * 1.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var sizeLife = flames.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.1f)));

        var noise = flames.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 1f;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        flameMat = GlowLine.CreateMaterial(2f);
        var tex = SoftDot();
        if (flameMat.HasProperty("_BaseMap")) flameMat.SetTexture("_BaseMap", tex);
        if (flameMat.HasProperty("_MainTex")) flameMat.SetTexture("_MainTex", tex);
        rend.sharedMaterial = flameMat;

        flames.Play();
    }

    // Body size from the CharacterController so the aura fits any wizard model
    void MeasureBody()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) return;
        bodyHeight = Mathf.Max(0.5f, cc.height);
        bodyCenter = cc.center.y;
        bodyRadius = Mathf.Max(0.2f, cc.radius);
    }

    void BuildBodyAura()
    {
        var go = new GameObject("BodyAura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * bodyCenter;

        aura = go.AddComponent<ParticleSystem>();
        aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        auraMain = aura.main;
        auraMain.loop = true;
        auraMain.duration = 1f;
        auraMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        auraMain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        auraMain.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        auraMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        auraMain.simulationSpace = ParticleSystemSimulationSpace.Local; // locked to the wizard
        auraMain.maxParticles = 200;
        auraMain.scalingMode = ParticleSystemScalingMode.Hierarchy;

        auraEmission = aura.emission;
        auraEmission.rateOverTime = 0f;

        // a capsule-ish shell just outside the body
        var shape = aura.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        shape.radiusThickness = 0.15f;
        shape.scale = new Vector3(bodyRadius * 2.6f, bodyHeight * 1.05f, bodyRadius * 2.6f);

        // slow shimmer upward
        var vel = aura.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sizeLife = aura.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.7f)));

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = flameMat; // same soft additive glow
        rend.sortingFudge = -10f;       // draw over the flames

        aura.Play();
    }

    // Big soft glow behind the wizard, always facing the camera
    void BuildHalo()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "AuraHalo";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * bodyCenter;
        halo = go.transform;

        haloMat = GlowLine.CreateMaterial(1.6f);
        var tex = SoftDot();
        if (haloMat.HasProperty("_BaseMap")) haloMat.SetTexture("_BaseMap", tex);
        if (haloMat.HasProperty("_MainTex")) haloMat.SetTexture("_MainTex", tex);

        haloRenderer = go.GetComponent<MeshRenderer>();
        haloRenderer.sharedMaterial = haloMat;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
        haloBlock = new MaterialPropertyBlock();
        haloRenderer.enabled = false;
    }

    void UpdateHalo(float alpha, float size)
    {
        if (haloRenderer == null) return;
        haloRenderer.enabled = alpha > 0.01f;
        if (!haloRenderer.enabled) return;

        // tint via the material's base color (quads have no vertex colors)
        var c = color * 1.6f;
        c.a = Mathf.Clamp01(alpha);
        haloBlock.SetColor("_BaseColor", c);
        haloBlock.SetColor("_Color", c);
        haloRenderer.SetPropertyBlock(haloBlock);

        var cam = Camera.main;
        Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
        // centred on the body; pushed back only along the view direction (a small
        // amount) so it sits behind the model without sliding sideways on screen
        halo.position = bodyWorldCenter + fwd * Mathf.Min(0.35f, bodyWorldSize.x * 0.5f);
        halo.rotation = Quaternion.LookRotation(fwd, cam != null ? cam.transform.up : Vector3.up);

        float s = haloSize * Mathf.Lerp(0.8f, 1.1f, Mathf.Clamp01(size - 0.6f));
        Vector3 ls = transform.lossyScale;
        float aspect = Mathf.Clamp(bodyWorldSize.y / 1.8f, 0.6f, 1.6f) * 1.15f;
        halo.localScale = new Vector3(s / Mathf.Max(0.01f, ls.x), s * aspect / Mathf.Max(0.01f, ls.y), 1f);
    }

    void BuildLight()
    {
        var go = new GameObject("ChargeGlow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.8f;
        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = lightRange;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    static Texture2D SoftDot() => GlowLine.SoftDot();

    void OnDestroy()
    {
        if (flameMat != null) Destroy(flameMat);
        if (haloMat != null) Destroy(haloMat);
        if (ringMat != null) Destroy(ringMat);
    }
}
