using System.Collections;
using UnityEngine;

/// <summary>
/// A water bomb going off: a geyser column shooting up, droplets sprayed out and raining
/// back down, a thick mist cloud that hides who stepped out of it, ripples racing across
/// the ground and a flash in the caster's color. Built in code, no assets needed.
/// Played when Shadow Clone splits a wizard in two (size 1) and when a clone pops (smaller).
/// </summary>
public class WaterCloneBurst : MonoBehaviour
{
    static Material glowMat, mistMat, lineMat;

    static readonly Color Deep = new Color(0.1f, 0.45f, 1f);
    static readonly Color Aqua = new Color(0.35f, 0.85f, 1f);
    static readonly Color Foam = new Color(0.9f, 0.97f, 1f);

    float size;
    Color tint;

    /// ground: point on the floor under the burst. tint: the caster's color (rings, flash).
    public static WaterCloneBurst Play(Vector3 ground, Color tint, float size = 1f)
    {
        var go = new GameObject("WaterCloneBurst");
        go.transform.position = ground;
        var fx = go.AddComponent<WaterCloneBurst>();
        fx.size = Mathf.Max(0.1f, size);
        fx.tint = tint;
        fx.Build(ground);
        fx.StartCoroutine(fx.Run(ground));
        return fx;
    }

    void Build(Vector3 ground)
    {
        Materials();
        Color water = Color.Lerp(Aqua, tint, 0.25f);
        Quaternion up = Quaternion.Euler(-90f, 0f, 0f); // cones fire along +Z: point them at the sky

        // geyser: a tight column of streaks erupting straight up, then falling back
        var column = Emitter("Geyser", glowMat, ground, up,
            ParticleSystemShapeType.Cone, 0.35f * size, 10f, Mathf.RoundToInt(70 * size),
            new Vector2(9f, 17f) * Mathf.Sqrt(size), new Vector2(0.18f, 0.42f) * size, new Vector2(0.5f, 0.9f), 2.6f,
            Fade(Foam, water, Deep, 1f));
        Stretch(column, 0.06f, 1.4f);

        // spray: droplets flung out in a wide crown, raining back down
        var spray = Emitter("Spray", glowMat, ground + Vector3.up * 0.3f, up,
            ParticleSystemShapeType.Cone, 0.5f * size, 68f, Mathf.RoundToInt(110 * size),
            new Vector2(5f, 12f) * Mathf.Sqrt(size), new Vector2(0.08f, 0.2f) * size, new Vector2(0.6f, 1.1f), 2.2f,
            Fade(Foam, water, Deep, 1f));
        Stretch(spray, 0.05f, 1.2f);

        // sheet: a low splash ring skimming outward along the floor
        var sheet = Emitter("Sheet", glowMat, ground + Vector3.up * 0.15f, up,
            ParticleSystemShapeType.Cone, 0.6f * size, 84f, Mathf.RoundToInt(60 * size),
            new Vector2(6f, 9f) * Mathf.Sqrt(size), new Vector2(0.25f, 0.45f) * size, new Vector2(0.3f, 0.5f), 0.8f,
            Fade(Foam, water, water, 0.8f));
        Drag(sheet, 2.5f);

        // mist: a thick cloud swallowing the wizard for a moment, so nobody sees who's who
        var mist = Emitter("Mist", mistMat, ground + Vector3.up * 0.9f * size, Quaternion.identity,
            ParticleSystemShapeType.Sphere, 0.9f * size, 0f, Mathf.RoundToInt(22 * size),
            new Vector2(1.5f, 4f) * size, new Vector2(1.8f, 3.2f) * size, new Vector2(0.9f, 1.5f), -0.05f,
            Fade(Foam, Color.Lerp(Foam, Aqua, 0.4f), Color.Lerp(Foam, Deep, 0.3f), 0.8f));
        Drag(mist, 3f);
        Grow(mist, 0.5f, 1.15f, 1.5f);

        // glints: bright motes catching the light inside the spray
        var glints = Emitter("Glints", glowMat, ground + Vector3.up * size, Quaternion.identity,
            ParticleSystemShapeType.Sphere, 1f * size, 0f, Mathf.RoundToInt(35 * size),
            new Vector2(2f, 6f) * size, new Vector2(0.05f, 0.12f) * size, new Vector2(0.4f, 0.9f), 0.4f,
            Fade(Color.white, Foam, Aqua, 1f));
        Grow(glints, 1f, 1.3f, 0f);
    }

