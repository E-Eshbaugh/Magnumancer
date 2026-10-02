using UnityEngine;

/// An authored horde entrance enabled by broken progression barriers. The spawner
/// also requires a safe position and a navigation route to a standing teammate.
public class ZombieSpawnPoint : MonoBehaviour
{
    [Min(0f)] public float scatterRadius = 1.25f;
    [Tooltip("Required progression gates. All must be broken before this entrance can spawn.")]
    public DestructibleWall[] requiredOpenings;

    public float LastSpawnTime { get; private set; } = float.NegativeInfinity;
    public bool GatesOpen
    {
        get
        {
            if (requiredOpenings != null)
                foreach (var wall in requiredOpenings)
                    if (wall == null || !wall.IsDestroyed) return false;
            return true;
        }
    }

    public void MarkUsed() => LastSpawnTime = Time.time;

    void OnDrawGizmos()
    {
        Gizmos.color = GatesOpen ? new Color(0.2f, 1f, 0.6f, 0.7f) : new Color(1f, 0.45f, 0.15f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, scatterRadius);
        if (requiredOpenings != null)
            foreach (var wall in requiredOpenings)
                if (wall != null) Gizmos.DrawLine(transform.position, wall.transform.position);
    }
}
