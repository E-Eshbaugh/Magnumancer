using TMPro;
using UnityEngine;

/// <summary>
/// The word that pops over a player who grabs an item: slams in oversized, bounces and
/// wobbles to rest, letters popping in one after another and riding a little wave, a white
/// shimmer sweeping across colors running from the item's color to white (gold for rare).
/// Follows the player while it's up, then drifts and fades.
/// </summary>
public class ItemGetText : MonoBehaviour
{
    const float Life = 1.8f;
    const float FadeTime = 0.4f;
    const float FontSize = 12f;
    const float OverHead = 4.1f;

    TextMeshPro tmp;
    Color a, b;
    GameObject follow;
    Vector3 anchor;
    float born, size;
    Vector3[][] baseVerts;

    static Material material;

    public static void Show(string word, Color colorA, Color colorB, GameObject follow, float scale = 1f)
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null) return;
        var go = new GameObject("ItemGetText");
        var t = go.AddComponent<ItemGetText>();
        t.Init(word, font, colorA, colorB, follow, scale);
    }

    void Init(string word, TMP_FontAsset font, Color colorA, Color colorB, GameObject target, float scale)
    {
        a = colorA; b = colorB; follow = target; size = scale; born = Time.time;
        anchor = target != null ? target.transform.position + Vector3.up * OverHead : transform.position;
        transform.position = anchor;

        tmp = gameObject.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = Material(font);
        tmp.text = word;
        tmp.fontSize = FontSize;
        tmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.characterSpacing = 6f;
        tmp.rectTransform.sizeDelta = new Vector2(24f, 5f);
        tmp.sortingOrder = 55;
        tmp.color = a;
        tmp.ForceMeshUpdate();

        var info = tmp.textInfo;
        baseVerts = new Vector3[info.meshInfo.Length][];
        for (int m = 0; m < info.meshInfo.Length; m++)
            baseVerts[m] = info.meshInfo[m].vertices != null ? (Vector3[])info.meshInfo[m].vertices.Clone() : null;

        Update();
    }

    static Material Material(TMP_FontAsset font)
    {
        if (material != null) return material;
        material = new Material(font.material) { name = "ItemGetText" };
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.3f);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.02f, 0.07f, 1f));
        material.renderQueue = 3500;
        return material;
    }

    void Update()
    {
        float age = Time.time - born;
        if (age >= Life) { Destroy(gameObject); return; }

        // sticks with the player while it's fresh, then lets go and drifts up
        if (follow != null && follow.activeInHierarchy && age < Life - FadeTime)
            anchor = follow.transform.position + Vector3.up * OverHead;
        transform.position = anchor + Vector3.up * 0.6f * (age / Life);

        // slam in oversized, overshoot small, bounce, settle
        float s;
        if (age < 0.09f) s = Mathf.Lerp(2.8f, 0.8f, age / 0.09f);
        else if (age < 0.2f) s = Mathf.Lerp(0.8f, 1.18f, (age - 0.09f) / 0.11f);
        else s = Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((age - 0.2f) / 0.15f));
        transform.localScale = Vector3.one * s * size;

        float wobble = 16f * Mathf.Sin(age * 30f) * Mathf.Exp(-age * 5f);
        var cam = Camera.main;
        transform.rotation = (cam != null ? cam.transform.rotation : Quaternion.identity) * Quaternion.Euler(0f, 0f, wobble);

        float alpha = 1f - Mathf.Clamp01((age - (Life - FadeTime)) / FadeTime);
        Animate(age, alpha);
    }

    // Per letter: pop in one after another, ride a wave, shimmer
    void Animate(float age, float alpha)
    {
        var info = tmp.textInfo;
        if (info == null || baseVerts == null) return;
        var bounds = tmp.textBounds;
        float min = bounds.min.x, width = Mathf.Max(0.01f, bounds.size.x);
        float band = Mathf.Repeat(age / 0.55f, 1f) * 1.6f - 0.3f;   // shimmer sweeps left to right

        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            int m = ch.materialReferenceIndex;
            var src = baseVerts[m];
            var verts = info.meshInfo[m].vertices;
            var cols = info.meshInfo[m].colors32;
            if (src == null || verts == null || cols == null) continue;

            int v0 = ch.vertexIndex;
            Vector3 center = (src[v0] + src[v0 + 2]) * 0.5f;
            float pop = Mathf.Clamp01((age - i * 0.03f) / 0.1f);
            float popScale = pop < 1f ? Mathf.Lerp(0f, 1.35f, pop) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((age - i * 0.03f - 0.1f) / 0.1f));
            float wave = Mathf.Sin(age * 11f - i * 0.65f) * 0.18f;

            float along = Mathf.Clamp01((center.x - min) / width);
            Color c = Color.Lerp(a, b, along);
            c = Color.Lerp(c, Color.white, Mathf.Exp(-Mathf.Pow((along - band) / 0.12f, 2f)) * 0.85f);

            for (int k = 0; k < 4; k++)
            {
                int idx = v0 + k;
                verts[idx] = center + (src[idx] - center) * popScale + Vector3.up * wave;
                Color vc = (k == 1 || k == 2) ? Color.Lerp(c, Color.white, 0.3f) : c;   // tops a little hotter
                vc.a = alpha;
                cols[idx] = vc;
            }
        }
        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => material = null;
}
