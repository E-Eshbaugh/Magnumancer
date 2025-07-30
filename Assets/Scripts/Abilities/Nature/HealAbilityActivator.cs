using UnityEngine;
using Magnumancer.Abilities;

public class HealAbilityActivator : MonoBehaviour, IActiveAbility
{
    public GameObject healCirclePrefab; // Prefab for the healing circle

    public void Activate(GameObject caster)
    {
        Vector3 position = caster.transform.Find("FeetPos")?.position ?? caster.transform.position;
        Instantiate(healCirclePrefab, position, healCirclePrefab.transform.rotation);
    }
}
