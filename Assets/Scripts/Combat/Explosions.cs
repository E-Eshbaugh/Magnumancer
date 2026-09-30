using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How blasts interact with the world beyond hurting players: they set off Viper mines
/// (which chain into each other), cook off nearby grenades, and damage destructible
/// items like healing crystals. Every explosion calls AffectWorld once.
/// Bullets use Shoot() so mines and grenades can be popped with gunfire too.
/// </summary>
public static class Explosions
{
    // Chain reactions ripple outward instead of all popping on the same frame
    const float MinChainDelay = 0.06f;
    const float MaxChainDelay = 0.22f;

    static readonly Collider[] buffer = new Collider[128];

    /// center/radius: the blast. damage: its max damage (falls off with distance).
    /// source: the exploding object itself, so it doesn't re-trigger itself.
    public static void AffectWorld(Vector3 center, float radius, float damage, GameObject source)
    {
        if (radius <= 0f) return;

        // everyone nearby feels it (victims inside the radius the most)
        Rumble.Blast(center, radius * 1.5f);

        int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
        var handled = new HashSet<Object>();

        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null) continue;
            if (source != null && col.transform.IsChildOf(source.transform)) continue;

            float dist = Vector3.Distance(center, col.ClosestPoint(center));
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
        }
    }

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
