using UnityEngine;

/// Emberguard — Rune II: hits cut your ability cooldown; taking a life refreshes it.
public class KindlingPassive : WizardPassive
{
    public float secondsPerHit = 0.15f;
    [Tooltip("A shotgun blast's pellets count once")]
    public float hitInterval = 0.1f;

    WizardAbilityController ability;
    float lastHit;

    public override void Init(WizardData data)
    {
        base.Init(data);
        ability = GetComponentInChildren<WizardAbilityController>(true);
    }

    void OnEnable()
    {
        DamageEvents.Damaged += OnDamaged;
        DamageEvents.Killed += OnKilled;
    }

    void OnDisable()
    {
        DamageEvents.Damaged -= OnDamaged;
        DamageEvents.Killed -= OnKilled;
    }

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (attacker != gameObject || !IsEnemy(victim) || Time.time - lastHit < hitInterval) return;
        lastHit = Time.time;
        ability?.Cooldown?.Reduce(secondsPerHit);
    }

    void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (attacker == gameObject && IsEnemy(victim)) ability?.Cooldown?.Refresh();
    }
}
