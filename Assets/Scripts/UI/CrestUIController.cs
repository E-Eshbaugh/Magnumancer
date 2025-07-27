using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class CrestUIController : MonoBehaviour
{
    [Tooltip("Dark overlay image used as health mask")]
    public Image healthMask;

    void Awake()
    {
        // If it’s already wired in the Inspector, bail out
        if (healthMask != null) return;

        // Grab all Image components under this object
        var imgs = GetComponentsInChildren<Image>(includeInactive: true);

        // 1) Try to find any whose name contains "mask"
        healthMask = imgs.FirstOrDefault(i =>
            i.gameObject.name.ToLower().Contains("Mask"));

        // 2) Fallback to the very first Image you find
        if (healthMask == null && imgs.Length > 0)
            healthMask = imgs[0];

        // 3) Report back
        if (healthMask == null)
            Debug.LogError($"[{name}] CrestUIController: could not locate a healthMask Image!");
        else
            Debug.Log($"[{name}] CrestUIController: auto-assigned healthMask to '{healthMask.gameObject.name}'");
    }

    /// <summary>
    /// Call this with a 0→1 fraction of health.
    /// </summary>
    public void SetHealthFraction(float frac)
    {
        if (healthMask == null)
        {
            return;
        }
        healthMask.fillAmount = 1f - Mathf.Clamp01(frac);
    }
}
