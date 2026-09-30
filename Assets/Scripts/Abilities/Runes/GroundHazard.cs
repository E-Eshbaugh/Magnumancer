using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A glowing circle on the ground that does things to whoever stands in it: damage,
/// slow, pull toward the center, freeze, root on contact, or speed up its owner.
/// Rune abilities (Overgrowth, Undertow, Static Field, Rimefield, Thornsnare, toxic
/// puddles...) configure one of these instead of each writing their own zone.
/// It carries an element (its owner's unless set): whoever stands in it gets that
/// element's status and can set off reactions, and it reacts with other zones
/// (fire Combusts poison, Conduct electrifies water).
/// </summary>
public class GroundHazard : MonoBehaviour, IElementZone, IElectrifiable
{
    public GameObject owner;
    public float radius = 3f;
    public float duration = 5f;
    public Color color = Color.white;
    [Tooltip("None = the owner's element")]
    public Element element;

    [Header("Effects on enemies")]
    public float damagePerSecond;
    [Range(0f, 1f)] public float slowMultiplier = 1f;     // 1 = no slow
    public float pullStrength;                             // units/sec toward the center
    public float freezeStacksPerSecond;
    [Tooltip("Snare: roots the first enemy that steps in for this long, deals burstDamage, then vanishes")]
    public float rootDuration;
    public float burstDamage;
    [Tooltip("Seconds before a snare can trigger")]
    public float armTime;

    [Header("Owner")]
    public float ownerSpeedMultiplier = 1f;

    const float Tick = 0.25f;
    const float ShockTick = 0.5f;
    readonly HashSet<PlayerMovement3D> slowed = new();
    PlayerMovement3D boostedOwner;
    float spawnTime, nextTick;
    LineRenderer edge, inner;
    bool triggered;
    string key;

    // Conduct: electrified water shocks everyone in it (the one who electrified it excepted)
    GameObject electrifiedBy;
    float electrifiedUntil, electrifyDps, nextShock, nextCrackle;

    public static GroundHazard Spawn(GameObject owner, Vector3 center, float radius, float duration, Color color)
    {
        var go = new GameObject("GroundHazard");
        go.transform.position = center;
        var h = go.AddComponent<GroundHazard>();
        h.owner = owner; h.radius = radius; h.duration = duration; h.color = color;
        return h;
    }

    void Start()
    {
        spawnTime = Time.time;
        key = "hazard" + GetEntityId();
        edge = GlowLine.Make(transform, "Edge", 56, 0.12f, AbilityKit.Glow());
        inner = GlowLine.Make(transform, "Inner", 40, 0.06f, AbilityKit.Glow());
        edge.loop = inner.loop = true;

        if (element == Element.None) element = Elements.Of(owner);
        ElementZones.Register(this);
    }

    bool IsSnare => rootDuration > 0f;
    bool Electrified => Time.time < electrifiedUntil;

