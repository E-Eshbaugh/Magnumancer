using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Verdant Circle — Rune II: a bramble trap that roots the first enemy to step in.
public class ThornsnareAbility : MonoBehaviour, IActiveAbility
{
    public float distance = 4f;
    public float radius = 1.3f;
    public float rootDuration = 1.5f;
    public float damage = 15f;
    public float lifetime = 25f;

    public void Activate(GameObject caster)
    {
        var h = GroundHazard.Spawn(caster, AbilityKit.AimPoint(caster, distance), radius, lifetime, AbilityKit.Theme(caster));
        h.rootDuration = rootDuration;
        h.burstDamage = damage;
        h.armTime = 0.6f;
    }
}
