using UnityEngine;
using UnityEngine.UI;

public class CrestUIController : MonoBehaviour
{
    [Tooltip("Dark overlay image used as health mask")]
    public Image healthMask;

    [Tooltip("Glow sprite or Image behind crest for ability charge")]
    public Image abilityGlow;

    public float healthAmount;

    /// <summary>
    /// Call this with a 0→1 value as the player’s current health fraction.
    /// </summary>
    public void SetHealthFraction(float frac)
    {
        healthMask.fillAmount = 1-Mathf.Clamp01(frac);
    }

    /// <summary>
    /// Call this with a 0→1 value as the ability charge fraction.
    /// Drives glow intensity via alpha.
    /// </summary>
    public void SetAbilityFraction(float frac)
    {
        // you can tweak the curve (e.g. ease-in) in code or via an animation curve
        abilityGlow.color = new Color(1, 1, 1, Mathf.Clamp01(frac));
    }

    void Update()
    {
        SetHealthFraction(healthAmount);
    }
}
