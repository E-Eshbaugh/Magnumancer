using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blink Charm: the next few dashes come straight back (no cooldown). Shown as small
/// glowing pips circling your waist, one per charge. Charges stay until used.
/// </summary>
public class BlinkCharm : MonoBehaviour
{
    const string Key = "blink";
    public static int MaxCharges = 6;

    int charges;
    Color color;
    PlayerMovement3D movement;
    readonly List<GameObject> pips = new();

    public static void Give(GameObject player, int charges, Color color)
    {
        var b = player.GetComponent<BlinkCharm>();
        if (b == null) b = player.AddComponent<BlinkCharm>();
        b.Add(charges, color);
    }

    void Add(int n, Color c)
    {
        color = c;
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement3D>();
            if (movement != null) movement.OnDash += Dashed;
        }
        charges = Mathf.Min(MaxCharges, charges + n);
        if (movement != null) movement.SetDashCooldownModifier(Key, 0.02f);
        RebuildPips();
    }

    void Dashed()
    {
        PowerFx.Sparks(AbilityKit.Chest(gameObject), color, 10, 4f, 0.3f, 0.06f, 0f);
        if (--charges <= 0) { Destroy(this); return; }
        RebuildPips();
    }

    void RebuildPips()
    {
        foreach (var p in pips) if (p != null) Destroy(p);
        pips.Clear();
        for (int i = 0; i < charges; i++)
        {
            var pip = AbilityKit.GlowOrb(color, 0.14f);
            pip.name = "BlinkPip";
            pip.transform.SetParent(transform, false);
            pips.Add(pip);
        }
    }

    void Update()
    {
        for (int i = 0; i < pips.Count; i++)
        {
            float a = Time.time * 3f + i / (float)Mathf.Max(1, pips.Count) * Mathf.PI * 2f;
            pips[i].transform.position = transform.position + new Vector3(Mathf.Cos(a) * 0.7f, 0.9f, Mathf.Sin(a) * 0.7f);
        }
    }

    void OnDisable() { if (!gameObject.activeInHierarchy) Destroy(this); }   // eliminated: the player switches off

    void OnDestroy()
    {
        if (movement != null)
        {
            movement.OnDash -= Dashed;
            movement.SetDashCooldownModifier(Key, 1f);
        }
        foreach (var p in pips) if (p != null) Destroy(p);
    }
}
