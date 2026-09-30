using UnityEngine;

/// Verdant Circle — Rune II: close-range attackers take a share of the damage back.
public class ThornhidePassive : WizardPassive
{
    [Range(0f, 1f)] public float reflect = 0.25f;
    public float range = 5f;

    static bool reflecting; // two Thornhides can't ping-pong damage forever

    void OnEnable() => DamageEvents.Damaged += OnDamaged;
    void OnDisable() => DamageEvents.Damaged -= OnDamaged;

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (reflecting || victim != gameObject || amount <= 0f || !IsEnemy(attacker)) return;
        if ((attacker.transform.position - transform.position).sqrMagnitude > range * range) return;

        reflecting = true;
        DamageEvents.Deal(attacker, amount * reflect, gameObject);
        reflecting = false;
    }
}
