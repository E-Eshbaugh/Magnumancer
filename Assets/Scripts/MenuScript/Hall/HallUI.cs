using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small kit for the Great Hall's screen UI, built in code: framed dark panels, pixel-font
/// labels and button-glyph prompts. The fonts are bitmap fonts (they ignore fontSize), so
/// labels are sized by an integer transform scale to stay crisp; positions and widths are
/// given in screen pixels of the 1920x1080 reference canvas.
/// </summary>
public static class HallUI
{
    public static readonly Color Ink = new Color(0.94f, 0.91f, 0.86f);
    public static readonly Color Dim = new Color(0.64f, 0.62f, 0.7f);
    public static readonly Color PanelFill = new Color(0.055f, 0.045f, 0.085f, 0.9f);
    public static readonly Color PanelInner = new Color(0.1f, 0.085f, 0.15f, 0.9f);

    public static Canvas Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        c.pixelPerfect = false;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        return c;
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// Rect anchored to one corner of its parent (anchor 0/1 per axis), at offset px
    public static RectTransform Corner(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(anchor.x < 0.5f ? offset.x : -offset.x, anchor.y < 0.5f ? offset.y : -offset.y);
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// Top-left anchored rect inside its parent at (x, y) px down from the top-left
    public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static Image Box(Transform parent, string name, Color color)
    {
        var rt = Rect(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// Dark panel with a coloured frame and a thin inner rule
    public static RectTransform Panel(Transform parent, string name, Color edge, out Image frame, out Image fill)
    {
        frame = Box(parent, name, edge);
        var rt = frame.rectTransform;
        fill = Box(rt, "Fill", PanelFill);
        Stretch(fill.rectTransform, 3f);
        var rule = Box(fill.rectTransform, "Rule", new Color(edge.r, edge.g, edge.b, 0.35f));
        Stretch(rule.rectTransform, 3f);
        var inner = Box(rule.rectTransform, "Inner", PanelFill);
        Stretch(inner.rectTransform, 1f);
        return rt;
    }

    /// A pixel-font label. width is in screen px; the text wraps inside it.
    public static Text Label(Transform parent, string name, Font font, int scale, Color color, TextAnchor align = TextAnchor.UpperLeft)
    {
        var rt = Rect(parent, name);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.lineSpacing = 1.15f;
        rt.localScale = Vector3.one * Mathf.Max(1, scale);
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    /// Places a label at (x, y) px inside its parent with the given width, sets its text
    /// and returns its height in px (so callers can stack labels top to bottom)
    public static float Set(Text t, string text, float x, float y, float width)
    {
        float s = t.rectTransform.localScale.x;
        t.text = text;
        // measure a little narrower than it draws: the layout pass and the renderer can
        // disagree by a glyph at the wrap boundary, and an extra line beats an overlap
        Place(t.rectTransform, x, y, width / s - 6f, 10f);
        float h = string.IsNullOrEmpty(text) ? 0f : t.preferredHeight;
        t.rectTransform.sizeDelta = new Vector2(width / s, h);
        t.gameObject.SetActive(!string.IsNullOrEmpty(text));
        return h * s;
    }

    public static Image Icon(Transform parent, string name, Sprite sprite, float px)
    {
        var img = Box(parent, name, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.rectTransform.sizeDelta = new Vector2(px, px);
        return img;
    }

    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    public static string Tint(string s, Color c) => $"<color=#{Hex(c)}>{s}</color>";

    /// A row of "[glyph] Label" prompts, laid out left to right from (x, y)
    public class Prompts
    {
        readonly RectTransform root;
        readonly Font font;
        readonly int scale;
        int used;
        readonly System.Collections.Generic.List<(Image icon, Text text)> items = new();

        public Prompts(Transform parent, Font font, int scale = 2)
        {
            root = Rect(parent, "Prompts");
            this.font = font;
            this.scale = scale;
        }

        public RectTransform Root => root;

        public void Begin() => used = 0;

        public void Add(Sprite glyph, string label, Color color)
        {
            if (used >= items.Count)
            {
                var icon = Icon(root, "Glyph", null, 30f);
                var text = Label(root, "Text", font, scale, color);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                items.Add((icon, text));
            }
            var it = items[used++];
            it.icon.sprite = glyph;
            it.icon.enabled = glyph != null;
            it.text.text = label;
            it.text.color = color;
            it.icon.gameObject.SetActive(true);
            it.text.gameObject.SetActive(true);
        }

        /// Hides unused items and lays the row out; returns its width in px
        public float End(float gap = 18f, bool center = false)
        {
            for (int i = used; i < items.Count; i++) { items[i].icon.gameObject.SetActive(false); items[i].text.gameObject.SetActive(false); }
            float x = 0f;
            for (int i = 0; i < used; i++)
            {
                var (icon, text) = items[i];
                float s = text.rectTransform.localScale.x;
                if (icon.enabled)
                {
                    Place(icon.rectTransform, x, 0f, 30f, 30f);
                    x += 34f;
                }
                float w = text.preferredWidth * s;
                Place(text.rectTransform, x, 6f, w / s + 2f, 12f);
                x += w + gap;
            }
            float width = Mathf.Max(0f, x - gap);
            if (center) root.anchoredPosition = new Vector2(-width * 0.5f, root.anchoredPosition.y);
            root.sizeDelta = new Vector2(width, 30f);
            return width;
        }
    }

    // ---------- tiny procedural sprites ----------

    static Sprite diamond, softCircle;

    /// Filled diamond (loadout slot backs, bullets)
    public static Sprite Diamond()
    {
        if (diamond != null) return diamond;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "HallDiamond" };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x - (n - 1) * 0.5f) + Mathf.Abs(y - (n - 1) * 0.5f);
                px[y * n + x] = d <= n * 0.5f - 0.5f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        return diamond = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f);
    }

    /// Soft round glow (vignettes, highlights)
    public static Sprite SoftCircle()
    {
        if (softCircle != null) return softCircle;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HallSoftCircle" };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n * 0.5f) / (n * 0.5f), dy = (y - n * 0.5f) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return softCircle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
