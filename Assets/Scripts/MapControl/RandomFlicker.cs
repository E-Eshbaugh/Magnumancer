using UnityEngine;

[RequireComponent(typeof(Light))]
public class RandomFlicker : MonoBehaviour
{
    public float baseIntensity = 1f;      // The average light intensity
    public float pulseAmplitude = 0.3f;   // How much it pulses (+/- around base)
    public float pulseSpeed = 1f;         // How fast it pulses

    private Light pointLight;
    private float randomOffset;

    void Start()
    {
        pointLight = GetComponent<Light>();
        randomOffset = Random.Range(0f, 2f * Mathf.PI); // Prevent sync between lights
    }

    void Update()
    {
        float pulse = Mathf.Sin(Time.time * pulseSpeed + randomOffset);
        pointLight.intensity = baseIntensity + pulseAmplitude * pulse;
    }
}

