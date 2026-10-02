using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Voltborn — Rune II: lightning that jumps between enemies.
public class ChainSurgeAbility : MonoBehaviour, IActiveAbility
{
    public float range = 12f;
    public float aimCone = 120f;
    public float damage = 30f;
    public int extraJumps = 2;
    public float jumpRange = 6f;
    [Range(0f, 1f)] public float falloff = 0.7f;

    public void Activate(GameObject caster)
    {
        Color theme = AbilityKit.Theme(caster);
        Vector3 from = AbilityKit.Chest(caster);
        var target = AbilityKit.NearestEnemy(caster, range, aimCone);
        if (target == null)
        {
            AbilityKit.Zap(from, AbilityKit.AimPoint(caster, range * 0.6f) + Vector3.up, theme);
            return;
        }

        var struck = new System.Collections.Generic.HashSet<GameObject>();
        float dmg = damage;
        int jumps = extraJumps + ForgedRunes.ConsumeRelayJumps(caster);   // Conductor relay
        for (int i = 0; i <= jumps && target != null; i++)
        {
            struck.Add(target);
            Vector3 to = AbilityKit.Chest(target);
            AbilityKit.Zap(from, to, theme, 0.25f, 0.25f);
            DamageEvents.Deal(target, dmg, caster);
            StatusEffects.Of(target).Stun(0.4f, 0.4f);
            ElementReactions.AbilityHit(target, caster, dmg);   // Conduct on Soaked, else Charged

            from = to;
            dmg *= falloff;
            GameObject next = null;
            float best = float.MaxValue;
            foreach (var e in AbilityKit.Enemies(target.transform.position, jumpRange, caster))
            {
                if (struck.Contains(e)) continue;
                float d = (e.transform.position - target.transform.position).sqrMagnitude;
                if (d < best) { best = d; next = e; }
            }
            target = next;
        }
    }
}
