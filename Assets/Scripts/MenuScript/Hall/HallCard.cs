using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A player's card in their corner of the screen (P1 top-left, P2 top-right, P3
/// bottom-left, P4 bottom-right), next to where their wizard stands in the hall. Shows
/// what the station's current step needs: wizard + runes (or lore), the armory's browsed
/// gun with stats beside the d-pad loadout, or the ready summary. Rebuilt on every change;
/// kept under MaxHeight so a top and a bottom card never meet.
/// </summary>
public class HallCard
{
    const float Width = 470f;
    const float Pad = 16f;
    const float Margin = 22f;
    const float Inner = Width - Pad * 2f;
    const float EmblemSize = 96f;
    public const float MaxHeight = 500f;

    readonly HallStation st;
    readonly HallCatalog cat;
    readonly RectTransform root;
    readonly Image frame;
    readonly RectTransform body;
    readonly Image header;
    readonly Text tag;
    readonly HallUI.Prompts prompts;
    readonly List<Text> texts = new();
    readonly List<Image> images = new();
    int textUsed, imageUsed;

    public HallCard(Transform canvas, HallStation station)
    {
        st = station;
        cat = station.Catalog;
        int i = station.index;
        bool bottom = i >= 2;
        var anchor = new Vector2(i % 2 == 0 ? 0f : 1f, bottom ? 0f : 1f);
        root = HallUI.Panel(canvas, $"Card P{i + 1}", st.PlayerColor, out frame, out _);
        HallUI.Corner(root, anchor, new Vector2(Margin, Margin), new Vector2(Width, 200f));

        body = HallUI.Rect(root, "Body");
        body.anchorMin = body.anchorMax = new Vector2(0f, 1f);
        body.pivot = new Vector2(0f, 1f);
        body.anchoredPosition = Vector2.zero;
        body.sizeDelta = new Vector2(Width, 10f);

        header = HallUI.Box(body, "Header", st.PlayerColor);
        tag = HallUI.Label(header.rectTransform, "Tag", cat.titleFont, 4, new Color(0.08f, 0.06f, 0.1f));
        prompts = new HallUI.Prompts(body, cat.bodyFont, 2);
    }

    // ---------- pooled parts ----------

    Text T(Font font, int scale, Color color)
    {
        if (textUsed >= texts.Count) texts.Add(HallUI.Label(body, "Line", font, scale, color));
        var t = texts[textUsed++];
        t.font = font;
        t.rectTransform.localScale = Vector3.one * scale;
        t.color = color;
        t.alignment = TextAnchor.UpperLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.gameObject.SetActive(true);
        return t;
    }

    Image I(Sprite sprite, Color color)
    {
        if (imageUsed >= images.Count) images.Add(HallUI.Icon(body, "Img", null, 10f));
        var img = images[imageUsed++];
        img.sprite = sprite;
        img.preserveAspect = sprite != null;
        img.color = color;
        img.gameObject.SetActive(true);
        return img;
    }

    float Line(string text, float y, Font font = null, int scale = 2, Color? color = null, float x = Pad, float width = Inner)
        => HallUI.Set(T(font ?? cat.bodyFont, scale, color ?? HallUI.Ink), text, x, y, width);

    /// One-line label that may run past its width (tabs, names)
    float Word(string text, float x, float y, Font font, int scale, Color color, out float width)
    {
        var t = T(font, scale, color);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        float h = HallUI.Set(t, text, x, y, 400f);
        width = HallUI.Measure(t.font, t.text) * scale;
        return h;
    }

    // ---------- refresh ----------

    public void Refresh()
    {
        textUsed = imageUsed = 0;
        var c = st.PlayerColor;
        frame.color = st.Joined ? c : Color.Lerp(c, new Color(0.2f, 0.2f, 0.25f), 0.55f);

        float y = st.Joined ? Header() : Unjoined();
        switch (st.CurrentStage)
        {
            case HallStation.Stage.Wizard: y = WizardPage(y); break;
            case HallStation.Stage.Armory: y = ArmoryPage(y); break;
            case HallStation.Stage.Ready: y = ReadyPage(y); break;
        }

        for (int i = textUsed; i < texts.Count; i++) texts[i].gameObject.SetActive(false);
        for (int i = imageUsed; i < images.Count; i++) images[i].gameObject.SetActive(false);

        float h = Mathf.Min(MaxHeight, y + Pad * 0.5f);
        root.sizeDelta = new Vector2(Width, h);
        body.sizeDelta = new Vector2(Width, h);
    }

