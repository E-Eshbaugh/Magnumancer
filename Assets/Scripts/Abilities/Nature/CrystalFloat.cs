using UnityEngine;

public class CrystalFloat : MonoBehaviour
{
    [Header("Float Settings")]
    public float floatAmplitude = 0.25f;
    public float floatFrequency = 1f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position; // world position
    }

    void Update()
    {
        float offsetY = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = startPos + new Vector3(0f, offsetY, 0f); // world Y axis
    }
}
