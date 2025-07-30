using System;
using System.Collections.Generic;
using UnityEngine;

public class HealingBeamController : MonoBehaviour
{
    [Header("Healing Settings")]
    public float healRange = 5f;
    public float yOffset = 0f;
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    [Header("Beam Settings")]
    public GameObject beamPrefab;
    public Transform beamOrigin;
    public GameObject crystal;

    private Dictionary<Transform, LineRenderer> activeBeams = new();

    // ✅ Event for other systems (e.g. HealingZone) to hook into
    public event Action<List<Transform>> OnHealablePlayersUpdated;

    void Start()
    {
        crystal = GetComponentInChildren<CrystalHealth>()?.gameObject;
    }

    void Update()
    {
        if (crystal == null)
        {
            Destroy(gameObject);
            return;
        }

        List<Transform> currentFrameInRange = new();

        foreach (string tag in playerTags)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(tag);

            foreach (GameObject player in players)
            {
                Transform midPoint = player.transform.Find("PlayerMidPos");
                if (midPoint == null) continue;

                float dist = Vector3.Distance(midPoint.position, beamOrigin.position);
                if (dist <= healRange)
                {
                    currentFrameInRange.Add(player.transform); // add root player
                }
            }
        }

        // ✅ Notify listeners (like HealingZone)
        OnHealablePlayersUpdated?.Invoke(currentFrameInRange);
        //Debug.Log($"[BeamController] Invoking heal event for {currentFrameInRange.Count} players.");


        // 🎯 Beam visual logic
        foreach (Transform player in currentFrameInRange)
        {
            Transform midPoint = player.Find("PlayerMidPos");
            if (midPoint == null) continue;

            if (!activeBeams.ContainsKey(player))
            {
                GameObject beamGO = Instantiate(beamPrefab, transform);
                LineRenderer lr = beamGO.GetComponent<LineRenderer>();
                activeBeams[player] = lr;
            }

            LineRenderer line = activeBeams[player];
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.SetPosition(0, beamOrigin.position);
            line.SetPosition(1, midPoint.position + Vector3.up * yOffset);
        }

        // Cleanup
        List<Transform> toRemove = new();
        foreach (var kvp in activeBeams)
        {
            if (!currentFrameInRange.Contains(kvp.Key))
            {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var player in toRemove)
        {
            activeBeams.Remove(player);
        }
    }
}
