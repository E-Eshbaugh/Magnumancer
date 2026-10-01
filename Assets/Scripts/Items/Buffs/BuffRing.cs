using UnityEngine;

/// <summary>
/// A thin arc at a player's feet showing how much of an item buff is left (time, rounds,
/// shield). Kept faint so a crowded screen stays readable. Each buff uses its own radius
/// so two at once nest instead of overlapping.
/// </summary>
public class BuffRing
{
    readonly LineRenderer lr;
    readonly float radius;
    Color color;

    public BuffRing(Transform owner, float radius, Color color)
    {
        this.radius = radius;
        this.color = color;
        var go = new GameObject("BuffRing");
        go.transform.SetParent(owner, false);
        lr = GlowLine.Make(go.transform, "Arc", 40, 0.06f, AbilityKit.Glow());
    }

    public void SetColor(Color c) => color = c;

    /// fraction 1 = full ring; alpha scales the glow (flash it when something happens)
    public void Draw(Vector3 feet, float fraction, float alpha = 0.6f)
    {
        if (lr == null) return;
        ItemVisuals.Arc(lr, feet + Vector3.up * 0.06f, radius, fraction);
        GlowLine.SetColor(lr, color, alpha);
    }

    public void Destroy()
    {
        if (lr != null) Object.Destroy(lr.transform.parent.gameObject);
    }
}
