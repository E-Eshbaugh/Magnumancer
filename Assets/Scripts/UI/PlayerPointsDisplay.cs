using TMPro;
using UnityEngine;

/// <summary>One persistent crest balance and one reusable activity readout per wizard.</summary>
[DisallowMultipleComponent]
public sealed class PlayerPointsDisplay : MonoBehaviour
{
    public const float HoldSeconds = 1.25f;
    public const float FadeSeconds = 0.45f;
    const float HeadHeight = 2.8f;
    static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

    TextMeshProUGUI balance;
    TextMeshPro overhead;
    RectTransform hudPanel;
    Vector2 originalHudPosition;
    Canvas hudCanvas;
    readonly Vector3[] corners = new Vector3[4];
    float changedAt = float.NegativeInfinity;
    long gained, spent;

    public static PlayerPointsDisplay Bind(PlayerHealthControl player, CircleAbilityUI hud)
    {
        var display = player.GetComponent<PlayerPointsDisplay>();
        if (display == null) display = player.gameObject.AddComponent<PlayerPointsDisplay>();
        display.Build(hud);
        return display;
    }

    void OnEnable()
    {
        ZombiesPoints.PointsChanged += OnPointsChanged;
        if (balance != null) balance.text = Format(ZombiesPoints.Get(gameObject));
    }

    void OnDisable()
    {
        ZombiesPoints.PointsChanged -= OnPointsChanged;
        if (overhead != null) overhead.gameObject.SetActive(false);
        changedAt = float.NegativeInfinity;
    }

    void OnDestroy()
    {
        if (balance != null) Destroy(balance.gameObject);
        if (hudPanel != null) hudPanel.anchoredPosition = originalHudPosition;
    }

    void Build(CircleAbilityUI hud)
    {
        if (TMP_Settings.defaultFontAsset == null) return;
        if (balance == null && hud != null && hud.crestImage != null)
        {
            hudPanel = hud.transform as RectTransform;
            if (hudPanel != null) originalHudPosition = hudPanel.anchoredPosition;
            hudCanvas = hud.GetComponentInParent<Canvas>();
            var go = new GameObject("PointsBalance", typeof(RectTransform));
            go.transform.SetParent(hud.crestImage.transform, false);
            balance = go.AddComponent<TextMeshProUGUI>();
            Style(balance);
            balance.fontSize = 40f;
            balance.enableAutoSizing = true;
            balance.fontSizeMin = 24f;
            balance.fontSizeMax = 40f;
            balance.raycastTarget = false;
            balance.color = Gold;
            var rect = balance.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            // The legacy life icon sits immediately below the top crests.
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(230f, 52f);
        }
        if (overhead == null)
        {
            var go = new GameObject("PointsActivity");
            go.transform.SetParent(transform, false);
            overhead = go.AddComponent<TextMeshPro>();
            Style(overhead);
            overhead.fontSize = 8f;
            overhead.sortingOrder = 56;
            overhead.rectTransform.sizeDelta = new Vector2(12f, 2.5f);
            go.SetActive(false);
        }
        if (balance != null) balance.text = Format(ZombiesPoints.Get(gameObject));
    }

    static void Style(TMP_Text label)
    {
        label.font = TMP_Settings.defaultFontAsset;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(16, 12, 24, 255);
    }

    static string Format(long amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    void OnPointsChanged(GameObject player, int total, int delta)
    {
        if (player != gameObject || delta == 0) return;
        if (balance != null) balance.text = Format(total);
        if (overhead == null) return;
        if (Time.time - changedAt >= HoldSeconds + FadeSeconds) gained = spent = 0;
        if (delta > 0) gained += delta;
        else spent -= (long)delta;
        // Keep credits and debits separate so a purchase cannot disappear in a net gain.
        string activity = gained > 0 ? $"<color=#FFE080>+{Format(gained)}</color>" : "";
        if (spent > 0) activity += (gained > 0 ? "  " : "") + $"<color=#FF9988>-{Format(spent)}</color>";
        overhead.text = $"{Format(total)}<br><size=70%>{activity}</size>";
        changedAt = Time.time;
        overhead.alpha = 1f;
        overhead.gameObject.SetActive(true);
        LateUpdate();
    }

    void LateUpdate()
    {
        FitHud();
        if (overhead == null || !overhead.gameObject.activeSelf) return;
        float age = Time.time - changedAt;
        if (age >= HoldSeconds + FadeSeconds)
        {
            overhead.gameObject.SetActive(false);
            return;
        }
        float fade = Mathf.Clamp01((age - HoldSeconds) / FadeSeconds);
        overhead.transform.position = transform.position + Vector3.up * (HeadHeight + fade * 0.3f);
        var camera = Camera.main;
        if (camera != null) overhead.transform.rotation = camera.transform.rotation;
        overhead.alpha = 1f - fade;
    }

    void FitHud()
    {
        if (balance == null || hudPanel == null || hudCanvas == null || !balance.gameObject.activeInHierarchy) return;
        // Move the whole lower crest panel together (mask, ammo and life icon), keeping
        // the new balance below it and inside the camera's pixel-sized render target.
        hudPanel.anchoredPosition = originalHudPosition;
        var camera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
        balance.rectTransform.GetWorldCorners(corners);
        Vector3 bottom = camera != null ? camera.WorldToScreenPoint(corners[0]) : corners[0];
        const float padding = 3f;
        if (bottom.y >= padding) return;
        Vector3 shift = camera != null
            ? camera.ScreenToWorldPoint(new Vector3(bottom.x, padding, bottom.z)) - camera.ScreenToWorldPoint(bottom)
            : Vector3.up * (padding - bottom.y);
        hudPanel.position += shift;
    }
}
