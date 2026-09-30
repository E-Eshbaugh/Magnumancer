using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Voltborn — Rune III: an electrified zone that slows enemies and speeds you up.
public class StaticFieldAbility : MonoBehaviour, IActiveAbility
{
    public float radius = 4.5f;
    public float duration = 5f;
    public float damagePerSecond = 6f;
    [Range(0f, 1f)] public float slow = 0.6f;
    public float ownerSpeed = 1.3f;

    public void Activate(GameObject caster)
    {
        Vector3 at = AbilityKit.Ground(caster.transform.position + Vector3.up);
        var h = GroundHazard.Spawn(caster, at, radius, duration, AbilityKit.Theme(caster));
        h.damagePerSecond = damagePerSecond;
        h.slowMultiplier = slow;
        h.ownerSpeedMultiplier = ownerSpeed;
        StaticSparks.Spawn(at, radius, duration, AbilityKit.Theme(caster)); // crackling ground inside the ring
    }
}
