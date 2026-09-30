using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Frostwarden — Fractalshot Shield: hits apply freeze counters that slow enemies.
/// Stacking 5 on an enemy freezes them briefly and grants a weak ice shield,
/// which grows stronger the more enemies you freeze.
/// </summary>
public class FractalshotShieldPassive : WizardPassive, IIncomingDamageModifier
{
    public float shieldPerFreeze = 15f;
    public float maxShield = 60f;
    [Tooltip("A single shotgun blast's pellets only add one counter")]
    public float stackCooldownPerTarget = 0.2f;
    [Tooltip("Each this-much damage in one hit adds another freeze counter")]
    public float damagePerExtraStack = 20f;
    [Header("Full freeze")]
    public float freezeStunMultiplier = 0.4f;
    public float freezeStunDuration = 1f;

    public Color glowColor = new Color(0.55f, 0.85f, 1f);

    public float Shield { get; private set; }

    // Icy glow that scales with the shield
    void Update() => Glow.Set(glowColor, Shield / maxShield);

    readonly Dictionary<GameObject, float> lastStackTime = new();

    void OnEnable() => DamageEvents.Damaged += OnDamaged;
    void OnDisable() => DamageEvents.Damaged -= OnDamaged;

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (attacker != gameObject || !IsEnemy(victim)) return;

        if (lastStackTime.TryGetValue(victim, out float last) && Time.time - last < stackCooldownPerTarget)
            return;
        lastStackTime[victim] = Time.time;

        // Heavy hits freeze harder: +1 counter per damagePerExtraStack (a 45 dmg sniper shot = 3)
        int stacks = 1 + Mathf.FloorToInt(amount / Mathf.Max(1f, damagePerExtraStack));
        var fx = StatusEffects.Of(victim);
        int count = 0;
        for (int i = 0; i < stacks; i++) count = fx.AddFreeze();
        if (count >= 5)
        {
            // Fully frozen: brief hard slow, counters reset, and we gain shield
            fx.ClearFreeze();
            fx.Stun(freezeStunMultiplier, freezeStunDuration);
            fx.MarkFrozen(freezeStunDuration);   // Shatter-able while it lasts
            Shield = Mathf.Min(maxShield, Shield + shieldPerFreeze);
            SpawnEffect(transform.position + Vector3.up, 2f);
        }
    }

    public float ModifyIncoming(float amount, GameObject attacker)
    {
        if (Shield <= 0f) return amount;
        float absorbed = Mathf.Min(Shield, amount);
        Shield -= absorbed;
        return amount - absorbed;
    }
}