    float Unjoined()
    {
        HallUI.Place(header.rectTransform, 3f, 3f, 76f, 52f);
        header.color = new Color(st.PlayerColor.r, st.PlayerColor.g, st.PlayerColor.b, 0.45f);
        HallUI.Set(tag, $"P{st.index + 1}", 10f, 8f, 70f);
        tag.color = HallUI.Ink;

        prompts.Root.gameObject.SetActive(true);
        prompts.Root.localScale = Vector3.one;
        prompts.Begin();
        prompts.Add(cat.btnX, "Press to join", HallUI.Ink);
        float w = prompts.End();
        HallUI.Place(prompts.Root, 100f, 14f, w, 30f);
        return 56f;
    }

    float Header()
    {
        var w = st.Wizard;
        header.color = st.PlayerColor;
        HallUI.Place(header.rectTransform, 3f, 3f, 70f, 70f);
        HallUI.Set(tag, $"P{st.index + 1}", 8f, 14f, 62f);
        tag.color = new Color(0.08f, 0.06f, 0.1f);

        Word(w != null ? w.wizardName.ToUpperInvariant() : "", 88f, 10f, cat.titleFont, 3, st.Theme, out _);
        if (w != null)
        {
            // lives as hearts, then the orb budget
            for (int i = 0; i < w.heartCount; i++)
            {
                var h = I(cat.heart, Color.white);
                HallUI.Place(h.rectTransform, 88f + i * 24f, 44f, 22f, 22f);
            }
            var o = I(cat.orb, Color.white);
            float ox = 88f + w.heartCount * 24f + 14f;
            HallUI.Place(o.rectTransform, ox, 44f, 22f, 22f);
            Word($"{w.loadoutOrbs} orbs", ox + 28f, 47f, cat.bodyFont, 2, HallUI.Dim, out _);
            if (w.factionEmblem != null)
            {
                var e = I(w.factionEmblem, Color.white);
                HallUI.Place(e.rectTransform, Width - Pad - EmblemSize + 4f, 8f, EmblemSize, EmblemSize);
            }
        }
        prompts.Root.gameObject.SetActive(false);
        return EmblemSize + 16f;
    }

    // ---------- wizard ----------

    float WizardPage(float y)
    {
        var w = st.Wizard;
        var runes = RuneBook.For(w);
        Color theme = st.Theme;

        if (st.ShowLore)
        {
            y += Line(HallUI.Tint("LORE", theme), y, cat.titleFont, 2) + 8f;
            y += Line(w != null ? w.loreText : "", y) + 10f;
        }
        else if (runes != null)
        {
            var a = runes.actives[st.ActiveRune];
            var p = runes.passives[st.PassiveRune];
            y = Section(y, $"ACTIVE  {RuneBook.Numeral(st.ActiveRune)}/{RuneBook.Numeral(RuneBook.ActiveCount - 1)}", "D-pad up/down",
                        $"{a.name}  {HallUI.Tint($"{a.cooldown:0}s", HallUI.Dim)}", a.description, theme);
            y = Section(y, $"PASSIVE  {RuneBook.Numeral(st.PassiveRune)}/{RuneBook.Numeral(RuneBook.PassiveCount - 1)}", "D-pad left/right",
                        p.name, p.description, theme);
            string hint = CompHint();
            if (!string.IsNullOrEmpty(hint)) y += Line(hint.Trim(), y, color: HallUI.Dim) + 8f;
        }

        return Footer(y,
            (cat.btnLB, ""), (cat.btnRB, "Wizard"),
            (cat.btnY, st.ShowLore ? "Runes" : "Lore"),
            (cat.btnA, "Armory"), (cat.btnB, "Leave"));
    }

    float Section(float y, string label, string how, string name, string desc, Color theme)
    {
        var g = I(cat.btnDpad, Color.white);
        HallUI.Place(g.rectTransform, Pad, y - 3f, 26f, 26f);
        Word(label, Pad + 34f, y + 2f, cat.titleFont, 2, theme, out float lw);
        Word(how, Pad + 34f + lw + 14f, y + 3f, cat.bodyFont, 2, new Color(1f, 1f, 1f, 0.3f), out _);
        y += 26f;
        y += Line(name, y) + 2f;
        y += Line(desc, y, color: HallUI.Dim) + 12f;
        return y;
    }

