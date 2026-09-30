using UnityEngine;

/// The Hollow — Rune II: each life you take adds damage until you lose one of yours.
public class SoulHarvestPassive : WizardPassive, IOutgoingDamageModifier
{
    public float bonusPerSoul = 0.06f;
    public int maxSouls = 5;
    public Color glowColor = new Color(0.65f, 0.25f, 1f);

    int souls;

    void OnEnable() => DamageEvents.Killed += OnKilled;
    void OnDisable() => DamageEvents.Killed -= OnKilled;

    void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (victim == gameObject) { souls = 0; return; }
        if (attacker == gameObject && IsEnemy(victim)) souls = Mathf.Min(maxSouls, souls + 1);
    }

    public float ModifyOutgoing(float amount) => amount * (1f + bonusPerSoul * souls);

    void Update() => Glow.Set(glowColor, souls / (float)maxSouls * 0.8f);
}
