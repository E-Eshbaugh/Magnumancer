using UnityEngine;

/// <summary>
/// Verdant Circle — Verdant Resurgence: avoiding damage for 6 seconds triggers a slow heal.
/// </summary>
public class VerdantResurgencePassive : WizardPassive
{
    public float delay = 6f;
    /// Seconds taken off `delay` (Grove Guard affinity)
    [HideInInspector] public float delayReduction;
    public float healPerSecond = 5f;

    float pending; // fractional healing carried between frames

    void Update()
    {
        if (!IsAlive) return;

        if (Time.time - health.LastDamageTime < delay - delayReduction || health.currentHealth >= health.maxHealth)
        {
            pending = 0f;
            return;
        }

        pending += healPerSecond * Time.deltaTime;
        int whole = Mathf.FloorToInt(pending);
        if (whole > 0)
        {
            health.Heal(whole);
            pending -= whole;
        }
    }
}