    string CompHint()
    {
        var picked = new List<(int, WizardData)>();
        foreach (var other in st.Director.Stations)
            if (other != st && other.Joined && other.Wizard != null) picked.Add((other.index, other.Wizard));
        return ElementComps.Hint(st.Wizard, picked, st.Director.SelectedMode != 0);
    }

    // ---------- armory ----------

    float ArmoryPage(float y)
    {
        Color theme = st.Theme;

        // tier tabs
        var lb = I(cat.btnLB, Color.white); HallUI.Place(lb.rectTransform, Pad, y, 34f, 24f);
        float x = Pad + 42f;
        for (int t = 0; t < cat.TierCount; t++)
        {
            bool on = t == st.TierIndex;
            Word(cat.tierNames[t].ToUpperInvariant(), x, y + 5f, cat.titleFont, 2, on ? theme : HallUI.Dim, out float w);
            if (on) { var u = I(null, theme); HallUI.Place(u.rectTransform, x, y + 26f, w, 3f); }
            x += w + 16f;
        }
        var rb = I(cat.btnRB, Color.white); HallUI.Place(rb.rectTransform, x - 2f, y, 34f, 24f);
        y += 38f;

        var wd = st.Browsed;
        float top = y;
        const float Left = 286f;   // stats column; the loadout diamond sits to its right
        if (wd != null)
        {
            var box = I(null, new Color(1f, 1f, 1f, 0.06f));
            HallUI.Place(box.rectTransform, Pad, y, 60f, 60f);
            var icon = I(wd.weaponIcon, Color.white);
            HallUI.Place(icon.rectTransform, Pad + 4f, y + 4f, 52f, 52f);
            bool favored = RuneBook.Favors(st.Wizard, wd);
            Word(wd.weaponName.ToUpperInvariant(), Pad + 70f, y + 2f, cat.titleFont, 2, theme, out _);
            string cls = wd.weaponClass != WeaponClass.None ? wd.weaponClass.ToString() : "";
            Word(cls + (favored ? "   " + HallUI.Tint("SYNERGY", Color.Lerp(theme, Color.white, 0.4f)) : ""), Pad + 70f, y + 22f, cat.bodyFont, 2, HallUI.Dim, out _);
            for (int i = 0; i < wd.orbCost; i++)
            {
                var o = I(cat.orb, Color.white);
                HallUI.Place(o.rectTransform, Pad + 70f + i * 20f, y + 42f, 18f, 18f);
            }
            y += 70f;

            int perShot = wd.isShotgun ? wd.damage * Mathf.Max(1, wd.pelletCount) : wd.damage;
            y = Bar(y, "DAMAGE", perShot / 75f, theme, Left);
            y = Bar(y, "AMMO", wd.ammoCapacity / 60f, theme, Left);
            y = Bar(y, "RATE", wd.attackSpeed / 15f, theme, Left);
            y = Bar(y, "WEIGHT", wd.weight / 5f, new Color(0.85f, 0.7f, 0.5f), Left);
        }
        Diamond(top + 4f, theme, Pad + Left + 82f);
        y = Mathf.Max(y, top + 156f) + 6f;

        if (wd != null)
        {
            y += Line(wd.description, y, color: HallUI.Dim) + 4f;
            if (RuneBook.Favors(st.Wizard, wd))
            {
                var a = RuneBook.AffinityOf(st.Wizard);
                y += Line(HallUI.Tint($"{a.name}: {a.description}", Color.Lerp(theme, Color.white, 0.35f)), y) + 4f;
            }
        }

        // orbs left
        y += 4f;
        int budget = st.Budget, spent = st.Spent;
        for (int i = 0; i < budget; i++)
        {
            var o = I(cat.orb, i < budget - spent ? Color.white : new Color(1f, 1f, 1f, 0.15f));
            HallUI.Place(o.rectTransform, Pad + i * 22f, y, 20f, 20f);
        }
        Word($"{budget - spent}/{budget} orbs left", Pad + budget * 22f + 10f, y + 2f, cat.bodyFont, 2, HallUI.Dim, out _);
        y += 28f;

        string flash = st.FlashText;
        if (!string.IsNullOrEmpty(flash)) y += Line(HallUI.Tint(flash, new Color(1f, 0.45f, 0.35f)), y) + 4f;

        return Footer(y,
            (cat.btnStick, "Browse"), (cat.btnDpad, "Equip"), (cat.btnX, "+"), (cat.btnDpad, "Clear"),
            (cat.btnA, "Ready"), (cat.btnB, "Back"));
    }

