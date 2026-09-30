using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The big word that pops where a reaction goes off (CONDUCT!, SHATTER!...), shaded
/// across the word from one element's color to the other's, with a dark outline so it
/// reads on ice and lava alike. Punches in, drifts up, fades. World-space, no HUD.
/// </summary>
public class ReactionPopup : MonoBehaviour
{
    const float Life = 1.1f;
    const float PunchTime = 0.14f;
    const float Rise = 1.1f;
    const float FadeTime = 0.35f;
    const float FontSize = 11f;

    TextMeshPro tmp;
    Color a, b;
    Vector3 start;
    float born, size;

    static readonly List<ReactionPopup> live = new();
    static Material material;

    public static void Show(string word, Color colorA, Color colorB, Vector3 at, float scale = 1f)
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null) return;

        // don't stack words on top of each other when reactions go off together
        foreach (var p in live)
            if (p != null && Vector3.Distance(Flat(p.start), Flat(at)) < 2.5f && Mathf.Abs(p.start.y - at.y) < 1f)
                at.y += 1f;

        var go = new GameObject("ReactionPopup");
        go.transform.position = at;
        var popup = go.AddComponent<ReactionPopup>();
        popup.Init(word, font, colorA, colorB, at, scale);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void Init(string word, TMP_FontAsset font, Color colorA, Color colorB, Vector3 at, float scale)
    {
        a = colorA; b = colorB; start = at; size = scale; born = Time.time;

        tmp = gameObject.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = Material(font);
        tmp.text = word;
        tmp.fontSize = FontSize;
        tmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.characterSpacing = 4f;
        tmp.rectTransform.sizeDelta = new Vector2(20f, 4f);
        tmp.sortingOrder = 50;   // over bullets, zones and smoke
        tmp.color = Color.Lerp(colorA, colorB, 0.5f);   // if TMP ever rebuilds the mesh mid-flight
        tmp.ForceMeshUpdate();

        live.Add(this);
        Update();
    }

    // One shared outlined material: dark rim, a touch of dilation for chunky letters
    static Material Material(TMP_FontAsset font)
    {
        if (material != null) return material;
        material = new Material(font.material) { name = "ReactionPopup" };
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.25f);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.03f, 0.08f, 1f));
        material.renderQueue = 3500;
        return material;
    }

    void Update()
    {
        float age = Time.time - born;
        if (age >= Life) { Destroy(gameObject); return; }

        // punch in past full size, settle, then drift up
        float punch = age < PunchTime
            ? Mathf.Lerp(0.2f, 1.35f, age / PunchTime)
            : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((age - PunchTime) / 0.12f));
        float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / Life), 2f);
        transform.position = start + Vector3.up * Rise * rise;
        transform.localScale = Vector3.one * punch * size;

        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;

        float alpha = 1f - Mathf.Clamp01((age - (Life - FadeTime)) / FadeTime);
        Paint(alpha);
    }

    // Shade each vertex by where it sits across the whole word: colorA → colorB
    void Paint(float alpha)
    {
        var info = tmp.textInfo;
        if (info == null || info.meshInfo == null || info.meshInfo.Length == 0) return;
        var bounds = tmp.textBounds;
        float min = bounds.min.x, width = Mathf.Max(0.01f, bounds.size.x);

        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            var verts = info.meshInfo[m].vertices;
            var cols = info.meshInfo[m].colors32;
            if (verts == null || cols == null) continue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible || ch.materialReferenceIndex != m) continue;
                for (int v = 0; v < 4; v++)
                {
                    int idx = ch.vertexIndex + v;
                    float t = Mathf.Clamp01((verts[idx].x - min) / width);
                    Color c = Color.Lerp(a, b, t);
                    // top edge a little hotter
                    if (v == 1 || v == 2) c = Color.Lerp(c, Color.white, 0.35f);
                    c.a = alpha;
                    cols[idx] = c;
                }
            }
        }
        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    void OnDestroy() => live.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        live.Clear();
        material = null;
    }
}
