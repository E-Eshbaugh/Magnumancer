using UnityEngine;

/// <summary>
/// Subtle buff indicator: a soft colored point light around the player.
/// Passives call Set() every frame with a 0..1 strength (0 hides it).
/// </summary>
public class PassiveGlow : MonoBehaviour
{
    public float maxIntensity = 2.5f;
    public float range = 3f;
    public float height = 1f;
    public float fadeSpeed = 6f;

    Light glow;
    float targetIntensity;
    float pulseSpeed;

    public static PassiveGlow On(GameObject player)
    {
        var g = player.GetComponent<PassiveGlow>();
        return g ? g : player.AddComponent<PassiveGlow>();
    }

    void Awake()
    {
        var go = new GameObject("PassiveGlow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * height;
        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = range;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    /// strength 0..1; pulse > 0 makes it breathe at that speed (e.g. for short, strong buffs)
    public void Set(Color color, float strength, float pulse = 0f)
    {
        glow.color = color;
        targetIntensity = Mathf.Clamp01(strength) * maxIntensity;
        pulseSpeed = pulse;
    }

    void Update()
    {
        float target = targetIntensity;
        if (pulseSpeed > 0f && target > 0f)
            target *= 0.65f + 0.35f * Mathf.Sin(Time.time * pulseSpeed);

        glow.intensity = Mathf.MoveTowards(glow.intensity, target, fadeSpeed * maxIntensity * Time.deltaTime);
        glow.enabled = glow.intensity > 0.01f;
    }

    void OnDisable()
    {
        if (glow) { glow.intensity = 0f; glow.enabled = false; }
    }
}
