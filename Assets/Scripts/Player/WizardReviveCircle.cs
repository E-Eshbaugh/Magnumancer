using UnityEngine;

/// A fixed rescue boundary and a clockwise progress arc around a fallen wizard.
public class WizardReviveCircle : MonoBehaviour
{
    PlayerHealthControl health;
    GameObject visuals;
    Material material;
    LineRenderer boundary, progress, crossA, crossB;
    TextMesh label;
    Animator fallenAnimator;
    Transform fallenModel;
    Quaternion standingRotation;
    Vector3 standingPosition;
    bool animatorWasEnabled;
    const int Points = 65;
    static readonly Color Waiting = new Color(1f, 0.65f, 0.15f);
    static readonly Color Helping = new Color(0.25f, 1f, 0.65f);

    public void Show(PlayerHealthControl target)
    {
        health = target;
        if (fallenModel == null && health.movement != null)
        {
            var animator = health.movement.animator;
            // Tilt only the model, keeping the controller and rescue boundary upright.
            if (animator != null && animator.transform != transform && animator.transform.IsChildOf(transform))
            {
                fallenAnimator = animator;
                fallenModel = animator.transform;
                standingRotation = fallenModel.localRotation;
                standingPosition = fallenModel.localPosition;
                animatorWasEnabled = animator.enabled;
                animator.enabled = false;
                fallenModel.localRotation = standingRotation * Quaternion.Euler(0f, 0f, 75f);
                fallenModel.localPosition = standingPosition + Vector3.up * 0.2f;
            }
        }
        if (visuals == null)
        {
            visuals = new GameObject("ReviveCircle");
            visuals.transform.SetParent(transform, false);
            material = GlowLine.CreateMaterial(1.5f);
            boundary = GlowLine.Make(visuals.transform, "ReviveBoundary", Points, 0.12f, material);
            progress = GlowLine.Make(visuals.transform, "ReviveProgress", Points, 0.2f, material);
            crossA = GlowLine.Make(visuals.transform, "RescueCrossA", 2, 0.15f, material);
            crossB = GlowLine.Make(visuals.transform, "RescueCrossB", 2, 0.15f, material);
            var text = new GameObject("ReviveLabel");
            text.transform.SetParent(visuals.transform, false);
            label = text.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.065f;
            label.fontSize = 48;
        }
        visuals.SetActive(true);
        LateUpdate();
    }

    public void Hide()
    {
        if (visuals != null) visuals.SetActive(false);
        if (fallenModel != null)
        {
            fallenModel.localRotation = standingRotation;
            fallenModel.localPosition = standingPosition;
            if (fallenAnimator != null) fallenAnimator.enabled = animatorWasEnabled;
            fallenModel = null;
            fallenAnimator = null;
        }
    }

    void OnDisable() => Hide();

    void LateUpdate()
    {
        if (health == null || !health.IsDowned) { Hide(); return; }
        Vector3 center = health.transform.position + Vector3.up * 0.08f;
        float amount = health.ReviveProgress;
        Color color = amount > 0f ? Helping : Waiting;
        DrawArc(boundary, center, health.reviveRadius, 1f);
        DrawArc(progress, center + Vector3.up * 0.02f, health.reviveRadius - 0.2f, amount);
        GlowLine.SetColor(boundary, color, 0.65f + 0.2f * Mathf.Sin(Time.time * 4f));
        GlowLine.SetColor(progress, Helping, 1f);
        crossA.SetPosition(0, center + Vector3.left * 0.4f);
        crossA.SetPosition(1, center + Vector3.right * 0.4f);
        crossB.SetPosition(0, center + Vector3.back * 0.4f);
        crossB.SetPosition(1, center + Vector3.forward * 0.4f);
        GlowLine.SetColor(crossA, color, 1f);
        GlowLine.SetColor(crossB, color, 1f);
        label.text = amount > 0f ? $"REVIVING {Mathf.FloorToInt(amount * 100f)}%" : "DOWN\nSTAND IN CIRCLE";
        label.color = color;
        label.transform.position = center + Vector3.up * 2.8f;
        if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        // Keep the text readable regardless of the player prefab's scale.
        Vector3 scale = label.transform.parent.lossyScale;
        label.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, scale.x),
            1f / Mathf.Max(0.01f, scale.y), 1f / Mathf.Max(0.01f, scale.z));
    }

    static void DrawArc(LineRenderer line, Vector3 center, float radius, float fraction)
    {
        line.enabled = fraction > 0f;
        if (!line.enabled) return;
        for (int i = 0; i < Points; i++)
        {
            float angle = Mathf.PI * 0.5f - Mathf.PI * 2f * fraction * i / (Points - 1);
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    void OnDestroy() { if (material != null) Destroy(material); }
}
