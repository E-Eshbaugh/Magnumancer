using TMPro;
using UnityEngine;

/// <summary>
/// Glowing looks for items: each item's little model (built from primitives, no assets),
/// floating name labels, rings and the dark ground shadow that keeps a pickup readable on
/// bright floors. The same model is what a wonder weapon looks like in your hands.
/// </summary>
public static class ItemVisuals
{
    static Material labelMaterial, shadowMaterial;
    static MaterialPropertyBlock block;

    /// A glowing primitive (no collider) tinted `color`
    public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color,
                                  Vector3 euler = default, float glow = 2f)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = AbilityKit.Glow();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Tint(r, color, glow);
        return go;
    }

    public static void Tint(Renderer r, Color color, float glow = 2f)
    {
        block ??= new MaterialPropertyBlock();
        var c = color * glow; c.a = 1f;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }

    /// The item's model under `parent`, about 0.7 units tall, centered on the parent
    public static Transform Model(ItemId id, Transform parent)
    {
        var root = new GameObject($"Model ({id})").transform;
        root.SetParent(parent, false);
        Color c = ItemBook.Get(id).color;
        Color hot = Color.Lerp(c, Color.white, 0.55f);
        var P = PrimitiveType.Sphere;

        switch (id)
        {
            case ItemId.RuneShard:
            {
                var shard = new GameObject("Shard").transform;
                shard.SetParent(root, false);
                shard.localScale = new Vector3(1f, 1.8f, 1f);
                Part(PrimitiveType.Cube, shard, Vector3.zero, Vector3.one * 0.3f, c, new Vector3(45f, 0f, 35.26f));
                Part(P, root, Vector3.zero, Vector3.one * 0.14f, Color.white);
                break;
            }
            case ItemId.HealingDraught:
                Part(P, root, new Vector3(0f, -0.08f, 0f), Vector3.one * 0.42f, c);
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 0.1f, 0.14f), hot);
                Part(PrimitiveType.Cube, root, new Vector3(0f, 0.33f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Color.white, glow: 1.2f);
                break;
            case ItemId.OverdriveOrb:
                Part(P, root, Vector3.zero, Vector3.one * 0.36f, c);
                Part(P, root, new Vector3(0.32f, 0.05f, 0f), Vector3.one * 0.11f, hot);
                Part(P, root, new Vector3(-0.32f, -0.05f, 0f), Vector3.one * 0.11f, hot);
                break;
            case ItemId.ElementalRounds:
            {
                Element[] shown = { Element.Fire, Element.Frost, Element.Lightning };
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Capsule, root, new Vector3((i - 1) * 0.17f, 0f, 0f), new Vector3(0.12f, 0.17f, 0.12f),
                         Elements.ColorOf(shown[i]), new Vector3(0f, 0f, (i - 1) * -12f));
                break;
            }
            case ItemId.BlinkCharm:
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Cube, root, new Vector3(0f, (i - 1) * 0.2f, 0f), new Vector3(0.18f, 0.18f, 0.06f),
                         i == 1 ? hot : c, new Vector3(0f, 0f, 45f));
                break;
            case ItemId.AegisSigil:
                Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.55f, 0.03f, 0.55f), c, new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, -0.03f), new Vector3(0.25f, 0.03f, 0.25f), Color.white, new Vector3(90f, 0f, 0f));
                break;
            case ItemId.HexGrenade:
                Part(P, root, new Vector3(0f, -0.04f, 0f), Vector3.one * 0.36f, c);
                Part(PrimitiveType.Cube, root, new Vector3(0f, 0.18f, 0f), new Vector3(0.1f, 0.1f, 0.1f), Color.white, new Vector3(0f, 45f, 0f));
                break;
            case ItemId.StickyBomb:
                Part(P, root, Vector3.zero, Vector3.one * 0.36f, c);
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f;
                    Part(P, root, new Vector3(Mathf.Cos(a) * 0.19f, Mathf.Sin(a * 2f) * 0.06f, Mathf.Sin(a) * 0.19f), Vector3.one * 0.12f, hot);
                }
                break;
            case ItemId.PortalStone:
            {
                Part(P, root, Vector3.zero, Vector3.one * 0.16f, Color.white);
                var ring = Ring(root, 0.28f, c, 0.07f, 32);
                ring.useWorldSpace = false;
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float a = i / (float)ring.positionCount * Mathf.PI * 2f;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.28f);
                }
                break;
            }
            case ItemId.SingularityLauncher:
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, -0.05f), new Vector3(0.2f, 0.3f, 0.2f), c, new Vector3(90f, 0f, 0f));
                Part(P, root, new Vector3(0f, 0f, 0.3f), Vector3.one * 0.3f, Color.Lerp(c, Color.black, 0.5f), glow: 1f);
                Part(P, root, new Vector3(0f, 0f, 0.3f), Vector3.one * 0.14f, Color.white);
                break;
            case ItemId.FrostCannon:
                Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), c, new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.32f), new Vector3(0.4f, 0.04f, 0.4f), Color.white, new Vector3(90f, 0f, 0f));
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Cube, root, new Vector3((i - 1) * 0.12f, 0.2f, -0.1f), new Vector3(0.06f, 0.2f, 0.06f), hot, new Vector3(0f, 0f, (i - 1) * 25f));
                break;
            case ItemId.ThunderMaul:
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, -0.12f, 0f), new Vector3(0.07f, 0.28f, 0.07f), hot);
                Part(PrimitiveType.Cube, root, new Vector3(0f, 0.2f, 0f), new Vector3(0.48f, 0.24f, 0.26f), c);
                Part(PrimitiveType.Cube, root, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.06f, 0.28f), Color.white);
                break;
            case ItemId.GaleHorn:
                for (int i = 0; i < 4; i++)
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, -0.2f + i * 0.14f),
                         new Vector3(0.1f + i * 0.08f, 0.07f, 0.1f + i * 0.08f), i == 3 ? Color.white : c, new Vector3(90f, 0f, 0f));
                break;
            case ItemId.EmberMinigun:
            {
                var barrels = new GameObject("Barrels").transform;
                barrels.SetParent(root, false);
                for (int i = 0; i < 4; i++)
                {
                    float a = i / 4f * Mathf.PI * 2f;
                    Part(PrimitiveType.Cylinder, barrels, new Vector3(Mathf.Cos(a) * 0.08f, Mathf.Sin(a) * 0.08f, 0.1f),
                         new Vector3(0.06f, 0.3f, 0.06f), i % 2 == 0 ? c : hot, new Vector3(90f, 0f, 0f));
                }
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, -0.25f), new Vector3(0.28f, 0.1f, 0.28f), c, new Vector3(90f, 0f, 0f));
                break;
            }
            case ItemId.HeartRelic:
                Part(P, root, new Vector3(-0.12f, 0.08f, 0f), Vector3.one * 0.28f, c);
                Part(P, root, new Vector3(0.12f, 0.08f, 0f), Vector3.one * 0.28f, c);
                Part(PrimitiveType.Cube, root, new Vector3(0f, -0.06f, 0f), new Vector3(0.28f, 0.28f, 0.2f), c, new Vector3(0f, 0f, 45f));
                Part(P, root, new Vector3(-0.1f, 0.12f, -0.06f), Vector3.one * 0.08f, Color.white);
                break;
        }
        return root;
    }

    /// Flat glowing ring (world space, set the positions with AbilityKit.Circle)
    public static LineRenderer Ring(Transform parent, float radius, Color color, float width, int points = 40)
    {
        var lr = GlowLine.Make(parent, "Ring", points, width, AbilityKit.Glow());
        lr.loop = true;
        GlowLine.SetColor(lr, color, 1f);
        return lr;
    }

    /// Part of a flat circle: fraction 1 = full ring, 0 = nothing (timers)
    public static void Arc(LineRenderer lr, Vector3 center, float radius, float fraction, float startAngle = 90f)
    {
        int n = lr.positionCount;
        float sweep = Mathf.Clamp01(fraction) * 360f;
        lr.loop = fraction >= 0.999f;
        for (int i = 0; i < n; i++)
        {
            float a = (startAngle - sweep * i / Mathf.Max(1, n - (lr.loop ? 0 : 1))) * Mathf.Deg2Rad;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
        lr.enabled = fraction > 0.01f;
    }

    /// Floating name label (billboard it to the camera yourself)
    public static TextMeshPro Label(Transform parent, string text, Color color, float fontSize = 4.5f)
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null) return null;
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = LabelMaterial(font);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 2f);
        tmp.color = Color.Lerp(color, Color.white, 0.35f);
        tmp.sortingOrder = 40;
        return tmp;
    }

    static Material LabelMaterial(TMP_FontAsset font)
    {
        if (labelMaterial != null) return labelMaterial;
        labelMaterial = new Material(font.material) { name = "ItemLabel" };
        labelMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        labelMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
        labelMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
        labelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.03f, 0.08f, 1f));
        labelMaterial.renderQueue = 3500;
        return labelMaterial;
    }

    /// Soft dark disc on the ground under a pickup so its glow reads on bright floors
    public static GameObject Shadow(Transform parent, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Shadow";
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * radius * 2f;
        var r = go.GetComponent<MeshRenderer>();
        if (shadowMaterial == null)
        {
            shadowMaterial = GlowLine.CreateParticleMaterial(1f, additive: false);
            shadowMaterial.name = "ItemShadow";
        }
        r.sharedMaterial = shadowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        var c = new Color(0.02f, 0.01f, 0.04f, 0.75f);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
        return go;
    }

    /// Camera-facing rotation (labels, crowns)
    public static Quaternion Billboard(Quaternion fallback)
    {
        var cam = Camera.main;
        return cam != null ? cam.transform.rotation : fallback;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        labelMaterial = null;
        shadowMaterial = null;
    }
}
