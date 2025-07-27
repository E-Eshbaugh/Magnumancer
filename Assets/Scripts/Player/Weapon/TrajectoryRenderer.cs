using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TrajectoryRenderer : MonoBehaviour
{
    public float dashPlusGap = 0.36f;   // world units per full cycle (dash+gap)
    public float lineWidth = 0.06f;
    public bool useGradient = false;
    public Color startColor = new(1f, 0.35f, 0.15f);
    public Color endColor   = new(0.85f, 0f, 0f);

    LineRenderer lr;
    Gradient grad;
    float lastWidth;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.enabled = false;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Tile;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;

        if (useGradient)
        {
            grad = BuildGradient(startColor, endColor);
            lr.colorGradient = grad;
        }
        else
        {
            lr.startColor = startColor;
            lr.endColor = endColor;
        }
    }

    Gradient BuildGradient(Color a, Color b)
    {
        var g = new Gradient();
        g.SetKeys(
            new []{ new GradientColorKey(a,0f), new GradientColorKey(b,1f) },
            new []{ new GradientAlphaKey(a.a,0f), new GradientAlphaKey(b.a,1f) }
        );
        return g;
    }

    public void Render(Vector3[] points, int count)
    {
        if (count < 2) { lr.enabled = false; return; }

        if (!lr.enabled) lr.enabled = true;
        lr.positionCount = count;
        for (int i = 0; i < count; i++)
            lr.SetPosition(i, points[i]);

        // compute total length
        float total = 0f;
        for (int i = 1; i < count; i++)
            total += Vector3.Distance(points[i - 1], points[i]);

        float cycles = (dashPlusGap > 0.0001f) ? total / dashPlusGap : 1f;

        // Adjust tiling (x = repeat count)
        var mat = lr.sharedMaterial;
        if (mat != null)
        {
            // Assumes main texture is dash pattern
            mat.mainTextureScale = new Vector2(cycles, 1f);
        }
    }

    public void Hide() => lr.enabled = false;

    public void SetWidth(float w)
    {
        if (Mathf.Approximately(w, lastWidth)) return;
        lastWidth = w;
        lr.widthMultiplier = w;
    }

    public void SetGradientColors(Color a, Color b)
    {
        if (!useGradient) return;
        grad = BuildGradient(a, b);
        lr.colorGradient = grad;
    }
}
