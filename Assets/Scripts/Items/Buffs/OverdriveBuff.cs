using UnityEngine;

/// Overdrive Orb: for a few seconds every gun you carry fires faster and never needs
/// reloading. Picking up another one restarts the timer.
public class OverdriveBuff : MonoBehaviour
{
    const string Key = "overdrive";

    float until, duration, rate;
    Color color;
    AmmoControl[] guns;
    BuffRing ring;
    float nextSpark;

    public static void Give(GameObject player, float duration, float fireRate, Color color)
    {
        var b = player.GetComponent<OverdriveBuff>();
        if (b == null) b = player.AddComponent<OverdriveBuff>();
        b.Begin(duration, fireRate, color);
    }

    void Begin(float time, float fireRate, Color c)
    {
        duration = time; rate = fireRate; color = c;
        until = Time.time + time;
        guns = GetComponentsInChildren<AmmoControl>(true);
        foreach (var g in guns)
        {
            g.SetFireRateModifier(Key, rate);
            g.SetFreeAmmo(Key, true);
        }
        if (ring == null) ring = new BuffRing(transform, 0.85f, color);
        AbilityKit.Shockwave(transform.position, 2f, color, 0.3f);
    }

    void Update()
    {
        float left = until - Time.time;
        if (left <= 0f) { Destroy(this); return; }
        ring.Draw(transform.position, left / duration, left < 1.5f ? 0.3f + 0.4f * Mathf.PingPong(Time.time * 6f, 1f) : 0.6f);

        // the muzzle runs hot
        if (Time.time >= nextSpark && guns.Length > 0 && guns[0] != null)
        {
            nextSpark = Time.time + 0.12f;
            var fire = guns[0].GetComponent<FireController3D>();
            if (fire != null && fire.firePoint != null)
                PowerFx.Sparks(fire.firePoint.position, color, 2, 1.5f, 0.3f, 0.05f, -0.3f);
        }
    }

    void OnDisable() { if (!gameObject.activeInHierarchy) Destroy(this); }   // eliminated: the player switches off

    void OnDestroy()
    {
        if (guns != null)
            foreach (var g in guns)
            {
                if (g == null) continue;
                g.SetFireRateModifier(Key, 1f);
                g.SetFreeAmmo(Key, false);
            }
        ring?.Destroy();
    }
}
