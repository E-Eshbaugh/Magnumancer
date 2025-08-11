using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightSoftPulse : MonoBehaviour
{
    [Header("Pulse Settings")]
    [Tooltip("How fast the light pulses (cycles per second)")]
    public float pulseSpeed = 1f;

    [Tooltip("Maximum intensity at pulse peak")]
    public float maxIntensity = 3f;

    [Tooltip("Minimum intensity at pulse trough (never zero for a soft glow)")]
    public float minIntensity = 1.5f;

    [Tooltip("Randomizes the pulse phase so multiple lights are offset")]
    public bool randomizeStart = true;

    private Light targetLight;
    private float baseTimeOffset;

    void Awake()
    {
        targetLight = GetComponent<Light>();
        if (randomizeStart)
            baseTimeOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float t = (Mathf.Sin((Time.time * pulseSpeed * 2f * Mathf.PI) + baseTimeOffset) + 1f) / 2f;
        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
    }
}
