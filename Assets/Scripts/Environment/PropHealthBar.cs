using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The health bar over a damaged prop, styled like the wizards' bars (WizardHealthBar):
/// frame, a trailing chip that drains after the hit, a flash and a jolt. Unlike wizard
/// bars it only exists while the prop is being hit: it fades out after a few quiet
/// seconds, so the arena isn't covered in bars. One shared screen-space canvas, drawn
/// under the wizard bars.
/// </summary>
public class PropHealthBar : MonoBehaviour
{
    const float Height = 6f, Border = 2f;
    const float Headroom = 0.35f;
    const float ChipDelay = 0.3f, ChipDrainSpeed = 1.4f;
    const float FlashTime = 0.12f, ShakeTime = 0.2f;
    /// Seconds after the last hit before it fades, and how long the fade takes
    public static float VisibleFor = 2.5f;
    const float FadeTime = 0.5f;

    static readonly Color FillColor = new Color(0.92f, 0.86f, 0.74f);   // neutral: props aren't anyone's
    static readonly Color LowColor = new Color(1f, 0.45f, 0.3f);

    static Canvas canvas;
    static readonly Dictionary<Destructible, PropHealthBar> bars = new();

    Destructible target;
    RectTransform rect, fill, chip, flash;
    Image fillImage, flashImage;
    CanvasGroup group;
    float width, frac = 1f, chipFrac = 1f, chipHoldUntil, flashT, shakeT, shakeAmp, lastHit;

    public static void Show(Destructible d)
    {
        if (d == null) return;
        if (!bars.TryGetValue(d, out var bar) || bar == null)
        {
            bar = Create(d);
            bars[d] = bar;
        }
        bar.OnHit();
    }

    public static void Hide(Destructible d)
    {
        if (d != null && bars.TryGetValue(d, out var bar))
        {
            bars.Remove(d);
            if (bar != null) Destroy(bar.gameObject);
        }
    }

    static PropHealthBar Create(Destructible d)
    {
        var go = new GameObject("PropHealthBar", typeof(RectTransform));
        go.transform.SetParent(Canvas().transform, false);
        var bar = go.AddComponent<PropHealthBar>();
        bar.target = d;
        bar.frac = bar.chipFrac = d.HealthFraction;
        // wider bars for bigger props
        bar.width = Mathf.Clamp(36f + d.Bounds.size.magnitude * 9f, 40f, 90f);
        bar.Build();
        return bar;
    }

    static Canvas Canvas()
    {
        if (canvas != null) return canvas;
        bars.Clear();   // a new map: last map's props are gone
        var go = new GameObject("PropHealthBars");
        canvas = go.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;   // over the Pixelation image (0), under the wizard bars (2)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    void Build()
    {
        rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(width + Border * 2f, Height + Border * 2f);
        rect.anchorMin = rect.anchorMax = Vector2.zero;   // positioned in screen pixels from the corner
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        Box("Frame", rect, new Color(0f, 0f, 0f, 0.6f), rect.sizeDelta, false);
        var back = Box("Back", rect, new Color(0.12f, 0.12f, 0.14f, 0.8f), new Vector2(width, Height), false);
        chip = Box("Chip", back, new Color(1f, 0.92f, 0.75f, 0.9f), new Vector2(width, Height), true);
        fill = Box("Fill", back, FillColor, new Vector2(width, Height), true);
        fillImage = fill.GetComponent<Image>();
        flash = Box("Flash", rect, new Color(1f, 1f, 1f, 0f), rect.sizeDelta, false);
        flashImage = flash.GetComponent<Image>();
    }

    static RectTransform Box(string name, RectTransform parent, Color color, Vector2 size, bool leftAligned)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        if (leftAligned)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
        }
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    void OnHit()
    {
        float before = frac;
        frac = target.HealthFraction;
        float lost = Mathf.Max(0f, before - frac);
        lastHit = Time.time;
        flashT = FlashTime;
        shakeT = ShakeTime;
        shakeAmp = Mathf.Clamp(2f + lost * 30f, 2f, 8f);
        chipHoldUntil = Time.time + ChipDelay;
        group.alpha = Mathf.Max(group.alpha, 0.9f);
    }

    void LateUpdate()
    {
        if (target == null || target.IsDestroyed || !target.isActiveAndEnabled) { Hide(target); if (this) Destroy(gameObject); return; }

        float quiet = Time.time - lastHit;
        float alpha = quiet < VisibleFor ? 1f : 1f - Mathf.Clamp01((quiet - VisibleFor) / FadeTime);
        if (alpha <= 0f) { Hide(target); return; }   // comes back on the next hit

        var cam = Camera.main;
        var b = target.Bounds;
        Vector3 top = new Vector3(b.center.x, b.max.y + Headroom, b.center.z);
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(top) : Vector3.back;
        if (cam == null || screen.z <= 0f || GamePause.IsPaused) { group.alpha = 0f; return; }

        float dt = Time.deltaTime;
        flashT = Mathf.Max(0f, flashT - dt);
        shakeT = Mathf.Max(0f, shakeT - dt);
        Vector2 jolt = shakeT > 0f ? Random.insideUnitCircle * shakeAmp * (shakeT / ShakeTime) : Vector2.zero;
        rect.anchoredPosition = (Vector2)screen / Canvas().scaleFactor + jolt;
        rect.localScale = Vector3.one * (1f + 0.2f * flashT / FlashTime);

        if (Time.time >= chipHoldUntil) chipFrac = Mathf.MoveTowards(chipFrac, frac, ChipDrainSpeed * dt);
        chipFrac = Mathf.Max(chipFrac, frac);
        fill.sizeDelta = new Vector2(width * frac, Height);
        chip.sizeDelta = new Vector2(width * chipFrac, Height);

        // nearly broken: warm and pulsing
        fillImage.color = frac <= 0.3f
            ? Color.Lerp(LowColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 12f))
            : FillColor;
        flashImage.color = new Color(1f, 1f, 1f, 0.9f * flashT / FlashTime);
        group.alpha = alpha;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        bars.Clear();
        canvas = null;
    }
}
