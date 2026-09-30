using UnityEngine;

/// Spawns Blightward's poison cloud (their passive effect prefab) owned by `owner`,
/// or a green damage zone if the wizard has no cloud prefab.
public static class PoisonCloud
{
    public static void Spawn(GameObject owner, Vector3 at, float fallbackRadius = 3f, float fallbackDuration = 6f)
    {
        var wiz = AbilityKit.Wizard(owner);
        var prefab = wiz != null ? wiz.passiveEffectPrefab : null;
        if (prefab != null && prefab.GetComponentInChildren<PoisonCloudHazard>(true) != null)
        {
            var cloud = Object.Instantiate(prefab, at, Quaternion.identity);
            foreach (var hz in cloud.GetComponentsInChildren<PoisonCloudHazard>())
            {
                hz.owner = owner;
                hz.ownerImmune = true;
            }
            return;
        }
        var h = GroundHazard.Spawn(owner, at, fallbackRadius, fallbackDuration, AbilityKit.Theme(owner));
        h.damagePerSecond = 10f;
    }
}
