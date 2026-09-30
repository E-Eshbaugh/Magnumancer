using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Emberguard — Rune II: refill and superheat your gun for a few seconds.
public class InfernoRoundsAbility : MonoBehaviour, IActiveAbility
{
    public float duration = 5f;
    public float damageMultiplier = 1.35f;

    public void Activate(GameObject caster)
    {
        foreach (var ammo in caster.GetComponentsInChildren<AmmoControl>()) ammo.RefillMagazine();
        var buff = caster.GetComponent<InfernoBuff>();
        if (buff == null) buff = caster.AddComponent<InfernoBuff>();
        buff.Begin(duration, damageMultiplier, AbilityKit.Theme(caster));
        AbilityKit.Shockwave(caster.transform.position, 2.5f, AbilityKit.Theme(caster), 0.35f);
    }
}