    IEnumerator Run(Vector3 ground)
    {
        var flash = MakeFlash(ground + Vector3.up * 1.2f * size);
        Color ringColor = Color.Lerp(Aqua, tint, 0.5f);
        var ringA = MakeRing("RippleA", 0.22f * size);
        var ringB = MakeRing("RippleB", 0.14f * size);

        const float ringTime = 0.7f, ringBDelay = 0.14f, flashTime = 0.35f;
        float t = 0f;
        while (t < ringTime + ringBDelay)
        {
            t += Time.deltaTime;
            Ripple(ringA, ground, t / ringTime, 3.4f * size, ringColor);
            Ripple(ringB, ground, (t - ringBDelay) / ringTime, 2.3f * size, Foam);
            if (flash != null) flash.intensity = 10f * size * (1f - Mathf.Clamp01(t / flashTime));
            yield return null;
        }

        Destroy(ringA.gameObject);
        Destroy(ringB.gameObject);
        if (flash != null) Destroy(flash.gameObject);

        // let the last droplets land and the mist thin out
        yield return new WaitForSeconds(1.2f);
        Destroy(gameObject);
    }

    static void Ripple(LineRenderer ring, Vector3 ground, float f, float radius, Color c)
    {
        if (f < 0f) { GlowLine.SetColor(ring, c, 0f); return; }
        f = Mathf.Clamp01(f);
        float eased = 1f - (1f - f) * (1f - f);
        float r = Mathf.Lerp(0.3f, radius, eased);
        int n = ring.positionCount;
        Vector3 center = ground + Vector3.up * 0.08f;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            // a little wobble so it reads as water, not a laser
            float wob = 1f + 0.05f * Mathf.Sin(a * 5f + f * 9f);
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * wob);
        }
        GlowLine.SetColor(ring, c, 1f - f);
        ring.widthMultiplier = Mathf.Lerp(1.5f, 0.25f, f);
    }

    // ---------- Builders ----------

    ParticleSystem Emitter(string name, Material mat, Vector3 at, Quaternion rot,
        ParticleSystemShapeType shapeType, float radius, float angle, int count,
        Vector2 speed, Vector2 startSize, Vector2 life, float gravity, Gradient colors)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(at, rot);
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
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = shapeType;
        shape.radius = Mathf.Max(0.01f, radius);
        if (shapeType == ParticleSystemShapeType.Cone) shape.angle = angle;

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

    static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = velocityScale;
        r.lengthScale = lengthScale;
    }

    static void Drag(ParticleSystem ps, float drag)
    {
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = drag;
    }

    static void Grow(ParticleSystem ps, float start, float mid, float end)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, start), new Keyframe(0.3f, mid), new Keyframe(1f, end)));
    }

    static Gradient Fade(Color a, Color b, Color c, float alpha) => new Gradient
    {
        colorKeys = new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.3f), new GradientColorKey(c, 1f) },
        alphaKeys = new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.85f, 0.55f), new GradientAlphaKey(0f, 1f) }
    };

    LineRenderer MakeRing(string ringName, float width)
    {
        var ring = GlowLine.Make(transform, ringName, 48, width, lineMat);
        ring.loop = true;
        return ring;
    }

    Light MakeFlash(Vector3 at)
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.Lerp(Aqua, tint, 0.4f);
        light.range = 8f * size;
        light.intensity = 10f * size;
        light.shadows = LightShadows.None;
        return light;
    }

    // shared by every burst (none of them animate the materials themselves)
    static void Materials()
    {
        if (glowMat == null) glowMat = GlowLine.CreateParticleMaterial(2.2f, additive: true);
        if (mistMat == null) mistMat = GlowLine.CreateParticleMaterial(1f, additive: false);
        if (lineMat == null) lineMat = GlowLine.CreateMaterial(2.5f);
    }
}
