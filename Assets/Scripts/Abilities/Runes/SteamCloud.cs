using UnityEngine;

/// <summary>
/// Steam reaction: a thick, scalding cloud that hides whoever's inside it. Bullets still
/// fly through (and their tracers draw over the steam), you just can't see who's in
/// there. Scalds everyone inside except whoever set it off.
/// </summary>
public class SteamCloud : MonoBehaviour
{
    const float Tick = 0.5f;

    GameObject owner;
    float radius, duration, dps, born, nextTick;
    ParticleSystem steam;

    public static SteamCloud Spawn(Vector3 at, float radius, float duration, float dps, GameObject owner)
    {
        var go = new GameObject("SteamCloud");
        go.transform.position = at;
        var s = go.AddComponent<SteamCloud>();
        s.owner = owner; s.radius = radius; s.duration = duration; s.dps = dps;
        s.born = Time.time;
        s.nextTick = Time.time + Tick;
        s.Build();
        return s;
    }

    void Build()
    {
        Color water = Elements.ColorOf(Element.Water);
        Vector3 mid = transform.position + Vector3.up;

        // the flash-boil
        PowerFx.Puffs(mid, Color.white, 14, 6f, 1.4f, 0.5f, additive: true, lift: 2f);
        PowerFx.Sparks(mid, Color.Lerp(water, Color.white, 0.5f), 30, 7f, 0.45f, 0.07f, 1.5f, Vector3.up, 160f);
        AbilityKit.Shockwave(transform.position, radius * 1.1f, Color.white, 0.4f);

        steam = gameObject.AddComponent<ParticleSystem>();
        steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = steam.main;
        main.loop = true;
        main.duration = duration;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.55f, radius * 0.85f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.93f, 0.96f, 0.9f), new Color(1f, 1f, 1f, 0.95f));
        main.gravityModifier = -0.03f;
        main.maxParticles = 400;

        var emission = steam.emission;
        emission.rateOverTime = 45f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });   // full cloud right away

        var shape = steam.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = radius * 0.7f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // dome upward

        var col = steam.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
        });

        var size = steam.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));

        var rot = steam.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var r = GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PowerFx.SmokeMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = 5;   // covers players; bullet tracers (10+) still draw on top

        steam.Play();
        Rumble.Blast(transform.position, radius * 1.5f, 0.4f);
    }

    void Update()
    {
        float age = Time.time - born;
        if (age >= duration)
        {
            if (steam != null && steam.isEmitting)
            {
                steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 1.8f);
            }
            return;
        }

        // wisps curling off the top
        if (Random.value < 0.3f)
            BulletFX.Mote(BulletFX.Flavor.Frost, Color.white, transform.position + Random.insideUnitSphere * radius * 0.6f + Vector3.up * 1.8f, 1.5f);

        if (Time.time < nextTick) return;
        nextTick = Time.time + Tick;
        foreach (var e in AbilityKit.Enemies(transform.position, radius, owner))
            DamageEvents.Deal(e, dps * Tick, owner);
    }
}
