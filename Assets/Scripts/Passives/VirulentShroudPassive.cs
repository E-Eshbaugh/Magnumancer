using UnityEngine;

/// <summary>
/// Blightward — Virulent Shroud: when enemies die nearby, they burst into poisonous spores,
/// corrupting enemies nearby. (Spawns a poison cloud that spares Blightward.)
/// </summary>
public class VirulentShroudPassive : WizardPassive
{
    public float radius = 8f;

    void OnEnable() => DamageEvents.Killed += OnKilled;
    void OnDisable() => DamageEvents.Killed -= OnKilled;

    void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (!IsAlive || !IsEnemy(victim)) return;
        if ((position - transform.position).sqrMagnitude > radius * radius) return;

        // The cloud prefab handles its own fade-out and cleanup
        var cloud = SpawnEffect(position, 0f);
        if (cloud != null && cloud.TryGetComponent<PoisonCloudHazard>(out var hazard))
        {
            hazard.owner = gameObject;
            hazard.ownerImmune = true;
        }
    }
}
