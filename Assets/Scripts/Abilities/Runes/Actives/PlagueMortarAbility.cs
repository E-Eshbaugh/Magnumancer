using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Blightward — Rune II: a lobbed spore shell that bursts into a poison cloud.
public class PlagueMortarAbility : MonoBehaviour, IActiveAbility
{
    public float distance = 8f;
    public float flightTime = 0.8f;
    public float impactRadius = 3f;
    public float impactDamage = 20f;

    public void Activate(GameObject caster) => StartCoroutine(Fire(caster));

    IEnumerator Fire(GameObject caster)
    {
        Color theme = AbilityKit.Theme(caster);
        Vector3 to = AbilityKit.AimPoint(caster, distance);
        var shell = AbilityKit.GlowOrb(theme, 0.4f);
        yield return AbilityKit.Lob(shell.transform, AbilityKit.Chest(caster), to, flightTime, 3.5f);
        Destroy(shell);

        foreach (var e in AbilityKit.Enemies(to, impactRadius, caster))
            DamageEvents.Deal(e, impactDamage, caster);
        Explosions.AffectWorld(to, impactRadius, impactDamage, null);
        AbilityKit.Shockwave(to, impactRadius, theme, 0.35f);
        PoisonCloud.Spawn(caster, to);
    }
}
