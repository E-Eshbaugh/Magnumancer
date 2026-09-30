using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Verdant Circle — Rune III: a thicket around you that slows and damages enemies.
public class OvergrowthAbility : MonoBehaviour, IActiveAbility
{
    public float radius = 3.5f;
    public float duration = 5f;
    public float damagePerSecond = 8f;
    [Range(0f, 1f)] public float slow = 0.55f;

    public void Activate(GameObject caster)
    {
        Vector3 at = AbilityKit.Ground(caster.transform.position + Vector3.up);
        var h = GroundHazard.Spawn(caster, at, radius, duration, AbilityKit.Theme(caster));
        h.damagePerSecond = damagePerSecond;
        h.slowMultiplier = slow;
        AbilityKit.Shockwave(at, radius, AbilityKit.Theme(caster), 0.4f);
        OvergrowthFx.Spawn(caster, at, radius, duration, AbilityKit.Theme(caster)); // vines + roots inside the ring
    }
}
