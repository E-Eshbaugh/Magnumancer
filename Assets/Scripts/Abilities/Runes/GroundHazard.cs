using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A glowing circle on the ground that does things to whoever stands in it: damage,
/// slow, pull toward the center, freeze, root on contact, or speed up its owner.
/// Rune abilities (Overgrowth, Undertow, Static Field, Rimefield, Thornsnare, toxic
/// puddles...) configure one of these instead of each writing their own zone.
/// </summary>
public class GroundHazard : MonoBehaviour
{
    public GameObject owner;
    public float radius = 3f;
    public float duration = 5f;
    public Color color = Color.white;

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
    readonly HashSet<PlayerMovement3D> slowed = new();
    PlayerMovement3D boostedOwner;
    float spawnTime, nextTick;
    LineRenderer edge, inner;
    bool triggered;
    string key;

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
    }

    bool IsSnare => rootDuration > 0f;

    void Update()
    {
        float age = Time.time - spawnTime;
        if (age >= duration || triggered) { Destroy(gameObject); return; }

        Vector3 c = transform.position + Vector3.up * 0.07f;
        float fadeIn = Mathf.Clamp01(age / 0.2f), fadeOut = Mathf.Clamp01((duration - age) / 0.4f);
        float armed = IsSnare && age < armTime ? 0.35f : 1f;
        AbilityKit.Circle(edge, c, radius);
        AbilityKit.Circle(inner, c, radius * (0.35f + 0.55f * Mathf.Repeat(age * 0.6f, 1f)));
        GlowLine.SetColor(edge, color, 0.9f * fadeIn * fadeOut * armed);
        GlowLine.SetColor(inner, color, 0.45f * (1f - Mathf.Repeat(age * 0.6f, 1f)) * fadeOut * armed);

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

        // Damage / freeze ticks
        if (Time.time >= nextTick)
        {
            nextTick = Time.time + Tick;
            foreach (var e in inside)
            {
                if (damagePerSecond > 0f) DamageEvents.Deal(e, damagePerSecond * Tick, owner);
                if (freezeStacksPerSecond > 0f && Random.value < freezeStacksPerSecond * Tick)
                    StatusEffects.Of(e).AddFreeze();
            }
        }
    }

    void OnDestroy()
    {
        foreach (var m in slowed) if (m != null) m.ClearSpeedModifier(key);
        if (boostedOwner != null) boostedOwner.ClearSpeedModifier(key);
    }
}
