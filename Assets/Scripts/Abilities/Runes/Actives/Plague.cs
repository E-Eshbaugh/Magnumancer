using UnityEngine;

/// Plague stacks on someone near a Plaguebearer: each tick in the cloud adds a stack and
/// deals damage that grows with stacks. Stacks wear off out of the cloud. They ooze.
public class Plague : MonoBehaviour
{
    public int maxStacks = 6;
    public float damagePerStackPerTick = 1.5f;   // ticks every 0.5s → 3..18 dps
    public float decayPerSecond = 1.5f;

    GameObject source;
    Color color;
    float stacks, lastAfflicted;
    ParticleSystem ooze;
    Light sickly;

    public static void Afflict(GameObject target, GameObject source, Color color)
    {
        var p = target.GetComponent<Plague>();
        if (p == null) p = target.AddComponent<Plague>();
        p.source = source;
        p.color = color;
        p.Tick();
    }

    void Tick()
    {
        lastAfflicted = Time.time;
        stacks = Mathf.Min(maxStacks, Mathf.Floor(stacks) + 1f);
        DamageEvents.Deal(gameObject, damagePerStackPerTick * stacks, source);
        PowerFx.Sparks(transform.position + Vector3.up * 1.2f, color, 2 + (int)stacks, 2f, 0.4f, 0.07f, -0.2f);
    }

    void Start()
    {
        var go = new GameObject("PlagueOoze");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 1f;
        ooze = go.AddComponent<ParticleSystem>();
        ooze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ooze.main;
        m.loop = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        m.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.black, 0.5f));
        m.gravityModifier = 0.6f;    // drips
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var sh = ooze.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.45f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = PowerFx.SparkMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ooze.Play();

        var lg = new GameObject("PlagueGlow");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = Vector3.up;
        sickly = lg.AddComponent<Light>();
        sickly.type = LightType.Point;
        sickly.range = 2.5f;
        sickly.color = color;
        sickly.shadows = LightShadows.None;
    }

    void Update()
    {
        if (Time.time - lastAfflicted > 0.6f)
            stacks = Mathf.Max(0f, stacks - decayPerSecond * Time.deltaTime);

        if (ooze != null) { var em = ooze.emission; em.rateOverTime = stacks * 6f; }
        if (sickly != null) sickly.intensity = stacks / maxStacks * 1.5f * (0.8f + 0.2f * Mathf.Sin(Time.time * 7f));

        if (stacks <= 0f && Time.time - lastAfflicted > 1f)
        {
            if (ooze != null) Destroy(ooze.gameObject, 1f);
            if (sickly != null) Destroy(sickly.gameObject);
            Destroy(this);
        }
    }
}
