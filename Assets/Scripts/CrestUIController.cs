using UnityEngine;
using UnityEngine.UI;

public class CrestUIController : MonoBehaviour
{
    [Tooltip("Dark overlay image used as health mask")]
    public Image healthMask;

    [Tooltip("Glow sprite or Image behind crest for ability charge")]
    public Image abilityGlow;

    /// <summary>
    /// Call this with a 0→1 value as the player’s current health fraction.
    /// 1.0 = full health (mask empty), 0.0 = zero health (mask full).
    /// </summary>
    public void SetHealthFraction(float frac)
    {
        Debug.Log($"[CrestUI] SetHealthFraction({frac})");
        healthMask.fillAmount = 1f - Mathf.Clamp01(frac);
    }


    /// <summary>
    /// Call this with a 0→1 value as the ability charge fraction.
    /// Drives glow intensity via alpha.
    /// </summary>
    public void SetAbilityFraction(float frac)
    {
        var c = abilityGlow.color;
        c.a = Mathf.Clamp01(frac);
        abilityGlow.color = c;
    }
}
