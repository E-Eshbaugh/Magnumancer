using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bullet holes and scorch marks on floors and walls, so firefights leave the arena
/// peppered. Fire rounds scorch wider, frost leaves a pale frost patch, poison a sickly
/// stain. A fixed pool: once full, the oldest marks are reused.
/// </summary>
public static class ImpactMarks
{
    public static bool Enabled = true;
    public static int Max = 220;

    static readonly Queue<GameObject> marks = new();

    public static void Mark(Vector3 point, Vector3 normal, Element element, float damage)
    {
        if (!Enabled || normal.sqrMagnitude < 0.01f) return;
        float size = Mathf.Clamp(0.12f + damage * 0.006f, 0.12f, 0.4f);
        Color c = new Color(0.03f, 0.025f, 0.02f, 0.7f);
        switch (element)
        {
            case Element.Fire: size *= 1.9f; c = new Color(0.06f, 0.035f, 0.02f, 0.6f); break;
            case Element.Frost: size *= 1.5f; c = new Color(0.8f, 0.92f, 1f, 0.45f); break;
            case Element.Poison: size *= 1.4f; c = new Color(0.25f, 0.35f, 0.05f, 0.5f); break;
            case Element.Lightning: size *= 1.3f; c = new Color(0.04f, 0.04f, 0.06f, 0.65f); break;
        }

        // reuse the oldest once the pool is full
        GameObject go;
        if (marks.Count >= Max)
        {
            go = marks.Dequeue();
            if (go == null) go = New(c);
            else Craters.Paint(go, c, reuse: true);
        }
        else go = New(c);
        marks.Enqueue(go);

        go.transform.SetPositionAndRotation(point + normal * 0.012f,
            Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.value * 360f));
        go.transform.localScale = new Vector3(size * Random.Range(0.8f, 1.2f), size * Random.Range(0.8f, 1.2f), 1f);
    }

    static GameObject New(Color c)
    {
        var go = new GameObject("ImpactMark");
        Craters.Paint(go, c);
        return go;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => marks.Clear();
}
