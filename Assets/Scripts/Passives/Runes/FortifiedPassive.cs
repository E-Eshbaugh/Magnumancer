using UnityEngine;

/// Granite Vow — Rune II: finishing a reload grants a temporary stone shield.
public class FortifiedPassive : WizardPassive, IIncomingDamageModifier
{
    public float shieldAmount = 20f;
    public float shieldDuration = 5f;
    public Color glowColor = new Color(0.85f, 0.65f, 0.4f);

    float shield, until;
    AmmoControl[] guns;

    public override void Init(WizardData data)
    {
        base.Init(data);
        guns = GetComponentsInChildren<AmmoControl>(true);
        foreach (var g in guns) g.OnReloaded += Grant;
    }

    void OnDestroy()
    {
        if (guns != null) foreach (var g in guns) if (g != null) g.OnReloaded -= Grant;
    }

    void Grant()
    {
        if (!IsAlive) return;
        shield = shieldAmount;
        until = Time.time + shieldDuration;
    }

    float Current => Time.time < until ? shield : 0f;

    void Update() => Glow.Set(glowColor, Current / shieldAmount);

    public float ModifyIncoming(float amount, GameObject attacker)
    {
        float s = Current;
        if (s <= 0f) return amount;
        float absorbed = Mathf.Min(s, amount);
        shield -= absorbed;
        return amount - absorbed;
    }
}
