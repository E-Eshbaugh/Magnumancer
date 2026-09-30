using UnityEngine;

/// A light that fades to nothing and removes itself (PowerFx.Flash)
public class FadingLight : MonoBehaviour
{
    Light l;
    float start, time, t;

    public void Init(float intensity, float duration)
    {
        l = GetComponent<Light>();
        start = intensity;
        time = Mathf.Max(0.01f, duration);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = 1f - t / time;
        if (l != null) l.intensity = start * k * k;
        if (t >= time) Destroy(gameObject);
    }
}
