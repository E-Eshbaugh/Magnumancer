using UnityEngine;

/// <summary>
/// Granite Vow — Stonebind: reduced knockback and recoil. Standing still builds armor,
/// increasing damage resistance.
/// </summary>
public class StonebindPassive : WizardPassive, IIncomingDamageModifier
{
    public float knockbackMultiplier = 0.5f;
    public float recoilMultiplier = 0.5f;

    [Header("Armor")]
    [Tooltip("Seconds standing still before armor starts building")]
    public float stillDelay = 0.5f;
    public float armorPerSecond = 0.15f;
    public float maxArmor = 0.4f;          // 40% damage reduction
    [Tooltip("Scales the move-speed penalty from weapon weight (engineer-soldiers built for heavy guns)")]
    public float weightPenaltyMultiplier = 0.5f;

    public Color glowColor = new Color(1f, 0.65f, 0.25f);

    float stillTime;

    /// Current damage reduction (0..maxArmor)
    public float Armor => Mathf.Clamp((stillTime - stillDelay) * armorPerSecond, 0f, maxArmor);

    public override void Init(WizardData data)
    {
        base.Init(data);
        if (movement != null)
        {
            movement.knockbackMultiplier = knockbackMultiplier;
            movement.weightPenaltyScale *= weightPenaltyMultiplier;
            movement.SetCarriedWeight(movement.HeldWeight); // re-apply with the lighter penalty
        }
        foreach (var gun in GetComponentsInChildren<FireController3D>(true))
            gun.recoilMultiplier = recoilMultiplier;
    }

    void Update()
    {
        if (movement != null && movement.StickMagnitude < 0.1f)
            stillTime += Time.deltaTime;
        else
            stillTime = 0f;

        // Warm glow that builds with armor
        Glow.Set(glowColor, Armor / maxArmor);
    }

    public float ModifyIncoming(float amount, GameObject attacker) => amount * (1f - Armor);
}
