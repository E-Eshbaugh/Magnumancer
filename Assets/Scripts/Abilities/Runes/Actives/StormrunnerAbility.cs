using UnityEngine;
using Magnumancer.Abilities;

/// Voltborn — Rune III: Stormrunner. For a few seconds your dash barely has a cooldown and
/// becomes a blink that leaves a crackling lightning fence where you were. Anyone who
/// touches a fence is shocked and briefly stunned. Every blink still arms Lightning Reflex.
public class StormrunnerAbility : MonoBehaviour, IActiveAbility
{
    public float duration = 4.5f;

    public void Activate(GameObject caster)
    {
        var s = caster.GetComponent<Stormrunner>();
        if (s == null) s = caster.AddComponent<Stormrunner>();
        s.Begin(duration, AbilityKit.Theme(caster));
    }
}
