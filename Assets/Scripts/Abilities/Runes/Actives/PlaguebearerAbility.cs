using UnityEngine;
using Magnumancer.Abilities;

/// Blightward — Rune II: Plaguebearer. Become a walking plague: a toxic cloud surrounds you,
/// and enemies near you build up plague stacks that hurt more the longer they stay close.
public class PlaguebearerAbility : MonoBehaviour, IActiveAbility
{
    public float duration = 5f;

    public void Activate(GameObject caster)
    {
        var p = caster.GetComponent<Plaguebearer>();
        if (p == null) p = caster.AddComponent<Plaguebearer>();
        p.Begin(duration, AbilityKit.Theme(caster));
    }
}
