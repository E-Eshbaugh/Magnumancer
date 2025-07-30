using System.Collections.Generic;
using UnityEngine;

public class HealingZone : MonoBehaviour
{
    [Header("Healing Settings")]
    public float maxHealPerSecond = 10f;
    public HealingBeamController beamController;

    private Dictionary<Transform, float> healAccumulator = new();

    void OnEnable()
    {
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
        if (beamController != null)
        {
            beamController.OnHealablePlayersUpdated -= HealPlayers;
        }
    }

    void HealPlayers(List<Transform> players)
    {
        int numTargets = players.Count;
        if (numTargets == 0) return;

        float healRatePerPlayer = maxHealPerSecond / numTargets;
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
