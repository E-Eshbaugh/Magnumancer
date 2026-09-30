using UnityEngine;
using Magnumancer.Abilities;

/// Verdant Circle — Rune III: Siphoning Vine. A vine lashes the enemy you're aiming
/// toward and drains their life into you while you stay close. It snaps if they get away.
/// Fizzles (no cooldown spent) if nobody's in reach.
public class SiphoningVineAbility : MonoBehaviour, IActiveAbility, IAbilityOutcome
{
    public float range = 10f;
    public float aimCone = 150f;
    public float duration = 4f;
    public float drainPerSecond = 8f;
    [Tooltip("The vine snaps if you get farther apart than this")]
    public float breakRange = 13f;

    public bool Fizzled { get; private set; }

    public void Activate(GameObject caster)
    {
        Fizzled = false;
        var target = AbilityKit.NearestEnemy(caster, range, aimCone);
        if (target == null)
        {
            Fizzled = true;
            PowerFx.Puffs(AbilityKit.Chest(caster) + AbilityKit.AimDir(caster), AbilityKit.Theme(caster), 6, 1.5f, 0.3f, 0.5f, additive: true);
            return;
        }
        new GameObject("SiphoningVine").AddComponent<SiphonTether>()
            .Begin(caster, target, duration, drainPerSecond, breakRange, AbilityKit.Theme(caster));
    }
}
