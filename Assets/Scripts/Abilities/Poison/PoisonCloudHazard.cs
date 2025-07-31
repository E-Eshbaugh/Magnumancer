using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoisonCloudHazard : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damagePerSecond = 10f;
    public float tickInterval = 1f;
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

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
        foreach (string tag in playerTags)
        {
            if (obj.CompareTag(tag))
                return true;
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
            ApplyDamage(player);
        }
    }

    private void ApplyDamage(GameObject player)
    {
        if (player.TryGetComponent<PlayerHealthControl>(out var health))
        {
            health.TakeDamage(damagePerSecond);
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
