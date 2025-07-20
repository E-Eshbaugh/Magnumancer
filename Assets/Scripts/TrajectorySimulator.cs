using UnityEngine;

public class TrajectorySimulator : MonoBehaviour
{
    [Header("Physics")]
    public Vector3 gravity = new Vector3(0, -11.5f, 0);
    public float timeStep = 0.05f;
    public float maxSimTime = 2.0f;
    public float radius = 0.15f;
    public LayerMask collisionMask = ~0;

    // Internal reusable buffer
    Vector3[] buffer;
    public Vector3[] Buffer => buffer;
    public int Count { get; private set; }
    public bool Hit { get; private set; }
    public RaycastHit HitInfo { get; private set; }

    void Awake()
    {
        // generous default capacity
        buffer = new Vector3[256];
    }

    public void Simulate(Vector3 startPos, Vector3 initialVelocity)
    {
        Count = 0;
        Hit = false;

        Vector3 pos = startPos;
        Vector3 vel = initialVelocity;
        float t = 0f;

        while (t < maxSimTime)
        {
            // store point
            if (Count >= buffer.Length) System.Array.Resize(ref buffer, buffer.Length * 2);
            buffer[Count++] = pos;

            // integrator
            Vector3 newVel = vel + gravity * timeStep;
            Vector3 newPos = pos + vel * timeStep + 0.5f * gravity * (timeStep * timeStep);

            Vector3 seg = newPos - pos;
            float dist = seg.magnitude;
            if (dist > 0f)
            {
                if (Physics.SphereCast(pos, radius, seg.normalized, out var hit, dist,
                        collisionMask, QueryTriggerInteraction.Ignore))
                {
                    if (Count >= buffer.Length) System.Array.Resize(ref buffer, buffer.Length * 2);
                    buffer[Count++] = hit.point;
                    Hit = true;
                    HitInfo = hit;
                    break;
                }
            }

            pos = newPos;
            vel = newVel;
            t += timeStep;
        }
    }
}
