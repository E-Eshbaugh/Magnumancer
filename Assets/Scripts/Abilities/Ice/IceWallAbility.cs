using UnityEngine;
using Magnumancer.Abilities;

public class IceWallAbility : MonoBehaviour, IActiveAbility
{
    [Header("Wall Settings")]
    [SerializeField] GameObject iceWallEffectPrefab;
    [SerializeField] float forwardDistance = 1.2f;

    private GameObject lastWall;

    public void Activate(GameObject caster)
    {
        if (!iceWallEffectPrefab || !caster) return;

        // Raycast to find ground position in front of player
        Vector3 eyeLevel = caster.transform.position + Vector3.up * 1.5f;
        Vector3 forward = caster.transform.forward;
        Vector3 testPoint = eyeLevel + forward * forwardDistance;

        // Ignore players/monsters and triggers (poison clouds, lava) so the wall lands on the floor
        if (Physics.Raycast(testPoint, Vector3.down, out RaycastHit hit, 5f,
                ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
        {
            testPoint = hit.point;
        }

        Quaternion rotation = Quaternion.LookRotation(-forward);

        // Handle previous wall
        if (lastWall != null)
        {
            IceWallEffect effect = lastWall.GetComponent<IceWallEffect>();
            if (effect != null) effect.BeginMelt();
            else Destroy(lastWall);

            lastWall = null;
        }

        lastWall = Instantiate(iceWallEffectPrefab, testPoint, rotation);
        lastWall.GetComponent<IceWallEffect>()?.BeginRise();
    }

}
