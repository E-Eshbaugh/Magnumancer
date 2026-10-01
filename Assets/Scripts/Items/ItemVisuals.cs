using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Looks for items: each item's model (art models from the project, refitted and recolored,
/// plus glowing details), rings and the dark ground shadow that keeps a pickup readable on
/// bright floors. The same model is what a wonder weapon looks like in your hands.
/// </summary>
public static class ItemVisuals
{
    static Material shadowMaterial, solidMaterial;
    static MaterialPropertyBlock block;
    static bool buildingSolid;

    // Flat, unshaded color: alpha-blended at full alpha, so it doesn't add light (glowing
    // parts blow out to white under bloom and lose their shape)
    static Material Solid()
    {
        if (solidMaterial != null) return solidMaterial;
        solidMaterial = GlowLine.CreateMaterial(1f, additive: false);
        solidMaterial.name = "ItemSolid";
        return solidMaterial;
    }

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
        r.sharedMaterial = buildingSolid ? Solid() : AbilityKit.Glow();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Tint(r, color, buildingSolid ? 1.15f : glow);
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

    /// The item's model under `parent`, about 0.7-1 units across, centered on the parent.
    /// Built from art models (Resources/ItemModels: fitted, recolored in the item's color
    /// with a little glow) plus glowing accents, or meshes made here (horn, heart).
    /// solid = accents in flat colors instead of glowing ones (pickups on the ground)
    public static Transform Model(ItemId id, Transform parent, bool solid = false)
    {
        buildingSolid = solid;
        try { return BuildModel(id, parent); }
        finally { buildingSolid = false; }
    }

    static Transform BuildModel(ItemId id, Transform parent)
    {
        var root = new GameObject($"Model ({id})").transform;
        root.SetParent(parent, false);
        var bank = ItemModelBank.Get();
        Color c = ItemBook.Get(id).color;
        Color hot = Color.Lerp(c, Color.white, 0.55f);
        Color gold = ItemBook.Gold;
        var P = PrimitiveType.Sphere;
        GameObject M(System.Func<ItemModelBank, GameObject> pick) => bank != null ? pick(bank) : null;

        switch (id)
        {
            case ItemId.RuneShard:
                Art(M(b => b.crystal), root, c, 0.8f, Vector3.zero, default, Orient.Tall);
                Art(M(b => b.crystal), root, hot, 0.32f, new Vector3(0.26f, -0.22f, 0f), new Vector3(0f, 0f, -28f), Orient.Tall);
                Art(M(b => b.crystal), root, hot, 0.26f, new Vector3(-0.24f, -0.25f, 0.05f), new Vector3(0f, 40f, 30f), Orient.Tall);
                Part(P, root, Vector3.zero, Vector3.one * 0.12f, Color.white);
                break;

            case ItemId.HealingDraught:
                Art(M(b => b.bottle), root, c, 0.78f, Vector3.zero, default, Orient.Tall);
                foreach (float side in new[] { -1f, 1f })   // a white cross on both faces
                {
                    Part(PrimitiveType.Cube, root, new Vector3(0f, -0.08f, 0.2f * side), new Vector3(0.2f, 0.06f, 0.03f), Color.white, glow: 1.4f);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, -0.08f, 0.2f * side), new Vector3(0.06f, 0.2f, 0.03f), Color.white, glow: 1.4f);
                }
                break;

            case ItemId.OverdriveOrb:
                Art(M(b => b.ammoBox), root, c, 0.72f, Vector3.zero, default, Orient.Wide);
                LitPart(P, root, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.2f, hot, 1.5f);
                Part(P, root, new Vector3(0.45f, 0.1f, 0f), Vector3.one * 0.1f, hot);
                Part(P, root, new Vector3(-0.45f, 0.1f, 0f), Vector3.one * 0.1f, hot);
                break;

            case ItemId.ElementalRounds:
                Art(M(b => b.bullet), root, Elements.ColorOf(Element.Fire), 0.55f, new Vector3(-0.22f, 0f, 0f), new Vector3(0f, 0f, 14f), Orient.Tall);
                Art(M(b => b.bulletSniper), root, Elements.ColorOf(Element.Frost), 0.72f, new Vector3(0f, 0.06f, 0f), default, Orient.Tall);
                Art(M(b => b.bulletShotgun), root, Elements.ColorOf(Element.Lightning), 0.5f, new Vector3(0.22f, -0.03f, 0f), new Vector3(0f, 0f, -14f), Orient.Tall);
                break;

            case ItemId.BlinkCharm:
                Art(M(b => b.dipyramid), root, c, 0.5f, Vector3.zero, default, Orient.Tall);
                Art(M(b => b.dipyramid), root, hot, 0.3f, new Vector3(0f, 0.38f, 0f), default, Orient.Tall);
                Art(M(b => b.dipyramid), root, hot, 0.3f, new Vector3(0f, -0.38f, 0f), default, Orient.Tall);
                Part(P, root, Vector3.zero, Vector3.one * 0.1f, Color.white);
                break;

