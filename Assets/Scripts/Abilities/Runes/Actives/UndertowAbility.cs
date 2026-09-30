using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Tidebound — Rune II: a whirlpool that drags enemies to its center.
public class UndertowAbility : MonoBehaviour, IActiveAbility
{
    public float distance = 5f;
    public float radius = 4f;
    public float duration = 3f;
    public float pull = 4.5f;
    public float damagePerSecond = 6f;

    public void Activate(GameObject caster)
    {
        Vector3 at = AbilityKit.AimPoint(caster, distance);
        var h = GroundHazard.Spawn(caster, at, radius, duration, AbilityKit.Theme(caster));
        h.pullStrength = pull;
        h.damagePerSecond = damagePerSecond;
        h.slowMultiplier = 0.8f;
        WhirlpoolFx.Spawn(at, radius, duration, AbilityKit.Theme(caster)); // churning water inside the ring
    }
}
