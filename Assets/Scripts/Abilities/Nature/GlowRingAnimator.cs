using UnityEngine;

public class GlowRingAnimator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 30f; // degrees per second

    [Header("Bob Settings")]
    public float bobAmplitude = 0.5f; // how high up/down they go
    public float bobFrequency = 1f;   // how fast they bounce

    private Transform[] spikes;
    private Vector3[] basePositions;

    void Start()
    {
        // Cache all FullSpike children
        int count = transform.childCount;
        spikes = new Transform[count];
        basePositions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            spikes[i] = transform.GetChild(i);
            basePositions[i] = spikes[i].localPosition;
        }
    }

    void Update()
    {
        // Rotate the whole ring
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Bob each spike individually
        float time = Time.time * bobFrequency;
        for (int i = 0; i < spikes.Length; i++)
        {
            float offset = Mathf.Sin(time + i) * bobAmplitude;
            Vector3 newPos = basePositions[i];
            newPos.y += offset;
            spikes[i].localPosition = newPos;
        }
    }
}
