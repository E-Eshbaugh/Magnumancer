using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WeaponAbilityControl : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Left half of the center-out ability bar")]
    public Image abilityBarL;
    [Tooltip("Right half of the center-out ability bar")]
    public Image abilityBarR;

    [Header("Fill Settings")]
    [Tooltip("How long (in seconds) it takes to refill from 0→1")]
    public float fillDuration = 2f;

    private Coroutine _fillRoutine;

    void Start()
    {
        // At start, both halves are full
        if (abilityBarL != null) abilityBarL.fillAmount = 1f;
        if (abilityBarR != null) abilityBarR.fillAmount = 1f;
    }

    /// <summary>
    /// Call this when the ability is used (e.g. LT pressed).
    /// Bars will disappear, then refill from center out.
    /// </summary>
    public void TriggerAbilityFill()
    {
        // stop any in-progress fill
        if (_fillRoutine != null)
            StopCoroutine(_fillRoutine);

        // instantly empty both halves
        if (abilityBarL != null) abilityBarL.fillAmount = 0f;
        if (abilityBarR != null) abilityBarR.fillAmount = 0f;

        // kick off the refill coroutine
        _fillRoutine = StartCoroutine(FillRoutine());
    }

    private IEnumerator FillRoutine()
    {
        float elapsed = 0f;
        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            float frac = Mathf.Clamp01(elapsed / fillDuration);

            if (abilityBarL != null) abilityBarL.fillAmount = frac;
            if (abilityBarR != null) abilityBarR.fillAmount = frac;

            yield return null;
        }

        // ensure fully filled at the end
        if (abilityBarL != null) abilityBarL.fillAmount = 1f;
        if (abilityBarR != null) abilityBarR.fillAmount = 1f;

        _fillRoutine = null;
    }
}
