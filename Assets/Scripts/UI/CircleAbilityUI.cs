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
        if (crestImage == null || crestImage.sprite == null)
        {
            glowImage.enabled = false;
            enabled = false;
            return;
        }

        Sprite sprite = crestImage.sprite;
        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;
        int x = Mathf.FloorToInt(r.x + r.width * 0.5f);
        int y = Mathf.FloorToInt(r.y + r.height * 0.5f);
        sampledCrestColor = tex.GetPixel(x, y);
        sampledCrestColor.a = 1f;

        glowImage.rectTransform.localScale = Vector3.one * scale;
    }

    public void Setup(Gamepad pad, Sprite crest)
    {
        gamepad = pad;
        if (crest != null)
        {
            crestImage.sprite = crest;
            Texture2D tex = crest.texture;
            Rect r = crest.rect;
            int x = Mathf.FloorToInt(r.x + r.width * 0.5f);
            int y = Mathf.FloorToInt(r.y + r.height * 0.5f);
            sampledCrestColor = tex.GetPixel(x, y);
            sampledCrestColor.a = 1f;
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
}
