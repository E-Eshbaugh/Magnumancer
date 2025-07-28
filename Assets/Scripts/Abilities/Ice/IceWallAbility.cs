using UnityEngine;
using Magnumancer.Abilities;

public class IceWallAbility : MonoBehaviour, IActiveAbility
{
    [Header("Wall Settings")]
    [SerializeField] GameObject iceWallEffectPrefab;
    [SerializeField] float forwardDistance = 1.2f;

    public void Activate(GameObject caster)
    {
        if (!iceWallEffectPrefab || !caster) return;

        // Determine spawn point in front of player face
        Vector3 eyeLevel = caster.transform.position + Vector3.up * 1.5f;
        Vector3 forward = caster.transform.forward;
        Vector3 spawnPoint = eyeLevel + forward * forwardDistance;

        Quaternion rotation = Quaternion.LookRotation(-forward);
        GameObject wall = Instantiate(iceWallEffectPrefab, spawnPoint, rotation);

        // Let the effect handle the rise
        wall.GetComponent<IceWallEffect>()?.BeginRise();
    }
}
