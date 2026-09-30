using UnityEngine;
using Magnumancer.Abilities;

public class IceWallAbility : MonoBehaviour, IActiveAbility
{
    [Header("Wall Settings")]
    [SerializeField] GameObject iceWallEffectPrefab;
    [SerializeField] float forwardDistance = 1.2f;
    [Tooltip("Walls never time out — they stand until shot down. Past this many, the oldest shatters.")]
    [SerializeField] int maxWalls = 3;

    private readonly System.Collections.Generic.List<IceWallEffect> walls = new();

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

        // Walls last until they're broken; only the oldest goes if there are too many
        walls.RemoveAll(w => w == null);
        while (walls.Count >= Mathf.Max(1, maxWalls))
        {
            walls[0].Shatter();
            walls.RemoveAt(0);
        }

        var wall = Instantiate(iceWallEffectPrefab, testPoint, rotation).GetComponent<IceWallEffect>();
        if (wall != null)
        {
            wall.BeginRise();
            walls.Add(wall);
        }
    }

}
