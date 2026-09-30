using System.Collections.Generic;
using UnityEngine;

/// Frostwarden — Rune II: hits apply freeze; frozen targets take extra damage from you.
public class ColdPrecisionPassive : WizardPassive
{
    public float bonusPerStack = 0.08f;       // 5 stacks = +40%
    public float stackCooldownPerTarget = 0.2f;

    readonly Dictionary<GameObject, float> lastStack = new();
    static bool dealingBonus;

    void OnEnable() => DamageEvents.Damaged += OnDamaged;
    void OnDisable() => DamageEvents.Damaged -= OnDamaged;

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (dealingBonus || attacker != gameObject || amount <= 0f || !IsEnemy(victim)) return;
        var fx = StatusEffects.Of(victim);

        // bonus uses the stacks already on them, then this hit adds one
        int stacks = fx.FreezeStacks;
        if (stacks > 0)
        {
            dealingBonus = true;
            DamageEvents.Deal(victim, amount * bonusPerStack * stacks, gameObject);
            dealingBonus = false;
        }

        if (lastStack.TryGetValue(victim, out float t) && Time.time - t < stackCooldownPerTarget) return;
        lastStack[victim] = Time.time;
        fx.AddFreeze();
    }
}