    void Update()
    {
        float age = Time.time - spawnTime;
        if (age >= duration || triggered) { Destroy(gameObject); return; }

        Vector3 c = transform.position + Vector3.up * 0.07f;
        float fadeIn = Mathf.Clamp01(age / 0.2f), fadeOut = Mathf.Clamp01((duration - age) / 0.4f);
        float armed = IsSnare && age < armTime ? 0.35f : 1f;
        Color tint = Electrified ? Color.Lerp(color, Elements.ColorOf(Element.Lightning), 0.6f + 0.4f * Random.value) : color;
        AbilityKit.Circle(edge, c, radius);
        AbilityKit.Circle(inner, c, radius * (0.35f + 0.55f * Mathf.Repeat(age * 0.6f, 1f)));
        GlowLine.SetColor(edge, tint, 0.9f * fadeIn * fadeOut * armed);
        GlowLine.SetColor(inner, tint, 0.45f * (1f - Mathf.Repeat(age * 0.6f, 1f)) * fadeOut * armed);

        var inside = AbilityKit.Enemies(transform.position, radius, owner);

        // Snare: first enemy in (after arming) gets rooted and it's spent
        if (IsSnare)
        {
            if (age >= armTime && inside.Count > 0)
            {
                foreach (var e in inside)
                {
                    StatusEffects.Of(e).Root(rootDuration);
                    if (burstDamage > 0f) DamageEvents.Deal(e, burstDamage, owner);
                }
                AbilityKit.Shockwave(transform.position, radius * 1.6f, color, 0.35f);
                triggered = true;
            }
            return;
        }

        // Pull continuously
        if (pullStrength > 0f)
            foreach (var e in inside)
            {
                Vector3 to = transform.position - e.transform.position; to.y = 0f;
                if (to.magnitude > 0.4f) AbilityKit.Knockback(e, to.normalized * pullStrength);
            }

        // Slow while inside
        var now = new HashSet<PlayerMovement3D>();
        if (slowMultiplier < 1f)
            foreach (var e in inside)
            {
                var m = e.GetComponent<PlayerMovement3D>();
                if (m == null) continue;
                m.SetSpeedModifier(key, slowMultiplier);
                now.Add(m);
            }
        foreach (var m in slowed)
            if (m != null && !now.Contains(m)) m.ClearSpeedModifier(key);
        slowed.Clear();
        slowed.UnionWith(now);

        // Owner buff while standing in it
        if (ownerSpeedMultiplier != 1f && owner != null)
        {
            var om = owner.GetComponent<PlayerMovement3D>();
            bool ownerIn = om != null && (om.transform.position - transform.position).sqrMagnitude <= radius * radius;
            if (ownerIn) { om.SetSpeedModifier(key, ownerSpeedMultiplier); boostedOwner = om; }
            else if (boostedOwner != null) { boostedOwner.ClearSpeedModifier(key); boostedOwner = null; }
        }

        if (Electrified) Shock(c);

        // Damage / freeze / element ticks
        if (Time.time >= nextTick)
        {
            nextTick = Time.time + Tick;
            foreach (var e in inside)
            {
                if (damagePerSecond > 0f) DamageEvents.Deal(e, damagePerSecond * Tick, owner);
                if (freezeStacksPerSecond > 0f && Random.value < freezeStacksPerSecond * Tick)
                    StatusEffects.Of(e).AddFreeze();
                ElementReactions.ZoneHit(e, owner, element, damagePerSecond * Tick);
            }
        }
    }

    // ---------- Conduct ----------

    public void Electrify(GameObject by, float time, float dps)
    {
        electrifiedBy = by;
        electrifyDps = dps;
        electrifiedUntil = Mathf.Max(electrifiedUntil, Time.time + time);
        nextShock = Time.time;
    }

    void Shock(Vector3 c)
    {
        Color volt = Elements.ColorOf(Element.Lightning);
        if (Time.time >= nextCrackle)
        {
            nextCrackle = Time.time + 0.07f;
            Vector3 rim = c + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * radius;
            AbilityKit.Zap(rim + Vector3.up * 0.1f, c + Random.insideUnitSphere * radius * 0.5f + Vector3.up * 0.2f, volt, 0.08f, 0.1f);
        }

        if (Time.time < nextShock) return;
        nextShock = Time.time + ShockTick;
        foreach (var e in AbilityKit.Enemies(transform.position, radius, electrifiedBy))
        {
            DamageEvents.Deal(e, electrifyDps * ShockTick, electrifiedBy);
            StatusEffects.Of(e).StunAtLeast(0.4f, 0.3f);
            AbilityKit.Zap(c, AbilityKit.Chest(e), volt, 0.12f, 0.14f);
        }
    }

    // ---------- IElementZone ----------

    public Element ZoneElement => element;
    public GameObject ZoneOwner => owner;
    public Vector3 ZoneCenter => transform.position;
    public float ZoneRadius => radius;
    public float DistanceTo(Vector3 p) => ElementZones.FlatDistance(transform.position, radius, p);
    public void Consume() { triggered = true; ElementZones.Unregister(this); }

    void OnDestroy()
    {
        ElementZones.Unregister(this);
        foreach (var m in slowed) if (m != null) m.ClearSpeedModifier(key);
        if (boostedOwner != null) boostedOwner.ClearSpeedModifier(key);
    }
}
