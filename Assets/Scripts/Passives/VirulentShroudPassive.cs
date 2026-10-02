using UnityEngine;

/// <summary>
/// Blightward — Virulent Shroud: when enemies die (or lose a life) nearby, they burst into
/// poisonous spores, corrupting enemies nearby. When the Blightward falls, their own
/// corrupted body bursts too, so it pays off even in a 1v1.
/// (Spawns a poison cloud that spares Blightward.)
/// </summary>
public class VirulentShroudPassive : WizardPassive
{
    public float radius = 10f;
    public bool burstOnOwnLifeLost = true;

    void OnEnable() => DamageEvents.Killed += OnKilled;
    void OnDisable() => DamageEvents.Killed -= OnKilled;

    void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        bool self = victim == gameObject;
        if (self ? !burstOnOwnLifeLost : (!IsAlive || !IsEnemy(victim))) return;
        if (!self && (position - transform.position).sqrMagnitude > radius * radius) return;

        // The cloud prefab handles its own fade-out and cleanup
        var cloud = SpawnEffect(position, 0f);
        if (cloud != null && (cloud.GetComponentInChildren<PoisonCloudHazard>() is PoisonCloudHazard hazard))
        {
            hazard.owner = gameObject;
            hazard.ownerImmune = true;
        }
    }
}
