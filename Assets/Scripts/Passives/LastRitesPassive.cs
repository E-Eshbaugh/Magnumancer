using UnityEngine;

/// <summary>
/// The Hollow — Last Rites: when on the last life stock, every 20 seconds gain
/// 3 seconds of invincibility and double damage (triggers as soon as you reach it).
/// </summary>
public class LastRitesPassive : WizardPassive, IOutgoingDamageModifier
{
    public float interval = 20f;
    public float duration = 3f;
    public float damageMultiplier = 2f;

    public Color glowColor = new Color(0.65f, 0.25f, 1f);

    float nextProc = -1f;
    float empoweredUntil;

    public bool Empowered => Time.time < empoweredUntil;

    void Update()
    {
        // Pulsing violet glow while empowered
        Glow.Set(glowColor, Empowered ? 1f : 0f, 6f);

        if (!IsAlive) return;

        if (health.stockCount > 0)
        {
            nextProc = -1f;
            return;
        }

        if (nextProc < 0f) nextProc = Time.time; // just reached the last life

        if (Time.time >= nextProc)
        {
            health.GrantInvulnerability(duration);
            empoweredUntil = Time.time + duration;
            nextProc = Time.time + interval;
            SpawnEffect(transform.position + Vector3.up, duration);
        }
    }

    public float ModifyOutgoing(float amount) => Empowered ? amount * damageMultiplier : amount;
}
