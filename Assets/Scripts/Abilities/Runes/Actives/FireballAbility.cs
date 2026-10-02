using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Emberguard — Rune III: Fireball. Hurls a ball of fire at wherever the nearest enemy
/// is standing when it's cast (they can still move out of the way while it
/// flies). It bursts on landing and leaves a pool of lava. With nobody in range it's
/// thrown along your aim instead.
public class FireballAbility : MonoBehaviour, IActiveAbility
{
    [Tooltip("Farthest enemy it will lock onto")]
    public float targetRange = 20f;
    [Tooltip("Throw distance along your aim when nobody's in range")]
    public float distance = 7f;
    [Tooltip("Flight time for a short and a max-range throw (farther = longer, higher arc)")]
    public float minFlightTime = 0.45f;
    public float maxFlightTime = 0.9f;
    public float blastRadius = 3.5f;
    public float damage = 35f;
    [Tooltip("Bonus damage per Brand of Flereous brand detonated on each enemy caught in the blast")]
    public float damagePerBrand = 8f;
    public float lavaRadius = 2.5f;
    public float lavaDuration = 4f;
    public float lavaDamagePerSecond = 10f;

    public void Activate(GameObject caster) => StartCoroutine(Throw(caster));

    IEnumerator Throw(GameObject caster)
    {
        Color theme = AbilityKit.Theme(caster);
        Vector3 from = AbilityKit.Chest(caster);
        var target = NearestEnemy(caster);
        Vector3 to = target != null
            ? AbilityKit.Ground(target.transform.position + Vector3.up)   // their spot at launch
            : AbilityKit.AimPoint(caster, distance);

        float dist = Vector3.Distance(from, to);
        float reach = Mathf.Clamp01(dist / targetRange);
        float flight = Mathf.Lerp(minFlightTime, maxFlightTime, reach);
        float height = Mathf.Lerp(2f, 4.5f, reach);

        var orb = AbilityKit.GlowOrb(theme, 0.5f);
        var fire = BuildTrail(orb, theme);
        PowerFx.Flash(from, theme, 5f, 4f, 0.2f);
        Rumble.Play(caster, 0.3f, 0.5f, 0.15f);
        yield return AbilityKit.Lob(orb.transform, from, to, flight, height);
        if (fire != null) { fire.transform.SetParent(null, true); fire.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(fire.gameObject, 1.5f); }
        Destroy(orb);

        var wiz = AbilityKit.Wizard(caster);
        foreach (var e in AbilityKit.Enemies(to, blastRadius, caster))
        {
            float k = 1f - Mathf.Clamp01(Vector3.Distance(to, e.transform.position) / blastRadius);
            DamageEvents.Deal(e, damage * Mathf.Lerp(0.4f, 1f, k), caster);

            // detonate their brands
            var fx = StatusEffects.Of(e);
            int brands = caster != null ? fx.BrandCount(caster) : 0;
            if (brands > 0)
            {
                fx.ClearBrands(caster);
                DamageEvents.Deal(e, damagePerBrand * brands, caster);
                Vector3 at = AbilityKit.Chest(e);
                PowerFx.Prefab(wiz != null ? wiz.passiveEffectPrefab : null, at, 2.5f, 0.6f + 0.15f * brands);
                PowerFx.Sparks(at, theme, 10 * brands, 7f, 0.6f, 0.09f, 0.8f);
                PowerFx.Flash(at, theme, 4f + brands * 1.5f, 5f, 0.3f);
                AbilityKit.Shockwave(e.transform.position, 1.2f + 0.3f * brands, Color.white, 0.3f);
                Rumble.Play(e, 0.7f, 0.6f, 0.3f);
            }

            // after the brands pop: Steam on the Soaked, Combust on the Poisoned, else Burning
            ElementReactions.AbilityHit(e, caster, damage);
        }
        Explosions.AffectWorld(to, blastRadius, damage, null);
        ElementReactions.OnElementArea(to, blastRadius, caster, Element.Fire);   // gas clouds go up

        // the blast
        AbilityKit.Shockwave(to, blastRadius, theme, 0.4f);
        AbilityKit.Shockwave(to, blastRadius * 0.6f, Color.white, 0.25f);
        PowerFx.Flash(to + Vector3.up, theme, 12f, 9f, 0.45f);
        PowerFx.Sparks(to + Vector3.up * 0.3f, theme, 45, 9f, 0.8f, 0.1f, 1.2f, Vector3.up, 140f);
        PowerFx.Puffs(to + Vector3.up * 0.8f, new Color(0.18f, 0.12f, 0.1f, 1f), 10, 3f, 1.1f, 1.4f, lift: 1.5f);
        CameraShake.Shake(0.3f, 0.25f);
        Rumble.Blast(to, blastRadius * 2f, 0.8f);
        if (wiz != null && wiz.passiveEffectPrefab != null)
            Destroy(Instantiate(wiz.passiveEffectPrefab, to + Vector3.up * 0.5f, Quaternion.identity), 3f);

        var lava = GroundHazard.Spawn(caster, to, lavaRadius, lavaDuration, theme);
        lava.damagePerSecond = lavaDamagePerSecond;
        lava.slowMultiplier = 0.6f;
        EffectPool.Spawn(to, lavaRadius, lavaDuration, EffectPool.Style.Lava);
    }

    // Embers and smoke streaming off the fireball, plus its light
    static ParticleSystem BuildTrail(GameObject orb, Color theme)
    {
        var l = orb.AddComponent<Light>();
        l.type = LightType.Point; l.color = theme; l.range = 5f; l.intensity = 4f; l.shadows = LightShadows.None;

        var go = new GameObject("FireballTrail");
        go.transform.SetParent(orb.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(theme, Color.white, 0.5f), theme);
        m.simulationSpace = ParticleSystemSimulationSpace.World; // streams behind it
        m.gravityModifier = -0.2f;
        var em = ps.emission; em.rateOverTime = 90f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(theme, 0.3f), new GradientColorKey(new Color(0.3f, 0.08f, 0.02f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }
        });
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = PowerFx.SparkMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // Closest living opponent, including goblins, within targetRange.
    GameObject NearestEnemy(GameObject caster)
    {
        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (var e in AbilityKit.Enemies(caster.transform.position, targetRange, caster))
        {
            float d = (e.transform.position - caster.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

}
