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
public class GroundHazard : MonoBehaviour, IElementZone, IElectrifiable, IFreezable, IMuddable
{
    public GameObject owner;
    public float radius = 3f;
    public float duration = 5f;
    public Color color = Color.white;
    [Tooltip("None = the owner's element")]
    public Element element;
    /// Left behind by a reaction (Combust's burning patch...): its kills are reaction kills
    [HideInInspector] public Reaction? fromReaction;

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

    [Tooltip("Below 1 = slippery ice (Brittle): everyone inside slides, owner included")]
    [Range(0f, 1f)] public float traction = 1f;
    [Tooltip("Enemies inside can't dash (Mudslide)")]
    public bool blocksDash;

    [Header("Owner")]
    public float ownerSpeedMultiplier = 1f;

    const float Tick = 0.25f;
    const float ShockTick = 0.5f;
    readonly HashSet<StatusEffects> slowed = new();
    readonly HashSet<PlayerMovement3D> sliding = new();
    readonly HashSet<PlayerMovement3D> mired = new();
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

        var inside = AbilityKit.Enemies(transform.position, radius, owner, includeTeammates: fromReaction.HasValue);

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
        var now = new HashSet<StatusEffects>();
        if (slowMultiplier < 1f)
            foreach (var e in inside)
            {
                var m = StatusEffects.Of(e);
                if (m == null) continue;
                m.SetSpeedModifier(key, slowMultiplier);
                now.Add(m);
            }
        foreach (var m in slowed)
            if (m != null && !now.Contains(m)) m.ClearSpeedModifier(key);
        slowed.Clear();
        slowed.UnionWith(now);

        // Ice: everyone slides, the one who made it too (Gang Beasts rules)
        Track(sliding, traction < 1f ? AbilityKit.Enemies(transform.position, radius, null) : null,
              m => m.SetTraction(key, traction), m => m.ClearTraction(key));
        // Mud: enemies can't dash out
        Track(mired, blocksDash ? inside : null,
              m => m.SetDashBlocked(key, true), m => m.SetDashBlocked(key, false));

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
                if (damagePerSecond > 0f)
                {
                    if (fromReaction.HasValue) ElementReactions.DealAs(fromReaction.Value, e, damagePerSecond * Tick, owner);
                    else DamageEvents.Deal(e, damagePerSecond * Tick, owner);
                }
                if (freezeStacksPerSecond > 0f && Random.value < freezeStacksPerSecond * Tick)
                    StatusEffects.Of(e).AddFreeze();
                ElementReactions.ZoneHit(e, owner, element, damagePerSecond * Tick);
            }
        }
    }

    // Applies `on` to players in `who` (null = nobody), `off` to those who left
    void Track(HashSet<PlayerMovement3D> set, List<GameObject> who, System.Action<PlayerMovement3D> on, System.Action<PlayerMovement3D> off)
    {
        var now = new HashSet<PlayerMovement3D>();
        if (who != null)
            foreach (var e in who)
            {
                var m = e.GetComponent<PlayerMovement3D>();
                if (m == null) continue;
                on(m);
                now.Add(m);
            }
        foreach (var m in set)
            if (m != null && !now.Contains(m)) off(m);
        set.Clear();
        set.UnionWith(now);
    }

    // ---------- Reactions reshaping the zone ----------

    float Age => spawnTime > 0f ? Time.time - spawnTime : 0f;

    /// Brittle: this water freezes into slippery ice for `time` seconds. It hurts nobody,
    /// but everyone on it slides.
    public void FreezeOver(GameObject by, float time, float iceTraction)
    {
        owner = by;
        element = Element.Frost;
        color = Elements.ColorOf(Element.Frost);
        pullStrength = 0f;
        damagePerSecond = 0f;
        slowMultiplier = 1f;
        traction = iceTraction;
        electrifiedUntil = 0f;
        duration = Age + time;
        EffectPool.Spawn(transform.position, radius, time, EffectPool.Style.Ice);
        PowerFx.IceShards(transform.position + Vector3.up * 0.3f, color, 18, 5f, 0.2f);
        ElementReactions.OnZoneChanged(this);
    }

    /// Mudslide: this water turns to mud. Enemies in it are slowed and can't dash.
    public void MudOver(GameObject by, float time, float slow)
    {
        owner = by;
        element = Element.Earth;
        color = Elements.ColorOf(Element.Earth);
        pullStrength = 0f;
        damagePerSecond = 0f;
        slowMultiplier = slow;
        blocksDash = true;
        electrifiedUntil = 0f;
        duration = Age + time;
        ElementReactions.MudFx(transform.position, radius);
        ElementReactions.OnZoneChanged(this);
    }

    bool surged;

    /// Overgrowth Surge: water makes the growth swell once: wider, longer and nastier
    public void Surge(float growth, float extraTime)
    {
        if (surged) return;
        surged = true;
        radius *= growth;
        duration += extraTime;
        damagePerSecond *= growth;
        if (slowMultiplier < 1f) slowMultiplier = Mathf.Max(0.2f, 1f - (1f - slowMultiplier) * growth);
        Color nature = Elements.ColorOf(Element.Nature);
        AbilityKit.Shockwave(transform.position, radius, nature, 0.5f);
        for (int i = 0; i < 14; i++)
            BulletFX.Mote(BulletFX.Flavor.Spores, nature, transform.position + Vector3.up * 0.4f + Random.insideUnitSphere * radius * 0.7f, 2f);
    }

    /// Wildfire: vines and brambles catch fire. It burns everyone in it, the Verdant
    /// who grew it included, and spreads to other growth it touches.
    public void Ignite(GameObject by, float time, float dps)
    {
        owner = by;
        element = Element.Fire;
        color = Elements.ColorOf(Element.Fire);
        damagePerSecond = Mathf.Max(damagePerSecond, dps);
        rootDuration = burstDamage = 0f;   // a snare burns up instead of springing
        fromReaction = Reaction.Wildfire;
        pullStrength = 0f;
        duration = Age + time;
        EffectPool.Spawn(transform.position, radius, time, EffectPool.Style.Lava);
        PowerFx.Sparks(transform.position + Vector3.up * 0.3f, color, 30, 6f, 0.7f, 0.08f, -0.3f, Vector3.up, 120f);
        ElementReactions.OnZoneChanged(this);
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
            ElementReactions.DealAs(Reaction.Conduct, e, electrifyDps * ShockTick, electrifiedBy);
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
        foreach (var m in sliding) if (m != null) m.ClearTraction(key);
        foreach (var m in mired) if (m != null) m.SetDashBlocked(key, false);
        if (boostedOwner != null) boostedOwner.ClearSpeedModifier(key);
    }
}
