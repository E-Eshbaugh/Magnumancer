using UnityEngine;

/// <summary>Gentle float: bobs and (optionally) turns a display object.</summary>
public class HallBob : MonoBehaviour
{
    public float amplitude = 0.08f;
    public float speed = 1.4f;
    public float spin;          // degrees per second around world up
    public float phase;
    public Vector3 basePos;
    public bool useBase;

    void Start()
    {
        if (!useBase) basePos = transform.localPosition;
        if (phase == 0f) phase = Random.value * 10f;
    }

    void Update()
    {
        transform.localPosition = basePos + Vector3.up * Mathf.Sin((Time.time + phase) * speed) * amplitude;
        if (spin != 0f) transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
    }
}