    float Bar(float y, string label, float pct, Color color, float width)
    {
        Word(label, Pad, y + 1f, cat.titleFont, 2, HallUI.Dim, out _);
        float bx = Pad + 92f, bw = width - 92f;
        var back = I(null, new Color(1f, 1f, 1f, 0.08f));
        HallUI.Place(back.rectTransform, bx, y + 3f, bw, 12f);
        var fill = I(null, color);
        HallUI.Place(fill.rectTransform, bx, y + 3f, bw * Mathf.Clamp(pct, 0.04f, 1f), 12f);
        return y + 21f;
    }

    /// The four d-pad slots as a diamond: what's packed on each side of the wizard
    void Diamond(float y, Color theme, float cx)
    {
        float cy = y + 74f, step = 46f, size = 46f;
        Vector2[] at = { new(0, -step), new(step, 0), new(0, step), new(-step, 0) };
        for (int i = 0; i < 4; i++)
        {
            var w = st.Loadout[i];
            var back = I(HallUI.Diamond(), w != null ? new Color(theme.r, theme.g, theme.b, 0.4f) : new Color(1f, 1f, 1f, 0.08f));
            HallUI.Place(back.rectTransform, cx + at[i].x - size * 0.5f, cy + at[i].y - size * 0.5f, size, size);
            if (w != null)
            {
                var icon = I(w.weaponIcon, Color.white);
                HallUI.Place(icon.rectTransform, cx + at[i].x - 18f, cy + at[i].y - 18f, 36f, 36f);
            }
        }
        var d = I(cat.btnDpad, new Color(1f, 1f, 1f, 0.5f));
        HallUI.Place(d.rectTransform, cx - 13f, cy - 13f, 26f, 26f);
    }

    // ---------- ready ----------

    float ReadyPage(float y)
    {
        Color theme = st.Theme;
        Word("READY!", Pad, y, cat.titleFont, 4, theme, out _);
        var runes = RuneBook.For(st.Wizard);
        if (runes != null)
            Line($"{runes.actives[st.ActiveRune].name}  /  {runes.passives[st.PassiveRune].name}", y + 48f, color: HallUI.Dim);
        y += 76f;

        float x = Pad;
        bool any = false;
        for (int i = 0; i < 4; i++)
        {
            var w = st.Loadout[i];
            if (w == null) continue;
            any = true;
            var back = I(HallUI.Diamond(), new Color(theme.r, theme.g, theme.b, 0.3f));
            HallUI.Place(back.rectTransform, x, y, 50f, 50f);
            var icon = I(w.weaponIcon, Color.white);
            HallUI.Place(icon.rectTransform, x + 6f, y + 6f, 38f, 38f);
            x += 58f;
        }
        if (!any) Line(HallUI.Tint("Empty-handed. Bold.", HallUI.Dim), y + 14f);
        y += 60f;
        return Footer(y, (cat.btnB, "Change loadout"));
    }

    float Footer(float y, params (Sprite glyph, string label)[] items)
    {
        var rule = I(null, new Color(1f, 1f, 1f, 0.08f));
        HallUI.Place(rule.rectTransform, Pad, y, Inner, 2f);
        y += 9f;
        prompts.Root.gameObject.SetActive(true);
        prompts.Root.localScale = Vector3.one;
        prompts.Begin();
        foreach (var (g, l) in items) prompts.Add(g, l, HallUI.Ink);
        float w = prompts.End(12f);
        float k = Mathf.Min(1f, Inner / Mathf.Max(1f, w));
        HallUI.Place(prompts.Root, Pad, y, w, 30f);
        prompts.Root.localScale = Vector3.one * k;
        return y + 30f * k + 4f;
    }
}
