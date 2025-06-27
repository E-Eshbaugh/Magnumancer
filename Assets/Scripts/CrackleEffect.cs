using UnityEngine;

public class CrackleEffect : MonoBehaviour
{
    public Light stormLight;
    public float minIntensity = 2f;
    public float maxIntensity = 5f;
    public float flickerSpeed = 0.1f;

    void Update()
    {
        stormLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, Mathf.PerlinNoise(Time.time * 10f, 0f));
    }
}
