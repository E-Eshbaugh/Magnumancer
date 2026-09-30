using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Emberguard — Brand of Flereous: each attack brands the target with fire.
/// At five brands, they ignite in a burst of flame that also hits enemies around them.
/// </summary>
public class BrandOfFlereousPassive : WizardPassive
{
    public int brandsToIgnite = 5;
    public float igniteDamage = 25f;
    public float igniteRadius = 3f;
    [Tooltip("A shotgun blast's pellets only brand once; also paces brands (0.3s = an ignite every ~1.5s at best, ~17 bonus dps)")]
    public float brandCooldownPerTarget = 0.3f;

    readonly Dictionary<GameObject, float> lastBrandTime = new();
    bool igniting;

    void OnEnable() => DamageEvents.Damaged += OnDamaged;
    void OnDisable() => DamageEvents.Damaged -= OnDamaged;

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (igniting || attacker != gameObject || !IsEnemy(victim)) return;

        if (lastBrandTime.TryGetValue(victim, out float last) && Time.time - last < brandCooldownPerTarget)
            return;
        lastBrandTime[victim] = Time.time;

        var fx = StatusEffects.Of(victim);
        if (fx.AddBrand(gameObject) >= brandsToIgnite)
        {
            fx.ClearBrands(gameObject);
            Ignite(victim.transform.position);
        }
    }

    void Ignite(Vector3 center)
    {
        SpawnEffect(center + Vector3.up, 3f);
        Explosions.AffectWorld(center, igniteRadius, igniteDamage, null);
        ElementReactions.OnElementArea(center, igniteRadius, gameObject, Element.Fire);

        igniting = true; // the burst's own damage shouldn't add brands
        var hit = new HashSet<GameObject>();
        foreach (var col in Physics.OverlapSphere(center, igniteRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var target = DamageEvents.RootOf(col);
            if (!IsEnemy(target) || !hit.Add(target)) continue;
            DamageEvents.Deal(target, igniteDamage, gameObject);
            ElementReactions.AbilityHit(target, gameObject, Element.Fire, igniteDamage);
        }
        igniting = false;
    }
}
