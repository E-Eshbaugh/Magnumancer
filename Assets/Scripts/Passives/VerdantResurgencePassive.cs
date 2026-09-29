using UnityEngine;

/// <summary>
/// Verdant Circle — Verdant Resurgence: avoiding damage for 6 seconds triggers a slow heal.
/// </summary>
public class VerdantResurgencePassive : WizardPassive
{
    public float delay = 6f;
    public float healPerSecond = 4f;

    float pending; // fractional healing carried between frames

    void Update()
    {
        if (!IsAlive) return;

        if (Time.time - health.LastDamageTime < delay || health.currentHealth >= health.maxHealth)
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
