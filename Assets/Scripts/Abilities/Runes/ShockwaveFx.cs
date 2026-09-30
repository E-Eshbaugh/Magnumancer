using UnityEngine;

/// Expanding ring used by AbilityKit.Shockwave
public class ShockwaveFx : MonoBehaviour
{
    LineRenderer lr;
    Vector3 center;
    float radius, time, t;
    Color color;

    public void Init(Vector3 c, float r, Color col, float duration)
    {
        center = c; radius = r; color = col; time = duration;
        lr = GlowLine.Make(transform, "Ring", 48, 0.3f, AbilityKit.Glow());
        lr.loop = true;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / time);
        AbilityKit.Circle(lr, center, Mathf.Lerp(0.3f, radius, 1f - (1f - k) * (1f - k)));
        lr.widthMultiplier = Mathf.Lerp(1.4f, 0.3f, k);
        GlowLine.SetColor(lr, Color.Lerp(Color.white, color, k), 1f - k);
        if (k >= 1f) Destroy(gameObject);
    }
}
