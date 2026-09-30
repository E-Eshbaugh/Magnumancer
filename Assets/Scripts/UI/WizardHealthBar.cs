using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small health bar floating over a wizard: the health left in their current life, in
/// their theme color. Hits make it flash white, jolt and punch in size, with the lost
/// chunk lingering a moment before draining away. It settles to half-transparent while
/// they're at full health so it stays out of the way, and pulses when they're low.
/// </summary>
public class WizardHealthBar : MonoBehaviour
{
    [Header("Layout (reference pixels at 1080p)")]
    public float width = 70f;
    public float height = 8f;
    public float border = 2f;
    [Tooltip("World units above the top of the wizard")]
    public float headroom = 0.45f;

    [Header("Feel")]
    public float chipDelay = 0.35f;     // lost chunk hangs this long...
    public float chipDrainSpeed = 1.2f; // ...then drains (fraction of bar per second)
    public float flashTime = 0.15f;
    public float shakeTime = 0.25f;
    public float lowHealth = 0.3f;
    [Tooltip("Opacity when at full health and not recently hit")]
    public float idleAlpha = 0.45f;

    PlayerHealthControl health;
    CharacterController body;
    Color color;

    RectTransform root, fill, chip;
    Image fillImage, flash;
    CanvasGroup group;

    float frac = 1f, chipFrac = 1f, chipHoldUntil;
    float flashT, shakeT, shakeAmp, lastChange = -99f;
    bool built;

    static Canvas canvas;

    public static WizardHealthBar AddTo(PlayerHealthControl health, WizardData wizard)
    {
        if (health == null) return null;
        var go = new GameObject($"HealthBar ({health.name})", typeof(RectTransform));
        go.transform.SetParent(Canvas().transform, false);
        var bar = go.AddComponent<WizardHealthBar>();
        bar.health = health;
        bar.body = health.GetComponent<CharacterController>();
        bar.color = GlowLine.Brighten(WizardSpawnEffect.ThemeColorOf(wizard));
        health.OnHealthChanged += bar.HandleHealthChanged;
        return bar;
    }

    // One overlay canvas holds every wizard's bar
    static Canvas Canvas()
    {
        if (canvas != null) return canvas;
        var go = new GameObject("WizardHealthBars");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    void Start()
    {
        Build();
        if (health != null) frac = chipFrac = Fraction(health.currentHealth, health.maxHealth);
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
    }

    void Build()
    {
        root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(width + border * 2f, height + border * 2f);
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;

        Box("Frame", root, new Color(0f, 0f, 0f, 0.6f), root.sizeDelta, false);
        Box("Back", root, new Color(0.12f, 0.12f, 0.14f, 0.8f), new Vector2(width, height), false);
        chip = Box("Chip", root, new Color(1f, 0.92f, 0.75f, 0.9f), new Vector2(width, height), true).rectTransform;
        fillImage = Box("Fill", root, color, new Vector2(width, height), true);
        fill = fillImage.rectTransform;

        // quarter ticks make chunks of damage easy to read
        for (int i = 1; i < 4; i++)
        {
            var tick = Box("Tick", root, new Color(0f, 0f, 0f, 0.45f), new Vector2(1.5f, height), false);
            tick.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + width * i / 4f, 0f);
        }

        flash = Box("Flash", root, new Color(1f, 1f, 1f, 0f), new Vector2(width, height), false);
        built = true;
    }

    Image Box(string name, RectTransform parent, Color c, Vector2 size, bool leftAligned)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = leftAligned ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = leftAligned ? new Vector2(-width * 0.5f, 0f) : Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void HandleHealthChanged(float current, float max)
    {
        float next = Fraction(current, max);
        if (next < frac - 1e-4f)
        {
            // hit: flash, jolt harder for bigger chunks, lost part lingers then drains
            float lost = frac - next;
            flashT = flashTime;
            shakeT = shakeTime;
            shakeAmp = Mathf.Clamp(3f + lost * 30f, 3f, 10f);
            chipFrac = Mathf.Max(chipFrac, frac);
            chipHoldUntil = Time.time + chipDelay;
            if (group != null && group.alpha > 0f) group.alpha = 1f;
        }
        else if (next > frac + 0.5f)
        {
            // new life: straight back to full
            chipFrac = next;
            flashT = flashTime;
        }
        frac = next;
        lastChange = Time.time;
    }

    static float Fraction(float current, float max) => max > 0f ? Mathf.Clamp01(current / max) : 0f;

    void LateUpdate()
    {
        if (health == null) { Destroy(gameObject); return; }
        if (!built) return;

        var cam = Camera.main;
        bool show = cam != null && health.isActiveAndEnabled && !health.IsDead && !GamePause.IsPaused
                    && !WizardSpawnEffect.IsArriving(health.transform);

        Vector3 screen = Vector3.zero;
        if (show)
        {
            screen = cam.WorldToScreenPoint(HeadPoint());
            show = screen.z > 0f;
        }
        group.alpha = show ? group.alpha : 0f;
        if (!show) return;

        float dt = Time.deltaTime;
        flashT = Mathf.Max(0f, flashT - dt);
        shakeT = Mathf.Max(0f, shakeT - dt);

        // position (plus jolt)
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 pos = new Vector2(screen.x, screen.y) / scale;
        if (shakeT > 0f)
            pos += Random.insideUnitCircle * shakeAmp * (shakeT / shakeTime);
        root.anchoredPosition = pos;
        float punch = flashT > 0f ? 1f + 0.2f * (flashT / flashTime) : 1f;
        root.localScale = new Vector3(punch, punch, 1f);

        // lost chunk drains after a beat
        if (Time.time >= chipHoldUntil)
            chipFrac = Mathf.MoveTowards(chipFrac, frac, chipDrainSpeed * dt);
        if (chipFrac < frac) chipFrac = frac;
        fill.sizeDelta = new Vector2(width * frac, height);
        chip.sizeDelta = new Vector2(width * chipFrac, height);

        // low health pulses
        Color c = color;
        if (frac <= lowHealth)
            c = Color.Lerp(color, Color.white, 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * 12f)));
        fillImage.color = c;
        flash.color = new Color(1f, 1f, 1f, 0.9f * (flashT / flashTime));

        // fade back when healthy and quiet
        bool calm = frac >= 0.999f && Time.time - lastChange > 2f;
        group.alpha = Mathf.MoveTowards(group.alpha, calm ? idleAlpha : 1f, dt * 3f);
    }

    Vector3 HeadPoint()
    {
        var t = health.transform;
        float top = 2f;
        if (body != null)
            top = (body.center.y + body.height * 0.5f) * t.lossyScale.y;
        return t.position + Vector3.up * (top + headroom);
    }
}
