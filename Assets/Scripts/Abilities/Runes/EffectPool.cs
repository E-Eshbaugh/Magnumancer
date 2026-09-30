using UnityEngine;

/// <summary>
/// Fills a ground circle with Emberguard's molten lava effect (the Blazing Ruin dash
/// trail), reshaped from a strip into a disc. Also used recolored as an ice sheet
/// (Rimefield): no smoke, a calm white→blue surface instead of the lava's flicker.
/// Visual only: the ring's GroundHazard does the damage/slow.
/// </summary>
public static class EffectPool
{
    public enum Style { Lava, Ice }

    static GameObject lavaPrefab;
    static bool searched;

    /// The lava trail prefab, found through whichever wizard's ability is Blazing Ruin
    public static GameObject LavaPrefab()
    {
        if (lavaPrefab != null || searched) return lavaPrefab;
        searched = true;
        foreach (var wiz in Resources.LoadAll<WizardData>("Wizards"))
        {
            var dash = wiz.activeAbilityPrefab != null ? wiz.activeAbilityPrefab.GetComponent<FireDashAbility>() : null;
            if (dash != null && dash.LavaTrailPrefab != null) { lavaPrefab = dash.LavaTrailPrefab; break; }
        }
        return lavaPrefab;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { lavaPrefab = null; searched = false; }

    const float TrailArea = 1.67f * 10f;   // the dash trail's emitter footprint

    // Ice look
    static readonly Color IceWhite = new Color(0.92f, 0.97f, 1f);
    static readonly Color IceBlue = new Color(0.45f, 0.75f, 1f);
    const float IceLifetimeScale = 3f;     // slower, calmer surface (rate scaled down to match)

    public static GameObject Spawn(Vector3 at, float ringRadius, float duration, Style style)
    {
        var prefab = LavaPrefab();
        if (prefab == null) return null;

        var pool = Object.Instantiate(prefab, at, Quaternion.identity);
        pool.name = style == Style.Ice ? "IcePool" : "LavaPool";
        foreach (var trail in pool.GetComponentsInChildren<LavaTrail>()) Object.Destroy(trail); // no falling, no double damage
        foreach (var col in pool.GetComponentsInChildren<Collider>()) Object.Destroy(col);

        float r = ringRadius * 0.85f; // stay just inside the ring
        float longestLife = 0f;

        foreach (var ps in pool.GetComponentsInChildren<ParticleSystem>())
        {
            bool surface = ps.transform == pool.transform;
            if (!surface && style == Style.Ice)
            {
                Object.Destroy(ps.gameObject); // no smoke on ice
                continue;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            var shape = ps.shape;
            var emission = ps.emission;

            if (surface)
            {
                // flat disc on the ground
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = r;
                shape.radiusThickness = 1f;
                shape.position = new Vector3(0f, 0.12f, 0f);
                shape.rotation = new Vector3(90f, 0f, 0f);
                shape.scale = Vector3.one;
                emission.rateOverTimeMultiplier *= Mathf.PI * r * r / TrailArea;

                if (style == Style.Ice) MakeIce(ps, ref main, ref emission);
            }
            else
            {
                // lava smoke keeps rising from a square that fits inside the ring
                ps.transform.localPosition = new Vector3(0f, ps.transform.localPosition.y, 0f);
                float side = r * 1.4f;
                shape.position = Vector3.zero;
                shape.scale = new Vector3(side, side, shape.scale.z);
                emission.rateOverTimeMultiplier *= side * side / 10f;
            }

            longestLife = Mathf.Max(longestLife, main.startLifetime.constantMax);
            ps.Play();
        }

        pool.AddComponent<PoolLifetime>().Init(duration, longestLife);
        return pool;
    }

    // Recolor the lava surface into a calm sheet of ice
    static void MakeIce(ParticleSystem ps, ref ParticleSystem.MainModule main, ref ParticleSystem.EmissionModule emission)
    {
        // longer-lived particles at a lower rate: same density, no bubbling flicker
        var life = main.startLifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.constantMin * IceLifetimeScale, life.constantMax * IceLifetimeScale);
        emission.rateOverTimeMultiplier /= IceLifetimeScale;
        main.startColor = Color.white;

        // one smooth gradient (the lava randomly picks between two, which flickers)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(IceWhite, 0f),
                new GradientColorKey(IceBlue, 0.55f),
                new GradientColorKey(IceWhite, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.2f),
                new GradientAlphaKey(0.85f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        });

        // the lava material glows orange: give this copy a cold glow instead
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null && rend.sharedMaterial != null)
        {
            var mat = new Material(rend.sharedMaterial) { name = "IcePool" };
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", IceBlue * 0.6f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            rend.sharedMaterial = mat;
            ps.gameObject.AddComponent<DestroyMaterialOnDestroy>().material = mat;
        }
    }
}
