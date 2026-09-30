using UnityEngine;

/// Voltborn — Rune II: sustained fire builds charge, up to +30% fire rate.
public class OverchargePassive : WizardPassive
{
    public float maxBonus = 0.3f;
    public float rampTime = 2f;          // seconds of firing to reach full charge
    public float holdWindow = 0.35f;     // gap between shots that still counts as "firing"
    public float decayTime = 0.6f;       // full charge drains in this long
    public Color glowColor = new Color(0.6f, 0.85f, 1f);

    AmmoControl[] guns;
    float lastShot = -99f, charge;

    public override void Init(WizardData data)
    {
        base.Init(data);
        guns = GetComponentsInChildren<AmmoControl>(true);
        foreach (var g in guns) g.OnFired += OnFired;
    }

    void OnDestroy()
    {
        if (guns != null) foreach (var g in guns) if (g != null) g.OnFired -= OnFired;
    }

    void OnFired(int _) => lastShot = Time.time;

    void Update()
    {
        bool firing = Time.time - lastShot <= holdWindow;
        charge = Mathf.Clamp01(charge + Time.deltaTime / (firing ? rampTime : -decayTime));
        if (guns != null) foreach (var g in guns) g.SetFireRateModifier("overcharge", 1f + maxBonus * charge);
        Glow.Set(glowColor, charge * 0.8f, charge >= 1f ? 20f : 0f);
    }
}
