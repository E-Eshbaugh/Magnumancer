using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// A poison gas cloud (Viper's Nest mines, Virulent Shroud). An element zone: standing
/// in it leaves you Poisoned, and fire that touches it Combusts the whole cloud.
public class PoisonCloudHazard : MonoBehaviour, IElementZone
{
    [Header("Damage Settings")]
    public float damagePerSecond = 10f;
    public float tickInterval = 1f;
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4", "Monster"};

    [HideInInspector] public GameObject owner;     // who created the cloud (damage credit)
    [HideInInspector] public bool ownerImmune;     // Virulent Shroud clouds spare their owner

    [Tooltip("Reach beyond the trigger for reactions (the gas looks bigger than its collider)")]
    public float reactionPadding = 0.6f;

    private Dictionary<GameObject, Coroutine> activeDamageCoroutines = new();
    private Collider zone;

    void Start()
    {
        zone = GetComponent<Collider>();
        ElementZones.Register(this);
    }

    // ---------- IElementZone ----------
    public Element ZoneElement => Element.Poison;
    public GameObject ZoneOwner => owner;
    public Vector3 ZoneCenter => zone != null ? zone.bounds.center : transform.position;
    public float ZoneRadius => (zone != null ? Mathf.Max(zone.bounds.extents.x, zone.bounds.extents.z) : 1.5f) + reactionPadding;
    public float DistanceTo(Vector3 p)
        => Mathf.Max(0f, (zone != null ? ElementZones.FlatDistance(zone, p) : ElementZones.FlatDistance(transform.position, 1.5f, p)) - reactionPadding);

    /// Combusted: the gas is gone
    public void Consume()
    {
        ElementZones.Unregister(this);
        var root = GetComponentInParent<PoisonCloudFadeOut>();
        Destroy(root != null ? root.gameObject : gameObject);
    }

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
            return;
        }

        // Poisoned while inside and a few seconds after; a Burning target Combusts
        ElementReactions.ZoneHit(player, owner, Element.Poison, damagePerSecond);
    }

    private void OnDisable()
    {
        ElementZones.Unregister(this);
        // Stop all active coroutines on despawn
        foreach (var kvp in activeDamageCoroutines)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);
        }

        activeDamageCoroutines.Clear();
    }
}
