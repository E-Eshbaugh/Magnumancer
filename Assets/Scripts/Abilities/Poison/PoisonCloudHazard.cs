using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoisonCloudHazard : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damagePerSecond = 10f;
    public float tickInterval = 1f;
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4", "Monster"};

    [HideInInspector] public GameObject owner;     // who created the cloud (damage credit)
    [HideInInspector] public bool ownerImmune;     // Virulent Shroud clouds spare their owner

    private Dictionary<GameObject, Coroutine> activeDamageCoroutines = new();

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            if (!activeDamageCoroutines.ContainsKey(other.gameObject))
            {
                // Start damaging immediately
                Coroutine c = StartCoroutine(DamageOverTime(other.gameObject));
                activeDamageCoroutines.Add(other.gameObject, c);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (activeDamageCoroutines.TryGetValue(other.gameObject, out Coroutine c))
        {
            StopCoroutine(c);
            activeDamageCoroutines.Remove(other.gameObject);
        }
    }

    private bool IsPlayer(GameObject obj)
    {
        if (ownerImmune && obj == owner) return false;
        foreach (string tag in playerTags)
        {
            if (obj.CompareTag(tag))
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator DamageOverTime(GameObject player)
    {
        // Initial damage on entry
        ApplyDamage(player);

        while (true)
        {
            yield return new WaitForSeconds(tickInterval);

            // Target died (goblins are destroyed, players deactivated) without
            // triggering OnTriggerExit — stop ticking on it.
            if (player == null || !player.activeInHierarchy)
            {
                activeDamageCoroutines.Remove(player);
                yield break;
            }

            ApplyDamage(player);
        }
    }

    private void ApplyDamage(GameObject player)
    {
        if (player.TryGetComponent<PlayerHealthControl>(out var health))
        {
            health.TakeDamage(damagePerSecond, owner);
        }
        else if (player.TryGetComponent<GoblinHealth>(out var goblinHealth))
        {
            goblinHealth.TakeDamage(damagePerSecond, owner);
        }
        else
        {
            Debug.LogWarning($"[PoisonCloud] {player.name} has no PlayerHealth component!");
        }
    }

    private void OnDisable()
    {
        // Stop all active coroutines on despawn
        foreach (var kvp in activeDamageCoroutines)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);
        }

        activeDamageCoroutines.Clear();
    }
}
