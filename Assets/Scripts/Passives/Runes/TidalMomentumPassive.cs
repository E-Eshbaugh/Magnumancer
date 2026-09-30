using UnityEngine;

/// Tidebound — Rune II: every dash reloads part of your magazine.
public class TidalMomentumPassive : WizardPassive
{
    [Range(0f, 1f)] public float reloadFraction = 0.25f;

    public override void Init(WizardData data)
    {
        base.Init(data);
        if (movement != null) movement.OnDash += Reload;
    }

    void OnDestroy()
    {
        if (movement != null) movement.OnDash -= Reload;
    }

    void Reload()
    {
        if (!IsAlive) return;
        foreach (var ammo in GetComponentsInChildren<AmmoControl>()) ammo.AddAmmoFraction(reloadFraction);
    }
}
