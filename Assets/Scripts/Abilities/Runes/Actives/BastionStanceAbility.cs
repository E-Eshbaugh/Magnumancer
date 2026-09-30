using UnityEngine;
using Magnumancer.Abilities;

/// Granite Vow — Rune III: Bastion Stance. Plant yourself: an arc of stone slabs erupts in
/// front of you and follows your aim, blocking bullets from the front. You can't be moved
/// or knocked back while planted; dashing out ends it early.
public class BastionStanceAbility : MonoBehaviour, IActiveAbility
{
    public float duration = 5f;

    public void Activate(GameObject caster)
    {
        var stance = caster.GetComponent<BastionStance>();
        if (stance == null) stance = caster.AddComponent<BastionStance>();
        stance.Begin(duration, AbilityKit.Theme(caster));
    }
}
