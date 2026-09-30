using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The center-out gun ability bar. Whichever gun ability belongs to the equipped gun
/// reports its cooldown each frame via ReportCooldown; guns whose ability has no
/// cooldown (or none at all) show a full bar.
/// </summary>
public class WeaponAbilityControl : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Left half of the center-out ability bar")]
    public Image abilityBarL;
    [Tooltip("Right half of the center-out ability bar")]
    public Image abilityBarR;

    int reportedFrame = -1;
    float reportedFill = 1f;

    void Start() => SetFill(1f);

    /// readyTime: Time.time the ability is ready again; cooldown: its full length.
    public void ReportCooldown(float readyTime, float cooldown)
    {
        float remaining = readyTime - Time.time;
        ReportFill(cooldown <= 0f || remaining <= 0f ? 1f : 1f - remaining / cooldown);
    }

    /// 0 = just used, 1 = ready (also used for "active, draining" states)
    public void ReportFill(float fill)
    {
        reportedFrame = Time.frameCount;
        reportedFill = Mathf.Clamp01(fill);
    }

    /// Kept for older callers: shows the bar emptying (the ability's own
    /// ReportCooldown drives the refill from here on).
    public void TriggerAbilityFill() => ReportFill(0f);

    void LateUpdate()
    {
        SetFill(reportedFrame == Time.frameCount ? reportedFill : 1f);
    }

    void SetFill(float frac)
    {
        if (abilityBarL != null) abilityBarL.fillAmount = frac;
        if (abilityBarR != null) abilityBarR.fillAmount = frac;
    }

    /// The bar for the player that owns this gun (abilities wired only on some components).
    public static WeaponAbilityControl FindFor(Component gunComponent)
    {
        if (gunComponent == null) return null;
        var oc = gunComponent.GetComponent<OverClock>();
        if (oc != null && oc.weaponAbility != null) return oc.weaponAbility;
        var ak = gunComponent.GetComponent<AkimboController>();
        if (ak != null && ak.weaponAbility != null) return ak.weaponAbility;
        return null;
    }
}
