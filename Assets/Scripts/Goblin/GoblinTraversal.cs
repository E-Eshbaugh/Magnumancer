using UnityEngine;
using UnityEngine.AI;

/// Short, collision-checked jumps between walkable surfaces. The agent keeps
/// owning ground movement; only the jump arc temporarily owns the transform.
[RequireComponent(typeof(NavMeshAgent))]
public class GoblinTraversal : MonoBehaviour
{
    [Tooltip("About the height of a wizard's two jumps (5 impulse, 9.81 gravity).")]
    [Min(0f)] public float jumpHeight = 2.5f;
    [Min(0f)] public float jumpDistance = 5f;
    [Min(0f)] public float jumpCooldown = 0.8f;
    [Min(0.1f)] public float probeInterval = 0.3f;

    public bool IsJumping { get; private set; }
    NavMeshAgent agent;
    readonly RaycastHit[] hits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    Vector3 takeoff, landing;
    float nextProbe, nextJump, elapsed, duration, launchSpeed;
    float radius, height;
    bool savedPosition, savedRotation;
    const float Gravity = 9.81f;
    const float Skin = 0.12f;
    NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        radius = agent.radius;
        height = agent.height;
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            Vector3 scale = transform.lossyScale;
            radius = Mathf.Max(radius, capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            height = Mathf.Max(height, capsule.height * Mathf.Abs(scale.y));
        }
        height = Mathf.Max(height, radius * 2f);
        nextProbe = Time.time + Mathf.Abs(GetEntityId().GetHashCode() % 17) * 0.015f;
    }

    public bool TryDestination(Vector3 target, out Vector3 destination)
    {
        destination = target;
        if (!Ground(target + Vector3.up * 0.3f, 4f, out var ground)) return false;
        if (!NavMesh.SamplePosition(ground.point, out var nav, 0.6f, Filter) &&
            !NavMesh.SamplePosition(ground.point, out nav, jumpHeight + 0.5f, Filter)) return false;
        destination = nav.position;
        return true;
    }

    // A grounded target on the same staircase is reachable even when its feet
    // differ by more than the flat-ground jump-dodge allowance. Separate ledges,
    // airborne targets and intervening walls retain the original height limit.
    public bool SharesWalkableSlope(Vector3 target, float reach)
    {
        Vector3 from = transform.position;
        if (Mathf.Abs(target.y - from.y) > reach + 0.25f) return false;
        if (!Ground(target + Vector3.up * 0.15f, 0.45f, out var support)) return false;
        if (!NavMesh.SamplePosition(support.point, out var end, 0.4f, Filter)) return false;
        if (!NavMesh.SamplePosition(from, out var start, 0.4f, Filter)) return false;
        return !NavMesh.Raycast(start.position, end.position, out _, Filter);
    }

    public bool TryJump(Vector3 target)
    {
        if (IsJumping || Time.time < nextProbe || Time.time < nextJump || agent.speed < 0.1f || agent.isStopped) return false;
        nextProbe = Time.time + Mathf.Max(0.1f, probeInterval);
        Vector3 delta = target - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.5f || jumpHeight <= 0f || jumpDistance <= 0f) return false;
        Vector3 direction = delta / distance;
        // Only consider a jump when approaching an actual navigation edge. Normal
        // stairs and open ground remain a continuous walk, including while slowed.
        if (!NavMesh.Raycast(agent.nextPosition, target, out var edge, Filter) ||
            PlanarDistance(transform.position, edge.position) > radius + 0.8f) return false;

        float limit = Mathf.Min(jumpDistance, distance + 0.3f);
        for (float d = Mathf.Max(1f, radius * 2f); d <= limit; d += 0.4f)
        {
            Vector3 probe = transform.position + direction * d;
            probe.y += jumpHeight + 0.3f;
            if (!Ground(probe, jumpHeight * 2f + 0.3f, out var support)) continue;
            if (support.normal.y < 0.7f) continue;
            if (!NavMesh.SamplePosition(support.point, out var nav, 0.45f, Filter)) continue;
            Vector3 end = nav.position + Vector3.up * agent.baseOffset;
            float rise = end.y - transform.position.y;
            if (rise > jumpHeight - 0.2f || rise < -jumpHeight) continue;
            if (PlanarDistance(end, target) > distance - 0.5f) continue;
            if (!NavMesh.Raycast(agent.nextPosition, nav.position, out _, Filter)) continue;
            if (!ClearBody(end)) continue;

            // Prefer a low hop, increasing the apex only enough to clear cover.
            float minApex = Mathf.Max(0.6f, rise + 0.25f);
            for (float apex = minApex; apex <= jumpHeight + 0.001f; apex = Mathf.Min(apex + 0.4f, jumpHeight))
            {
                float speed = Mathf.Sqrt(2f * Gravity * apex);
                float flight = (speed + Mathf.Sqrt(speed * speed - 2f * Gravity * rise)) / Gravity;
                if (PlanarDistance(transform.position, end) / flight <= 6f && ClearArc(transform.position, end, speed, flight))
                {
                    BeginJump(end, speed, flight);
                    return true;
                }
                if (apex >= jumpHeight) break;
            }
        }
        return false;
    }

    void BeginJump(Vector3 end, float speed, float flight)
    {
        takeoff = transform.position;
        landing = end;
        launchSpeed = speed;
        duration = flight;
        elapsed = 0f;
        savedPosition = agent.updatePosition;
        savedRotation = agent.updateRotation;
        agent.ResetPath();
        agent.updatePosition = agent.updateRotation = false;
        IsJumping = true;
        Vector3 facing = end - takeoff; facing.y = 0f;
        transform.rotation = Quaternion.LookRotation(facing);
    }

    public void TickJump()
    {
        if (!IsJumping) return;
        elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
        Vector3 next = Arc(takeoff, landing, launchSpeed, duration, elapsed);
        // Cover may have appeared since takeoff. Never fly through a new wall.
        if (!ClearSegment(transform.position, next) || !ClearBody(next)) { CancelJump(); return; }
        transform.position = next;
        if (elapsed < duration) return;
        if (!NavMesh.SamplePosition(landing - Vector3.up * agent.baseOffset, out var nav, 0.45f, Filter) || !ClearBody(landing))
        { CancelJump(); return; }
        FinishJump(nav.position);
    }

    public void CancelJump()
    {
        if (!IsJumping) return;
        // The agent stayed at takeoff; its current position also includes any
        // status-effect shove that interrupted the jump. Do not own isStopped.
        FinishJump(agent.nextPosition - Vector3.up * agent.baseOffset);
    }

    void FinishJump(Vector3 ground)
    {
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.Warp(ground);
        else transform.position = takeoff;
        agent.updatePosition = savedPosition;
        agent.updateRotation = savedRotation;
        IsJumping = false;
        nextJump = Time.time + jumpCooldown;
    }

    void OnDisable() => CancelJump();

    static float PlanarDistance(Vector3 a, Vector3 b)
    { a.y = b.y = 0f; return Vector3.Distance(a, b); }

    static Vector3 Arc(Vector3 start, Vector3 end, float speed, float flight, float t)
    {
        Vector3 point = Vector3.Lerp(start, end, t / flight);
        point.y = start.y + speed * t - 0.5f * Gravity * t * t;
        return point;
    }

    bool ClearArc(Vector3 start, Vector3 end, float speed, float flight)
    {
        Vector3 previous = start;
        int steps = Mathf.Max(12, Mathf.CeilToInt(flight / 0.04f));
        for (int i = 1; i <= steps; i++)
        {
            Vector3 next = Arc(start, end, speed, flight, flight * i / steps);
            if (!ClearSegment(previous, next) || !ClearBody(next)) return false;
            previous = next;
        }
        return true;
    }

    bool Solid(Collider collider)
    {
        if (collider.transform.IsChildOf(transform)) return false;
        return !DamageEvents.IsCombatant(DamageEvents.RootOf(collider));
    }

    bool Ground(Vector3 origin, float distance, out RaycastHit ground)
    {
        ground = default;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false; // fail closed if the query overflowed
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (hits[i].distance < nearest && Solid(hits[i].collider))
            { ground = hits[i]; nearest = ground.distance; }
        return nearest < float.PositiveInfinity;
    }

    void Capsule(Vector3 feet, out Vector3 bottom, out Vector3 top)
    {
        bottom = feet + Vector3.up * (radius + Skin);
        top = feet + Vector3.up * Mathf.Max(radius + Skin, height - radius);
    }

    bool ClearBody(Vector3 feet)
    {
        Capsule(feet, out var bottom, out var top);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (Solid(overlaps[i])) return false;
        return true;
    }

    bool ClearSegment(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.000001f) return true;
        Capsule(from, out var bottom, out var top);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta.normalized, hits, delta.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++) if (Solid(hits[i].collider)) return false;
        return true;
    }
}
