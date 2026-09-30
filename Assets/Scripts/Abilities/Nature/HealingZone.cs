using System.Collections.Generic;
using UnityEngine;

public class HealingZone : MonoBehaviour
{
    [Header("Healing Settings")]
    public float maxHealPerSecond = 10f;
    public HealingBeamController beamController;

    private Dictionary<Transform, float> healAccumulator = new();

    // Overgrowth Surge: water feeds the totem, healing harder for a while
    float surgeMultiplier = 1f, surgeUntil;
    public static readonly List<HealingZone> Active = new();

    public void Surge(float multiplier, float duration)
    {
        surgeMultiplier = multiplier;
        surgeUntil = Time.time + duration;
        Color nature = Elements.ColorOf(Element.Nature);
        AbilityKit.Shockwave(AbilityKit.Ground(transform.position + Vector3.up), 4f, nature, 0.6f);
        PowerFx.Sparks(transform.position + Vector3.up, nature, 30, 4f, 0.8f, 0.08f, -0.3f, Vector3.up, 120f);
    }

    void OnEnable()
    {
        Active.Add(this);
        if (beamController != null)
        {
            Debug.Log("[HealingZone] Subscribed to HealingBeamController");
            beamController.OnHealablePlayersUpdated += HealPlayers;
        }
        else
        {
            Debug.LogError("[HealingZone] beamController reference is missing!");
        }
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (beamController != null)
        {
            beamController.OnHealablePlayersUpdated -= HealPlayers;
        }
    }

    void HealPlayers(List<Transform> players)
    {
        int numTargets = players.Count;
        if (numTargets == 0) return;

        float rate = maxHealPerSecond * (Time.time < surgeUntil ? surgeMultiplier : 1f);
        float healRatePerPlayer = rate / numTargets;
        float healThisFrame = healRatePerPlayer * Time.deltaTime;

        foreach (Transform player in players)
        {
            var health = player.GetComponentInParent<PlayerHealthControl>();
            if (health == null || health.currentHealth >= health.maxHealth || health.currentHealth <= 0)
                continue;

            // Initialize accumulator for new players
            if (!healAccumulator.ContainsKey(player))
                healAccumulator[player] = 0f;

            // Accumulate healing over time
            healAccumulator[player] += healThisFrame;

            int healNow = Mathf.FloorToInt(healAccumulator[player]);
            if (healNow > 0)
            {
                health.Heal(healNow);
                healAccumulator[player] -= healNow;

                Debug.Log($"[HealingZone] Healed {player.name} for {healNow} HP (Accumulated: {healAccumulator[player]:F2})");
            }
        }

        // Optional cleanup: remove players who are no longer in the list
        HashSet<Transform> currentSet = new(players);
        List<Transform> toRemove = new();
        foreach (var tracked in healAccumulator.Keys)
        {
            if (!currentSet.Contains(tracked))
                toRemove.Add(tracked);
        }
        foreach (var p in toRemove)
            healAccumulator.Remove(p);
    }
}
