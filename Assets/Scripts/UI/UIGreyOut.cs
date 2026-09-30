using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Greys out an eliminated player's HUD panel (crest, hearts, bars): a jolt, then the
/// color drains away to a dim greyscale that stays for the rest of the match.
/// </summary>
public class UIGreyOut : MonoBehaviour
{
    public float drainTime = 0.6f;
    public float shake = 10f;
    public Color deadTint = new Color(0.5f, 0.5f, 0.5f, 0.75f);

    static Shader shader;
    Material material;

    public static UIGreyOut Apply(GameObject panel)
    {
        if (panel == null) return null;
        var grey = panel.GetComponent<UIGreyOut>();
        if (grey == null) grey = panel.AddComponent<UIGreyOut>();
        grey.StopAllCoroutines();
        if (grey.isActiveAndEnabled) grey.StartCoroutine(grey.Run());
        else grey.SetTint(1f);
        return grey;
    }

    IEnumerator Run()
    {
        foreach (var ui in GetComponentsInChildren<CircleAbilityUI>(true))
            ui.SetEliminated();

        if (shader == null) shader = Resources.Load<Shader>("Shaders/UIGreyscale");
        if (shader != null && material == null)
            material = new Material(shader) { name = "UIGreyOut" };

        // Images go greyscale; anything else (text) is just dimmed
        foreach (var g in GetComponentsInChildren<Graphic>(true))
        {
            if (material != null && (g is Image || g is RawImage)) g.material = material;
            else g.color = new Color(0.5f, 0.5f, 0.5f, g.color.a * deadTint.a);
        }

        // jolt the panel's contents (the Canvas root itself can't move)
        var parts = new RectTransform[transform.childCount];
        var home = new Vector2[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = transform.GetChild(i) as RectTransform;
            if (parts[i] != null) home[i] = parts[i].anchoredPosition;
        }

        float t = 0f;
        while (t < drainTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / drainTime);
            SetTint(k);
            float amp = shake * (1f - k) * (1f - k);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].anchoredPosition = home[i] + Random.insideUnitCircle * amp;
            yield return null;
        }
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] != null) parts[i].anchoredPosition = home[i];
        SetTint(1f);
    }

    void SetTint(float k)
    {
        if (material != null) material.SetColor("_Color", Color.Lerp(Color.white, deadTint, k));
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
