using UnityEngine;
using Magnumancer.Abilities;

public class HealAbilityActivator : MonoBehaviour, IActiveAbility
{
    public GameObject healCirclePrefab; // Prefab for the healing circle
    private GameObject lastHealCircle;  // Track the previously spawned one

    public void Activate(GameObject caster)
    {
        if (!healCirclePrefab || !caster) return;

        // Remove the previous healing zone if still active
        if (lastHealCircle != null)
        {
            Destroy(lastHealCircle);
            lastHealCircle = null;
        }

        // Use feet position or fallback
        Vector3 position = caster.transform.Find("FeetPos")?.position ?? caster.transform.position;

        // Spawn the new healing zone
        lastHealCircle = Instantiate(healCirclePrefab, position, healCirclePrefab.transform.rotation);
    }
}
