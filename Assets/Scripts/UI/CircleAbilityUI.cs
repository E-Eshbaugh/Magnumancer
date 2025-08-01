using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class CircleAbilityUI : MonoBehaviour
{
    [Header("UI")]
    public Image glowImage;
    public Image crestImage;

    [Header("Visual Settings")]
    public float scale = 1.2f;
    public float maxAlpha = 0.8f;
    public float pulseIntensity = 1.2f;
    public float pulseSpeed = 3f;

    [Header("Controller")]
    public Gamepad gamepad;

    private Coroutine pulseRoutine;
    private Color sampledCrestColor;

    void Start()
    {
        if (crestImage == null || glowImage == null)
        {
            Debug.LogWarning("Missing images!");
            return;
        }

        // Force reassign canvas camera if not set
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
            Debug.Log("[CircleAbilityUI] Reassigned Canvas camera to MainCamera at runtime.");
        }

        // Force glow visibility for debug
        glowImage.color = new Color(1f, 0f, 0f, 0.9f); // red debug color
        glowImage.enabled = true;
    }


    public void Setup(Gamepad pad, Sprite crest)
    {
        gamepad = pad;
        if (crest != null)
        {
            crestImage.sprite = crest;
            sampledCrestColor = crestImage.color;
            sampledCrestColor.a = 1f; // full opacity
        }
    }



    private void TrySampleCrestColor(Sprite sprite)
    {
        try
        {
            Texture2D tex = sprite.texture;
            Rect r = sprite.rect;
            int x = Mathf.FloorToInt(r.x + r.width * 0.5f);
            int y = Mathf.FloorToInt(r.y + r.height * 0.5f);
            sampledCrestColor = tex.GetPixel(x, y);
            sampledCrestColor.a = 1f;

            // Fallback if texture sampling fails or returns black
            if (sampledCrestColor.r <= 0.01f &&
                sampledCrestColor.g <= 0.01f &&
                sampledCrestColor.b <= 0.01f)
            {
                Debug.LogWarning("[CircleAbilityUI] Sampled crest color came back black. Using fallback color.");
                sampledCrestColor = Color.magenta;
            }
        }
        catch
        {
            Debug.LogWarning("[CircleAbilityUI] Failed to sample crest texture. Using fallback color.");
            sampledCrestColor = Color.magenta;
        }
    }

    public void SetCooldownFill(float t)
    {
        float baseA = Mathf.Lerp(0f, maxAlpha, t);
        glowImage.color = new Color(
            sampledCrestColor.r,
            sampledCrestColor.g,
            sampledCrestColor.b,
            baseA
        );

        // Pulse when full
        if (t >= 1f && pulseRoutine == null)
        {
            pulseRoutine = StartCoroutine(PulseAlpha(baseA));
        }
        else if (t < 1f && pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }
    }

    private IEnumerator PulseAlpha(float baseAlpha)
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(1f - (pulseIntensity - 1f), pulseIntensity, t);
            float a = baseAlpha * intensity;
            var c = glowImage.color;
            c.a = a;
            glowImage.color = c;
            yield return null;
        }
    }

    private Color GetCrestColor(Sprite crest)
    {
        if (crest == null) return Color.magenta;

        string name = crest.name.ToLower();
        if (name.Contains("fire")) return new Color(1f, 0.3f, 0.1f);       // Fire red-orange
        if (name.Contains("frost") || name.Contains("ice")) return new Color(0.4f, 0.8f, 1f); // Ice blue
        if (name.Contains("lightning") || name.Contains("volt")) return new Color(1f, 1f, 0.2f); // Electric yellow
        if (name.Contains("nature") || name.Contains("verdant")) return new Color(0.4f, 1f, 0.4f); // Green
        if (name.Contains("earth") || name.Contains("granite")) return new Color(0.6f, 0.4f, 0.2f); // Brown
        if (name.Contains("dark") || name.Contains("hollow")) return new Color(0.5f, 0f, 0.8f); // Purple
        if (name.Contains("water") || name.Contains("tide")) return new Color(0.2f, 0.5f, 1f); // Deep blue
        if (name.Contains("poison") || name.Contains("blight")) return new Color(0.6f, 1f, 0.3f); // Toxic green

        return Color.white;
    }

}
