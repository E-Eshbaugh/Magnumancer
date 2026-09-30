using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// The Hollow — Rune II: trade health percentages with the enemy you're aiming toward.
public class SoulSwapAbility : MonoBehaviour, IActiveAbility, IAbilityOutcome
{
    public float range = 14f;
    public float aimCone = 140f;

    public bool Fizzled { get; private set; }

    public void Activate(GameObject caster)
    {
        Fizzled = false;
        var me = caster.GetComponent<PlayerHealthControl>();
        var target = AbilityKit.NearestEnemy(caster, range, aimCone);
        var them = target != null ? target.GetComponent<PlayerHealthControl>() : null;
        if (me == null || them == null || them.IsDead)
        {
            Fizzled = true; // nobody to swap with: no cooldown spent
            AbilityKit.Zap(AbilityKit.Chest(caster), AbilityKit.AimPoint(caster, 4f) + Vector3.up, AbilityKit.Theme(caster), 0.15f, 0.1f);
            return;
        }

        float mine = me.currentHealth / me.maxHealth;
        float theirs = them.currentHealth / them.maxHealth;
        me.SetHealthFraction(theirs);
        them.SetHealthFraction(mine);

        Color theme = AbilityKit.Theme(caster);
        AbilityKit.Zap(AbilityKit.Chest(caster), AbilityKit.Chest(target), theme, 0.35f, 0.3f);
        AbilityKit.Zap(AbilityKit.Chest(target), AbilityKit.Chest(caster), Color.white, 0.35f, 0.12f);
        AbilityKit.Shockwave(caster.transform.position, 2f, theme);
        AbilityKit.Shockwave(target.transform.position, 2f, theme);
        Rumble.Play(target, 0.6f, 0.8f, 0.3f);
    }
}
