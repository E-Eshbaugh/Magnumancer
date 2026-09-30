using UnityEngine;

/// Blightward — Rune II: heal for a share of all damage you deal.
public class LeechSporesPassive : WizardPassive
{
    [Range(0f, 1f)] public float lifesteal = 0.2f;
    float pending;

    void OnEnable() => DamageEvents.Damaged += OnDamaged;
    void OnDisable() => DamageEvents.Damaged -= OnDamaged;

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (attacker != gameObject || amount <= 0f || !IsEnemy(victim) || !IsAlive) return;
        pending += amount * lifesteal;
        int whole = Mathf.FloorToInt(pending);
        if (whole > 0) { health.Heal(whole); pending -= whole; }
    }
}
