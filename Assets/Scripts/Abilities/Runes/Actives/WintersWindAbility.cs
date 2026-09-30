using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Frostwarden — Rune II: Winter's Wind. A freezing gust of icy smoke billows out in a
/// cone, hurling everyone caught in it back hard (and chilling them a little). The
/// shove lands as the front of the gust reaches each enemy.
public class WintersWindAbility : MonoBehaviour, IActiveAbility
{
    public float range = 8f;
    public float coneDegrees = 70f;
    public float knockback = 45f;
    public float damage = 8f;
    public int freezeStacks = 1;
    [Tooltip("How fast the gust front travels (times the knockback to the smoke)")]
    public float gustSpeed = 18f;

    static readonly Color Frost = new Color(0.85f, 0.94f, 1f);

    static Material smokeMat, streakMat;

    public void Activate(GameObject caster)
    {
        Vector3 dir = AbilityKit.AimDir(caster);
        Vector3 origin = caster.transform.position;
        Color theme = AbilityKit.Theme(caster);

        SpawnGust(origin + Vector3.up * 0.9f + dir * 0.6f, dir, theme);
        CameraShake.Shake(0.12f, 0.25f);

        foreach (var e in AbilityKit.Enemies(origin, range, caster))
        {
            Vector3 to = e.transform.position - origin; to.y = 0f;
            if (Vector3.Angle(dir, to) > coneDegrees * 0.5f) continue;
            StartCoroutine(Blow(e, caster, dir, to.magnitude / gustSpeed));
        }
    }

    IEnumerator Blow(GameObject enemy, GameObject caster, Vector3 dir, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (enemy == null) yield break;
        // mostly along the gust, a little outward so a crowd scatters
        Vector3 away = enemy.transform.position - caster.transform.position; away.y = 0f;
        Vector3 push = (dir * 0.75f + away.normalized * 0.25f).normalized;
        AbilityKit.Knockback(enemy, push * knockback);
        if (damage > 0f) DamageEvents.Deal(enemy, damage, caster);
        var fx = StatusEffects.Of(enemy);
        for (int i = 0; i < freezeStacks; i++) fx.AddFreeze();
        Rumble.Play(enemy, 0.5f, 0.4f, 0.25f);
    }

    // ---------- visuals ----------

    void SpawnGust(Vector3 origin, Vector3 dir, Color theme)
    {
        var go = new GameObject("WintersWind");
        go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir, Vector3.up));

        float travelTime = range / gustSpeed;
        BuildSmoke(go, travelTime, theme);
        BuildStreaks(go, travelTime);
        Destroy(go, 2.5f);
    }

    // Billowing, swirling icy smoke that rushes forward and spreads as it slows
    void BuildSmoke(GameObject go, float travelTime, Color theme)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(travelTime * 1.2f, travelTime * 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(gustSpeed * 0.8f, gustSpeed * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(Frost, Color.Lerp(Frost, theme, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 30),
            new ParticleSystem.Burst(0.08f, 25),
            new ParticleSystem.Burst(0.16f, 20),
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneDegrees * 0.4f;
        shape.radius = 0.4f;

        // drag: fast at first, then it slows and billows out
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = gustSpeed * 0.25f;
        limit.dampen = 0.12f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1.8f), new Keyframe(1f, 3f)));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.2f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 1.5f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.1f),
                new GradientAlphaKey(0.35f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (smokeMat == null) smokeMat = GlowLine.CreateParticleMaterial(1.1f, additive: false);
        rend.sharedMaterial = smokeMat;

        ps.Play();
    }

    // Thin, fast wind lines streaking through the smoke
    void BuildStreaks(GameObject parent, float travelTime)
    {
        var go = new GameObject("Streaks");
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(travelTime * 0.6f, travelTime * 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(gustSpeed * 1.2f, gustSpeed * 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startColor = Frost;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18), new ParticleSystem.Burst(0.1f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneDegrees * 0.35f;
        shape.radius = 0.6f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Frost, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        });

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch; // long thin lines along their motion
        rend.velocityScale = 0.12f;
        rend.lengthScale = 2f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (streakMat == null) streakMat = GlowLine.CreateParticleMaterial(1.8f, additive: true);
        rend.sharedMaterial = streakMat;

        ps.Play();
    }
}