            case ItemId.AegisSigil:
                Art(M(b => b.shield), root, c, 0.85f, Vector3.zero, default, Orient.Tall);
                LocalRing(root, 0.5f, gold, 0.05f, Vector3.zero, new Vector3(90f, 0f, 0f));
                break;

            case ItemId.HexGrenade:
                Art(M(b => b.grenade), root, c, 0.62f, Vector3.zero, default, Orient.Tall);
                LocalRing(root, 0.42f, hot, 0.05f, Vector3.zero, new Vector3(20f, 0f, 0f), 6);   // a hexagon of runes
                LocalRing(root, 0.42f, hot, 0.05f, Vector3.zero, new Vector3(-20f, 60f, 0f), 6);
                break;

            case ItemId.StickyBomb:
                Art(M(b => b.bomb), root, c, 0.68f, Vector3.zero, default, Orient.Tall);
                for (int i = 0; i < 4; i++)   // goo dripping off it
                {
                    float a = i / 4f * Mathf.PI * 2f + 0.4f;
                    LitPart(P, root, new Vector3(Mathf.Cos(a) * 0.26f, -0.18f - 0.05f * (i % 2), Mathf.Sin(a) * 0.26f),
                            new Vector3(0.12f, 0.16f, 0.12f), hot, 0.8f);
                }
                Part(P, root, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.1f, new Color(1f, 0.85f, 0.4f));   // lit fuse
                break;

            case ItemId.PortalStone:
                Art(M(b => b.hexGem), root, c, 0.45f, Vector3.zero, default, Orient.Tall);
                LocalRing(root, 0.45f, c, 0.07f, Vector3.zero, Vector3.zero);
                LocalRing(root, 0.36f, Color.white, 0.04f, Vector3.zero, new Vector3(0f, 90f, 0f));
                break;

            case ItemId.SingularityLauncher:
                Art(M(b => b.rocketLauncher), root, c, 1f, Vector3.zero, default, Orient.Long);
                LitPart(P, root, new Vector3(0f, 0.02f, 0.55f), Vector3.one * 0.28f, new Color(0.08f, 0.02f, 0.14f), 0.15f);
                LocalRing(root, 0.2f, Color.white, 0.04f, new Vector3(0f, 0.02f, 0.55f), Vector3.zero);
                LocalRing(root, 0.26f, c, 0.05f, new Vector3(0f, 0.02f, 0.55f), new Vector3(70f, 0f, 0f));
                break;

            case ItemId.FrostCannon:
                Art(M(b => b.blaster), root, c, 0.95f, Vector3.zero, default, Orient.Long);
                for (int i = 0; i < 3; i++)
                    Art(M(b => b.radiant), root, hot, 0.26f - i * 0.03f, new Vector3((i - 1) * 0.1f, 0.24f, -0.15f + i * 0.12f),
                        new Vector3(0f, 0f, (i - 1) * 25f), Orient.Tall);
                LocalRing(root, 0.18f, Color.white, 0.05f, new Vector3(0f, 0f, 0.5f), Vector3.zero);
                break;

            case ItemId.ThunderMaul:
                Art(M(b => b.staff), root, Color.Lerp(c, new Color(0.35f, 0.3f, 0.25f), 0.6f), 0.85f, new Vector3(0f, -0.08f, 0f), default, Orient.Tall);
                Art(M(b => b.keg), root, c, 0.5f, new Vector3(0f, 0.32f, 0f), new Vector3(0f, 0f, 90f), Orient.None);
                LitPart(PrimitiveType.Cylinder, root, new Vector3(0.27f, 0.32f, 0f), new Vector3(0.24f, 0.03f, 0.24f), gold, 0.8f, new Vector3(0f, 0f, 90f));
                LitPart(PrimitiveType.Cylinder, root, new Vector3(-0.27f, 0.32f, 0f), new Vector3(0.24f, 0.03f, 0.24f), gold, 0.8f, new Vector3(0f, 0f, 90f));
                Part(P, root, new Vector3(0.3f, 0.32f, 0f), Vector3.one * 0.1f, Color.white);
                Part(P, root, new Vector3(-0.3f, 0.32f, 0f), Vector3.one * 0.1f, Color.white);
                break;

