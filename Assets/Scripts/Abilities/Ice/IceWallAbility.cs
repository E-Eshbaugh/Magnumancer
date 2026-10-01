using UnityEngine;
using Magnumancer.Abilities;

public class IceWallAbility : MonoBehaviour, IActiveAbility
{
    [Header("Wall Settings")]
    [SerializeField] GameObject iceWallEffectPrefab;
    [Tooltip("Far enough ahead that the caster isn't standing in the (thick) wall")]
    [SerializeField] float forwardDistance = 2.2f;
    [Tooltip("Walls can't be broken and melt on their own. Past this many, the oldest melts away early.")]
    [SerializeField] int maxWalls = 2;

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

        // Too many up (cooldown cuts): the oldest melts away early
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
