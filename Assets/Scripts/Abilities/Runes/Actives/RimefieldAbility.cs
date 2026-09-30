using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Frostwarden — Rune III: rime on the ground that slows and keeps freezing enemies.
public class RimefieldAbility : MonoBehaviour, IActiveAbility
{
    public float distance = 4f;
    public float radius = 4.5f;
    public float duration = 6f;
    [Range(0f, 1f)] public float slow = 0.65f;
    public float freezePerSecond = 2f;

    public void Activate(GameObject caster)
    {
        Vector3 at = AbilityKit.AimPoint(caster, distance);
        var h = GroundHazard.Spawn(caster, at, radius, duration, AbilityKit.Theme(caster));
        h.slowMultiplier = slow;
        h.freezeStacksPerSecond = freezePerSecond;
        EffectPool.Spawn(at, radius, duration, EffectPool.Style.Ice); // cracked ice filling the ring
    }
}