            case ItemId.GaleHorn:
                MeshPart(HornMesh(), root, c, Vector3.zero, default, 1f, 0.35f, twoSided: true);
                LitPart(PrimitiveType.Cylinder, root, new Vector3(0f, -0.03f, -0.1f), new Vector3(0.12f, 0.02f, 0.12f), gold, 0.6f, new Vector3(90f, 0f, 0f));
                LitPart(PrimitiveType.Cylinder, root, new Vector3(0f, 0.02f, 0.15f), new Vector3(0.22f, 0.02f, 0.22f), gold, 0.6f, new Vector3(90f, 0f, 0f));
                LitPart(PrimitiveType.Cylinder, root, new Vector3(0f, -0.06f, -0.45f), new Vector3(0.07f, 0.04f, 0.07f), gold, 0.6f, new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.07f, 0.4f), new Vector3(0.5f, 0.01f, 0.5f), hot, new Vector3(90f, 0f, 0f), 1.2f);
                break;

            case ItemId.EmberMinigun:
            {
                Art(M(b => b.machineGun), root, c, 1.05f, new Vector3(0f, 0f, -0.08f), default, Orient.Long);
                var barrels = new GameObject("Barrels").transform;   // EmberMinigun spins these
                barrels.SetParent(root, false);
                barrels.localPosition = new Vector3(0f, 0f, 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i / 4f * Mathf.PI * 2f;
                    Part(PrimitiveType.Cylinder, barrels, new Vector3(Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.06f, 0f),
                         new Vector3(0.045f, 0.13f, 0.045f), i % 2 == 0 ? c : hot, new Vector3(90f, 0f, 0f));
                }
                for (int i = 0; i < 3; i++)   // glowing heat vents
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 0.12f, 0.05f + i * 0.1f), new Vector3(0.1f, 0.03f, 0.04f), hot);
                break;
            }

            case ItemId.HeartRelic:
                MeshPart(HeartMesh(), root, c, Vector3.zero, default, 1f, 0.6f, twoSided: true);
                Part(P, root, new Vector3(-0.12f, 0.12f, -0.12f), Vector3.one * 0.08f, Color.white);
                LocalRing(root, 0.42f, gold, 0.04f, Vector3.zero, Vector3.zero);
                break;
        }
        return root;
    }

    // ---------- art models ----------

    enum Orient { None, Tall, Wide, Long }

    static readonly Dictionary<(Texture, Color, bool), Material> litMaterials = new();
    static Shader litShader;

    // URP Lit in the item's color, keeping the art's own texture for detail, glowing a little
    static Material Lit(Texture texture, Color tint, float emission, bool twoSided = false)
    {
        var key = (texture, tint, twoSided);
        if (litMaterials.TryGetValue(key, out var m) && m != null) return m;
        if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) return Solid();
        m = new Material(litShader) { name = "ItemLit" };
        m.SetColor("_BaseColor", tint);
        if (texture != null) m.SetTexture("_BaseMap", texture);
        m.SetFloat("_Smoothness", 0.35f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", tint * emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        if (twoSided) m.SetFloat("_Cull", 0f);
        litMaterials[key] = m;
        return m;
    }

    /// A copy of an art model, fitted to `size` (its largest side), centered, turned (Tall:
    /// longest side up; Wide: longest side across; Long: longest side forward, the far end
    /// from its pivot as the front), and recolored in `tint`. Null model: a plain block.
    static Transform Art(GameObject prefab, Transform parent, Color tint, float size, Vector3 pos, Vector3 euler,
                         Orient orient, float emission = 0.3f)
    {
        var fit = new GameObject(prefab != null ? prefab.name : "Art").transform;
        fit.SetParent(parent, false);
        fit.localPosition = pos;
        fit.localRotation = Quaternion.Euler(euler);
        if (prefab == null)
        {
            LitPart(PrimitiveType.Cube, fit, Vector3.zero, Vector3.one * size * 0.7f, tint, emission);
            return fit;
        }

        var inst = Object.Instantiate(prefab, fit, false);
        inst.name = prefab.name;
        foreach (var col in inst.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
        foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb);
        var renderers = inst.GetComponentsInChildren<Renderer>(true);
        var t = inst.transform;

        var b = BoundsIn(fit, renderers);
        switch (orient)
        {
            case Orient.Tall:
                if (b.size.x >= b.size.y && b.size.x >= b.size.z) t.localRotation = Quaternion.Euler(0f, 0f, 90f) * t.localRotation;
                else if (b.size.z >= b.size.y && b.size.z > b.size.x) t.localRotation = Quaternion.Euler(90f, 0f, 0f) * t.localRotation;
                break;
            case Orient.Wide:
            case Orient.Long:
                if (b.size.y > b.size.x && b.size.y > b.size.z) t.localRotation = Quaternion.Euler(90f, 0f, 0f) * t.localRotation;
                b = BoundsIn(fit, renderers);
                if (orient == Orient.Long && b.size.x > b.size.z) t.localRotation = Quaternion.Euler(0f, 90f, 0f) * t.localRotation;
                if (orient == Orient.Long && BoundsIn(fit, renderers).center.z < 0f) t.localRotation = Quaternion.Euler(0f, 180f, 0f) * t.localRotation;
                break;
        }

        b = BoundsIn(fit, renderers);
        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        t.localPosition -= b.center;
        fit.localScale = Vector3.one * (size / Mathf.Max(1e-4f, largest));

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = Lit(MainTexture(mats[i]), tint, emission);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return fit;
    }

    static Texture MainTexture(Material m)
    {
        if (m == null) return null;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
        if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
        return null;
    }

    // Bounds of the renderers, in `space`'s local coordinates
    static Bounds BoundsIn(Transform space, Renderer[] renderers)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in renderers)
        {
            var lb = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    /// A primitive in a lit, slightly glowing material (solid details on the art models)
    static GameObject LitPart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, float emission,
                              Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Lit(null, color, emission);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static GameObject MeshPart(Mesh mesh, Transform parent, Color color, Vector3 pos, Vector3 euler, float scale, float emission,
                           bool twoSided = false)
    {
        var go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Lit(null, color, emission, twoSided);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// Glowing ring in the parent's space (lies in its XY plane before `euler`); sides = 6 for a hexagon
    static LineRenderer LocalRing(Transform parent, float radius, Color color, float width, Vector3 pos, Vector3 euler, int sides = 32)
    {
        var holder = new GameObject("Ring").transform;
        holder.SetParent(parent, false);
        holder.localPosition = pos;
        holder.localRotation = Quaternion.Euler(euler);
        var lr = GlowLine.Make(holder, "Line", sides, width, AbilityKit.Glow());
        lr.useWorldSpace = false;
        lr.loop = true;
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
        }
        GlowLine.SetColor(lr, color, 0.9f);
        return lr;
    }

    // ---------- made-here meshes ----------

    static Mesh hornMesh, heartMesh;

    // A war horn: a thin mouthpiece at the back curling up into a wide flared bell at the front (+Z)
    static Mesh HornMesh()
    {
        if (hornMesh != null) return hornMesh;
        const int rings = 28, sides = 18;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            Vector3 center = new Vector3(0f, 0.16f * t * t - 0.08f, -0.45f + 0.88f * t);
            float radius = 0.035f + 0.3f * Mathf.Pow(t, 4f);
            Vector3 along = new Vector3(0f, 0.32f * t, 0.88f).normalized;
            Vector3 side = Vector3.right, up = Vector3.Cross(along, side).normalized;
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                verts.Add(center + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
        }
        for (int i = 0; i < rings; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = i * (sides + 1) + s, b = a + sides + 1;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        hornMesh = new Mesh { name = "GaleHorn" };
        hornMesh.SetVertices(verts);
        hornMesh.SetTriangles(tris, 0);
        hornMesh.RecalculateNormals();
        hornMesh.RecalculateBounds();
        return hornMesh;
    }

    // A puffy heart (domed front and back), about 0.6 wide, facing ±Z
    static Mesh HeartMesh()
    {
        if (heartMesh != null) return heartMesh;
        const int n = 56;
        const float k = 0.6f / 34f, rim = 0.07f, dome = 0.17f;
        var outline = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n * Mathf.PI * 2f;
            float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            outline[i] = new Vector3(x * k, (y + 2f) * k, 0f);
        }
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (float face in new[] { -1f, 1f })
        {
            int center = verts.Count;
            verts.Add(new Vector3(0f, 0.02f, dome * face));
            for (int i = 0; i < n; i++) verts.Add(outline[i] + Vector3.forward * rim * face);
            for (int i = 0; i < n; i++)
            {
                int a = center + 1 + i, b = center + 1 + (i + 1) % n;
                if (face < 0f) tris.AddRange(new[] { center, b, a });
                else tris.AddRange(new[] { center, a, b });
            }
        }
        for (int i = 0; i < n; i++)   // the rim between the faces
        {
            int s = verts.Count;
            Vector3 p = outline[i], q = outline[(i + 1) % n];
            verts.Add(p - Vector3.forward * rim); verts.Add(q - Vector3.forward * rim);
            verts.Add(p + Vector3.forward * rim); verts.Add(q + Vector3.forward * rim);
            tris.AddRange(new[] { s, s + 2, s + 1, s + 1, s + 2, s + 3 });
        }
        heartMesh = new Mesh { name = "HeartRelic" };
        heartMesh.SetVertices(verts);
        heartMesh.SetTriangles(tris, 0);
        heartMesh.RecalculateNormals();
        heartMesh.RecalculateBounds();
        return heartMesh;
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
        shadowMaterial = null;
        solidMaterial = null;
        litMaterials.Clear();
        litShader = null;
        hornMesh = null;
        heartMesh = null;
    }
}
