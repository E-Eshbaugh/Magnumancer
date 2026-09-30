using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How blasts interact with the world beyond hurting players: they set off Viper mines
/// (which chain into each other), cook off nearby grenades, damage destructible
/// items like healing crystals, and shove everyone caught in them (the one who set it
/// off too). Every explosion calls AffectWorld once.
/// Bullets use Shoot() so mines and grenades can be popped with gunfire too.
/// </summary>
public static class Explosions
{
    // Chain reactions ripple outward instead of all popping on the same frame
    const float MinChainDelay = 0.06f;
    const float MaxChainDelay = 0.22f;

    static readonly Collider[] buffer = new Collider[128];

    /// Shove per point of blast damage at the center (0 = blasts don't shove), capped
    public static float ShovePerDamage = 0.35f;
    public static float MaxShove = 14f;

    /// center/radius: the blast. damage: its max damage (falls off with distance).
    /// source: the exploding object itself, so it doesn't re-trigger itself.
    public static void AffectWorld(Vector3 center, float radius, float damage, GameObject source, bool shove = true)
    {
        if (radius <= 0f) return;

        // everyone nearby feels it (victims inside the radius the most)
        Rumble.Blast(center, radius * 1.5f);

        // ...and gets shoved outward, harder near the middle
        if (shove && damage > 0f && ShovePerDamage > 0f)
        {
            float force = Mathf.Min(MaxShove, damage * ShovePerDamage);
            foreach (var e in AbilityKit.Enemies(center, radius, null))
            {
                Vector3 d = e.transform.position - center; d.y = 0f;
                float k = 1f - Mathf.Clamp01(d.magnitude / radius);
                if (d.sqrMagnitude < 0.01f) d = Random.insideUnitSphere;
                d.y = 0f;
                AbilityKit.Knockback(e, d.normalized * force * Mathf.Lerp(0.35f, 1f, k));
            }
        }

        int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
        var handled = new HashSet<Object>();

        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null) continue;
            if (source != null && col.transform.IsChildOf(source.transform)) continue;

            float dist = Vector3.Distance(center, SafeClosestPoint(col, center));
            float t = Mathf.Clamp01(dist / radius);
            float delay = Mathf.Lerp(MinChainDelay, MaxChainDelay, t);

            var mine = col.GetComponentInParent<MineExplosionController>();
            if (mine != null && handled.Add(mine))
            {
                mine.Detonate(delay);
                continue;
            }

            var timed = col.GetComponentInParent<GrenadeExplodeAfterDelay>();
            if (timed != null && handled.Add(timed))
            {
                timed.Detonate(delay);
                continue;
            }

            var impact = col.GetComponentInParent<GrenadeExplodeOnImpact>();
            if (impact != null && handled.Add(impact))
            {
                impact.Detonate(delay);
                continue;
            }

            var crystal = col.GetComponentInParent<CrystalHealth>();
            if (crystal != null && damage > 0f && handled.Add(crystal))
                crystal.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * (1f - t))));

            // props take the blast too (barrels ripple outward like mines)
            var prop = col.GetComponentInParent<Destructible>();
            if (prop != null && damage > 0f && handled.Add(prop))
                prop.TakeDamageAfter(delay, damage * Mathf.Lerp(1f, 0.3f, t), null, col.ClosestPointOnBounds(center));
        }

        // big blasts scar the floor for the rest of the match
        if (shove && damage >= Craters.MinBlastDamage)
            Craters.Blast(center, radius * 0.45f);
    }

    /// ClosestPoint doesn't support non-convex mesh colliders (floors, big props): use bounds
    public static Vector3 SafeClosestPoint(Collider col, Vector3 p)
        => col is MeshCollider m && !m.convex ? col.ClosestPointOnBounds(p) : col.ClosestPoint(p);

    /// A bullet fired by `shooter` hit this collider. Returns true if it set something off.
    /// Your own grenades are safe from your bullets (they leave the muzzle right in the
    /// line of fire); any mine can be shot.
    public static bool Shoot(Collider hit, GameObject shooter)
    {
        if (hit == null) return false;

        var mine = hit.GetComponentInParent<MineExplosionController>();
        if (mine != null) { mine.Detonate(0f); return true; }

        var timed = hit.GetComponentInParent<GrenadeExplodeAfterDelay>();
        if (timed != null && (shooter == null || timed.owner != shooter)) { timed.Detonate(0f); return true; }

        var impact = hit.GetComponentInParent<GrenadeExplodeOnImpact>();
        if (impact != null && (shooter == null || impact.owner != shooter)) { impact.Detonate(0f); return true; }

        return false;
    }
}
