using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen text for the big moments: the round banner ("ROUND 2 / FIGHT!"), announcer
/// callouts (FIRST BLOOD, DOUBLE KILL...), and the results board between maps. Built at
/// runtime on its own overlay canvas, one per scene. Animates on unscaled time so slow-mo
/// and hitstop don't stall it.
/// </summary>
[AddComponentMenu("")]
public class FeelHud : MonoBehaviour
{
    static FeelHud instance;

    RectTransform root;
    TMP_FontAsset font;

    class Pop
    {
        public RectTransform rt; public TextMeshProUGUI text, sub; public CanvasGroup group;
        public float born, hold, size; public bool banner;
    }
    readonly List<Pop> pops = new();

    CanvasGroup board;
    TextMeshProUGUI boardTitle, boardRows, boardFooter;
    Image dim;
    float boardShownAt = -1f;

    public static FeelHud Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("[FeelHud]");
        instance = go.AddComponent<FeelHud>();
        instance.Build();
        return instance;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Build()
    {
        font = TMP_Settings.defaultFontAsset;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;   // over the HUD, under the pause menu
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)transform;
    }

    // ---------- Banners and callouts ----------

    /// Big centered title, e.g. "ROUND 2" with "FIRST TO 3" under it
    public static void Banner(string title, Color color, string subtitle = null, float hold = 1.3f, float size = 1f)
        => Get().AddPop(title, color, subtitle, hold, size, banner: true);

    /// Announcer callout across the top: FIRST BLOOD, DOUBLE KILL...
    public static void Callout(string text, Color color, string subtitle = null)
        => Get().AddPop(text, color, subtitle, 1.1f, 0.62f, banner: false);

    void AddPop(string title, Color color, string subtitle, float hold, float size, bool banner)
    {
        if (font == null) return;

        // a new banner replaces the old one; callouts stack downward
        if (banner)
            foreach (var p in pops) if (p.banner) p.hold = Mathf.Min(p.hold, Time.unscaledTime - p.born);

        var go = new GameObject(banner ? "Banner" : "Callout", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, banner ? 0.62f : 0.86f);
        rt.sizeDelta = new Vector2(1600f, 260f);

        var pop = new Pop
        {
            rt = rt, group = go.GetComponent<CanvasGroup>(), born = Time.unscaledTime,
            hold = hold, size = size, banner = banner,
            text = Text(rt, title, 150f * size, color, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 20f)),
        };
        if (!string.IsNullOrEmpty(subtitle))
            pop.sub = Text(rt, subtitle, 46f * Mathf.Max(0.8f, size), Color.white, FontStyles.Bold, new Vector2(0f, -85f * size));
        pop.text.characterSpacing = 6f;

        if (!banner)
        {
            int stacked = 0;
            foreach (var p in pops) if (!p.banner) stacked++;
            rt.anchoredPosition = new Vector2(0f, -stacked * 110f);
        }
        pops.Add(pop);
        Animate(pop, Time.unscaledTime);
    }

    TextMeshProUGUI Text(RectTransform parent, string s, float size, Color color, FontStyles style, Vector2 at)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1800f, size * 1.4f);
        rt.anchoredPosition = at;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.22f;
        t.outlineColor = new Color(0.05f, 0.03f, 0.08f, 1f);
        return t;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            if (p.rt == null || now - p.born > p.hold + 0.4f)
            {
                if (p.rt != null) Destroy(p.rt.gameObject);
                pops.RemoveAt(i);
                continue;
            }
            Animate(p, now);
        }

        if (board != null && boardShownAt >= 0f)
        {
            float t = Mathf.Clamp01((now - boardShownAt) / 0.35f);
            board.alpha = t;
            board.transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, Ease(t));
        }
    }

    // Slam in big, settle, hold, then lift and fade
    static void Animate(Pop p, float now)
    {
        float t = now - p.born;
        float punch = Mathf.Clamp01(t / 0.16f);
        float scale = Mathf.Lerp(1.9f, 1f, Ease(punch));
        if (punch >= 1f) scale = 1f + 0.04f * Mathf.Exp(-(t - 0.16f) * 10f) * Mathf.Sin((t - 0.16f) * 40f);
        float fade = t > p.hold ? Mathf.Clamp01((t - p.hold) / 0.4f) : 0f;
        p.rt.localScale = Vector3.one * scale * (1f + fade * 0.15f);
        p.group.alpha = Mathf.Min(punch * 3f, 1f) * (1f - fade);
        if (p.sub != null) p.sub.alpha = Mathf.Clamp01((t - 0.15f) / 0.2f);
    }

    static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // ---------- Results board ----------

    public struct Row
    {
        public string name; public Color color; public int wins, kills; public bool roundWinner;
    }

    public static void ShowBoard(string title, Color titleColor, IList<Row> rows, int winsNeeded, string footer)
        => Get().Board(title, titleColor, rows, winsNeeded, footer);

    public static void SetBoardFooter(string footer)
    {
        if (instance != null && instance.boardFooter != null) instance.boardFooter.text = footer;
    }

    void Board(string title, Color titleColor, IList<Row> rows, int winsNeeded, string footer)
    {
        if (font == null) return;
        if (board == null)
        {
            var dimGo = new GameObject("Dim", typeof(RectTransform));
            var drt = (RectTransform)dimGo.transform;
            drt.SetParent(root, false);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.01f, 0.05f, 0.62f);
            dim.raycastTarget = false;

            var go = new GameObject("Board", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, 800f);
            board = go.GetComponent<CanvasGroup>();
            boardTitle = Text(rt, "", 110f, Color.white, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 270f));
            boardRows = Text(rt, "", 58f, Color.white, FontStyles.Bold, new Vector2(0f, -10f));
            boardRows.rectTransform.sizeDelta = new Vector2(1400f, 420f);
            boardRows.alignment = TextAlignmentOptions.Center;
            boardRows.lineSpacing = 18f;
            boardRows.richText = true;
            boardFooter = Text(rt, "", 40f, new Color(1f, 1f, 1f, 0.8f), FontStyles.Bold, new Vector2(0f, -290f));
        }
        dim.gameObject.SetActive(true);
        board.gameObject.SetActive(true);
        board.alpha = 0f;
        boardShownAt = Time.unscaledTime;

        boardTitle.text = title;
        boardTitle.color = titleColor;
        boardFooter.text = footer;

        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            string hex = ColorUtility.ToHtmlStringRGB(r.color);
            sb.Append(r.roundWinner ? "<size=115%>" : "<size=100%>");
            sb.Append($"<color=#{hex}>{r.name}</color>   ");
            for (int i = 0; i < winsNeeded; i++)
                sb.Append(i < r.wins ? $"<size=170%><color=#{hex}>•</color></size>" : "<size=170%><color=#FFFFFF38>•</color></size>");
            sb.Append($"  <size=70%><color=#FFFFFFB0>{r.kills} {(r.kills == 1 ? "kill" : "kills")}</color></size>");
            sb.Append("</size>\n");
        }
        boardRows.text = sb.ToString();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;
}
