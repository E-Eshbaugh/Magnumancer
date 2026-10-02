using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bakes each arena into a square floating-island miniature for the War Table:
/// opens the scene, takes a square around the playfield, clips every static mesh in it to
/// that square (so the edges are clean diorama cuts), turns terrain into a mesh, and hangs
/// a jagged chunk of earth underneath. Saved at real scale (the War Table shrinks it) to
/// Assets/Art/Miniatures/{scene}/Mini_{scene}.prefab.
///
/// Re-run after editing an arena: Magnumancer > Great Hall > Bake Map Miniatures.
/// </summary>
public static class MiniatureBaker
{
    public const string Folder = "Assets/Art/Miniatures";

    /// Per-map square: centre offset from the camera's ground focus, and side length
    public class Crop { public string scene; public Vector2 offset; public float size = 30f; }

    public static readonly Crop[] Crops =
    {
        new Crop { scene = "Oldwoods3D" },
        new Crop { scene = "Stormspire" },
        new Crop { scene = "FungalHollow" },
        new Crop { scene = "Riftforge" },
        new Crop { scene = "CinderCrucible" },
        new Crop { scene = "CinderCrucibleZombies" },
        new Crop { scene = "DrownedSanctum" },
        new Crop { scene = "BlackOsuary" },
        new Crop { scene = "Frostgrave" },
    };

    // scene roots that are gameplay/UI, never scenery
    static readonly string[] SkipRoots = { "Canvas", "PauseMenu", "WinManager", "EventSystem", "Players", "Players (1)", "GoblinSpawner", "CameraSled", "Global Volume" };

    [MenuItem("Magnumancer/Great Hall/Bake Map Miniatures")]
    public static void BakeAll()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            foreach (var c in Crops) Bake(c);
        }
        finally
        {
            if (!Application.isBatchMode && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LinkCatalog();
    }

    /// Points every War Table entry at its own scene's miniature (a map without one keeps what it had)
    static void LinkCatalog()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:HallCatalog"))
        {
            var cat = AssetDatabase.LoadAssetAtPath<HallCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (cat == null || cat.maps == null) continue;
            foreach (var m in cat.maps)
            {
                var mini = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(m.scene));
                if (mini != null) m.miniature = mini;
            }
            EditorUtility.SetDirty(cat);
        }
        AssetDatabase.SaveAssets();
    }

    /// Tallest geometry kept, as a fraction of the square's side
    public static float MaxHeightFraction = 0.28f;
    static float ClipTop = float.MaxValue;

    public static string PrefabPath(string scene) => $"{Folder}/{scene}/Mini_{scene}.prefab";

    public static void Bake(Crop crop)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{crop.scene}.unity", OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();

        // ground height = where the wizards stand
        var movers = roots.SelectMany(r => r.GetComponentsInChildren<PlayerMovement3D>(true)).ToArray();
        float groundY = movers.Length > 0 ? movers.Average(m => m.transform.position.y) : 0f;

        // the camera's focus on the ground is the middle of the fight
        var cam = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"))
                  ?? roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        Vector3 focus = Vector3.zero;
        if (cam != null)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var plane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            if (plane.Raycast(ray, out float d)) focus = ray.GetPoint(d);
        }
        else if (movers.Length > 0) focus = new Vector3(movers.Average(m => m.transform.position.x), groundY, movers.Average(m => m.transform.position.z));

        var center = new Vector3(focus.x + crop.offset.x, groundY, focus.z + crop.offset.y);
        float half = crop.size * 0.5f;
        var rect = new Rect(center.x - half, center.z - half, crop.size, crop.size);

        // nothing taller than this stands on the island (keeps neighbours on the table clear)
        ClipTop = groundY + crop.size * MaxHeightFraction;

        // collect geometry by material
        var groups = new Dictionary<Material, MeshBuild>();
        int used = 0;
        foreach (var root in roots)
        {
            if (!root.activeInHierarchy || SkipRoots.Contains(root.name)) continue;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!mr.enabled || mr.gameObject.layer == 5) continue;
                if (mr.GetComponentInParent<Camera>() || mr.GetComponentInParent<Canvas>()) continue;
                if (!InLod0(mr)) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mr.bounds;
                if (b.max.x < rect.xMin || b.min.x > rect.xMax || b.max.z < rect.yMin || b.min.z > rect.yMax) continue;
                if (b.max.y < groundY - 12f || b.min.y > groundY + 30f) continue;   // backdrops far below / skyboxes
                if (Mathf.Max(b.size.x, b.size.z) > 400f) continue;                // giant backdrop planes

                var mesh = mf.sharedMesh;
                var mats = mr.sharedMaterials;
                if (!TryRead(mesh, out var verts, out var normals, out var uvs, out var colors)) continue;
                var l2w = mr.transform.localToWorldMatrix;
                bool flip = l2w.determinant < 0f;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    var mat = mats[s];
                    if (mat == null || mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                    if (!groups.TryGetValue(mat, out var g)) groups[mat] = g = new MeshBuild();
                    var tris = mesh.GetTriangles(s);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        int a = tris[t], bb = tris[t + 1], c = tris[t + 2];
                        if (flip) { int tmp = bb; bb = c; c = tmp; }
                        g.AddClipped(rect, center,
                            V(l2w, verts, normals, uvs, colors, a),
                            V(l2w, verts, normals, uvs, colors, bb),
                            V(l2w, verts, normals, uvs, colors, c));
                    }
                }
                used++;
            }
        }

        // terrain -> mesh
        foreach (var terrain in roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(false)))
            BakeTerrain(terrain, rect, center, groundY, groups, crop.scene);

        string dir = $"{Folder}/{crop.scene}";
        Directory.CreateDirectory(dir);
        string meshPath = $"{dir}/Mini_{crop.scene}_Meshes.asset";
        AssetDatabase.DeleteAsset(meshPath);

        var go = new GameObject($"Mini_{crop.scene}");
        Mesh container = null;
        int idx = 0;
        foreach (var kv in groups)
        {
            if (kv.Value.Count == 0) continue;
            var m = kv.Value.ToMesh($"{crop.scene}_{idx}_{kv.Key.name}");
            if (container == null) { AssetDatabase.CreateAsset(m, meshPath); container = m; }
            else AssetDatabase.AddObjectToAsset(m, container);
            var part = new GameObject(kv.Key.name);
            part.transform.SetParent(go.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = m;
            var r = part.AddComponent<MeshRenderer>();
            r.sharedMaterial = kv.Key;
            r.shadowCastingMode = ShadowCastingMode.On;
            idx++;
        }

        // the floating earth beneath
        var baseMesh = IslandBase(crop.size, crop.scene.GetHashCode());
        baseMesh.name = $"{crop.scene}_IslandBase";
        if (container == null) { AssetDatabase.CreateAsset(baseMesh, meshPath); container = baseMesh; }
        else AssetDatabase.AddObjectToAsset(baseMesh, container);
        var baseGo = new GameObject("IslandBase");
        baseGo.transform.SetParent(go.transform, false);
        baseGo.AddComponent<MeshFilter>().sharedMesh = baseMesh;
        baseGo.AddComponent<MeshRenderer>().sharedMaterial = EarthMaterial(crop.scene, groups);

        var info = go.AddComponent<MapMiniature>();
        info.sceneName = crop.scene;
        info.size = crop.size;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(crop.scene));
        Object.DestroyImmediate(go);
        Debug.Log($"[MiniatureBaker] {crop.scene}: {used} renderers, {groups.Count} materials, center {center}, ground {groundY:0.0}");
    }

    static bool InLod0(Renderer r)
    {
        var lod = r.GetComponentInParent<LODGroup>();
        if (lod == null) return true;
        var lods = lod.GetLODs();
        if (lods.Length == 0) return true;
        if (lods[0].renderers.Contains(r)) return true;
        return !lods.Any(l => l.renderers.Contains(r));
    }

    static bool TryRead(Mesh mesh, out Vector3[] v, out Vector3[] n, out Vector2[] uv, out Color[] col)
    {
        v = null; n = null; uv = null; col = null;
        try
        {
            using var data = Mesh.AcquireReadOnlyMeshData(mesh);
            var md = data[0];
            var va = new Unity.Collections.NativeArray<Vector3>(md.vertexCount, Unity.Collections.Allocator.Temp);
            md.GetVertices(va); v = va.ToArray(); va.Dispose();
            if (md.HasVertexAttribute(VertexAttribute.Normal))
            {
                var na = new Unity.Collections.NativeArray<Vector3>(md.vertexCount, Unity.Collections.Allocator.Temp);
                md.GetNormals(na); n = na.ToArray(); na.Dispose();
            }
            if (md.HasVertexAttribute(VertexAttribute.TexCoord0) && md.GetVertexAttributeDimension(VertexAttribute.TexCoord0) == 2)
            {
                var ua = new Unity.Collections.NativeArray<Vector2>(md.vertexCount, Unity.Collections.Allocator.Temp);
                md.GetUVs(0, ua); uv = ua.ToArray(); ua.Dispose();
            }
            if (md.HasVertexAttribute(VertexAttribute.Color))
            {
                var ca = new Unity.Collections.NativeArray<Color>(md.vertexCount, Unity.Collections.Allocator.Temp);
                md.GetColors(ca); col = ca.ToArray(); ca.Dispose();
            }
            return v.Length > 0;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MiniatureBaker] can't read {mesh.name}: {e.Message}");
            return false;
        }
    }

    struct Vtx { public Vector3 p, n; public Vector2 uv; public Color c; }

    static Vtx V(Matrix4x4 m, Vector3[] v, Vector3[] n, Vector2[] uv, Color[] c, int i) => new Vtx
    {
        p = m.MultiplyPoint3x4(v[i]),
        n = n != null ? m.MultiplyVector(n[i]).normalized : Vector3.up,
        uv = uv != null ? uv[i] : Vector2.zero,
        c = c != null ? c[i] : Color.white,
    };

    static Vtx Lerp(Vtx a, Vtx b, float t) => new Vtx
    {
        p = Vector3.Lerp(a.p, b.p, t),
        n = Vector3.Lerp(a.n, b.n, t).normalized,
        uv = Vector2.Lerp(a.uv, b.uv, t),
        c = Color.Lerp(a.c, b.c, t),
    };

    class MeshBuild
    {
        readonly List<Vector3> p = new();
        readonly List<Vector3> n = new();
        readonly List<Vector2> uv = new();
        readonly List<Color> c = new();
        readonly List<int> tris = new();
        public int Count => tris.Count;

        /// Clips the triangle to the square (Sutherland–Hodgman on the four sides) and adds it
        public void AddClipped(Rect r, Vector3 origin, Vtx a, Vtx b, Vtx cc)
        {
            var poly = new List<Vtx>(6) { a, b, cc };
            poly = Clip(poly, v => v.p.x - r.xMin);
            poly = Clip(poly, v => r.xMax - v.p.x);
            poly = Clip(poly, v => v.p.z - r.yMin);
            poly = Clip(poly, v => r.yMax - v.p.z);
            poly = Clip(poly, v => ClipTop - v.p.y);   // a flat lid: tall trees/spires are sliced off
            if (poly.Count < 3) return;
            int start = p.Count;
            foreach (var v in poly)
            {
                p.Add(v.p - origin); n.Add(v.n); uv.Add(v.uv); c.Add(v.c);
            }
            for (int i = 1; i < poly.Count - 1; i++) { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
        }

        public void AddTri(Vtx a, Vtx b, Vtx cc, Vector3 origin)
        {
            int start = p.Count;
            foreach (var v in new[] { a, b, cc }) { p.Add(v.p - origin); n.Add(v.n); uv.Add(v.uv); c.Add(v.c); }
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        static List<Vtx> Clip(List<Vtx> poly, System.Func<Vtx, float> inside)
        {
            if (poly.Count == 0) return poly;
            var outp = new List<Vtx>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                var cur = poly[i];
                var prev = poly[(i + poly.Count - 1) % poly.Count];
                float dc = inside(cur), dp = inside(prev);
                if (dc >= 0f)
                {
                    if (dp < 0f) outp.Add(Lerp(prev, cur, dp / (dp - dc)));
                    outp.Add(cur);
                }
                else if (dp >= 0f) outp.Add(Lerp(prev, cur, dp / (dp - dc)));
            }
            return outp;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = p.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(p); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(c);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    static void BakeTerrain(Terrain terrain, Rect rect, Vector3 origin, float groundY, Dictionary<Material, MeshBuild> groups, string scene)
    {
        var td = terrain.terrainData;
        var tp = terrain.transform.position;
        var tRect = new Rect(tp.x, tp.z, td.size.x, td.size.z);
        if (!tRect.Overlaps(rect)) return;

        var layer = td.terrainLayers.FirstOrDefault(l => l != null);
        var mat = TerrainMaterial(scene, layer);
        if (!groups.TryGetValue(mat, out var g)) groups[mat] = g = new MeshBuild();

        const int Res = 60;
        Vector2 tile = layer != null ? layer.tileSize : new Vector2(10, 10);
        Vtx At(int i, int j)
        {
            float x = rect.xMin + rect.width * i / Res, z = rect.yMin + rect.height * j / Res;
            var w = new Vector3(x, 0, z);
            w.y = terrain.SampleHeight(w) + tp.y;
            var nrm = td.GetInterpolatedNormal((x - tp.x) / td.size.x, (z - tp.z) / td.size.z);
            return new Vtx { p = w, n = nrm, uv = new Vector2(x / tile.x, z / tile.y), c = Color.white };
        }
        for (int i = 0; i < Res; i++)
            for (int j = 0; j < Res; j++)
            {
                var a = At(i, j); var b = At(i, j + 1); var c = At(i + 1, j + 1); var d = At(i + 1, j);
                if (!tRect.Contains(new Vector2(a.p.x, a.p.z))) continue;
                g.AddTri(a, b, c, origin);
                g.AddTri(a, c, d, origin);
            }

        // skirt the four edges down into the earth below so hills don't float
        float bottom = groundY - 1.2f;
        for (int k = 0; k < Res; k++)
        {
            Skirt(g, At(k, 0), At(k + 1, 0), bottom, origin);
            Skirt(g, At(Res, k), At(Res, k + 1), bottom, origin);
            Skirt(g, At(k + 1, Res), At(k, Res), bottom, origin);
            Skirt(g, At(0, k + 1), At(0, k), bottom, origin);
        }
    }

    static void Skirt(MeshBuild g, Vtx a, Vtx b, float bottom, Vector3 origin)
    {
        var a2 = a; a2.p.y = bottom; var b2 = b; b2.p.y = bottom;
        var nrm = Vector3.Cross(b.p - a.p, Vector3.up).normalized;
        a.n = b.n = a2.n = b2.n = nrm;
        g.AddTri(a, a2, b2, origin);
        g.AddTri(a, b2, b, origin);
    }

    static Material TerrainMaterial(string scene, TerrainLayer layer)
    {
        string path = $"{Folder}/{scene}/Mini_{scene}_Terrain.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Directory.CreateDirectory($"{Folder}/{scene}");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        if (layer != null && layer.diffuseTexture != null) mat.SetTexture("_BaseMap", layer.diffuseTexture);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- the floating chunk of earth ----------

    /// A jagged low-poly rock hanging under a square of the given side, flat shaded.
    /// Top at y = -0.05 (just under the ground), tapering to a point ~0.55×side below.
    public static Mesh IslandBase(float side, int seed)
    {
        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();
        const int Around = 24;   // points around each ring (6 per side of the square)
        float[] depths = { 0f, 0.07f, 0.2f, 0.36f, 0.5f };
        float[] shrink = { 1f, 0.97f, 0.78f, 0.48f, 0.2f };
        var rings = new List<Vector3[]>();
        for (int r = 0; r < depths.Length; r++)
        {
            var ring = new Vector3[Around];
            for (int i = 0; i < Around; i++)
            {
                // perimeter of the square, walked evenly
                float t = i / (float)Around * 4f;
                int sideIdx = Mathf.FloorToInt(t); float f = t - sideIdx;
                Vector2 p = sideIdx switch
                {
                    0 => new Vector2(-1 + 2 * f, -1),
                    1 => new Vector2(1, -1 + 2 * f),
                    2 => new Vector2(1 - 2 * f, 1),
                    _ => new Vector2(-1, 1 - 2 * f),
                };
                float s = shrink[r] * (r == 0 ? 1f : 0.9f + 0.18f * R());
                if (r > 0) p = Vector2.Lerp(p, p.normalized * 1.15f, 0.25f * r / depths.Length);   // round off toward the tip
                float y = -0.05f - depths[r] * side * (r == 0 ? 1f : 0.85f + 0.3f * R());
                ring[i] = new Vector3(p.x * side * 0.5f * s, y, p.y * side * 0.5f * s);
            }
            rings.Add(ring);
        }
        var tip = new Vector3((R() - 0.5f) * side * 0.08f, -side * 0.62f, (R() - 0.5f) * side * 0.08f);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int s0 = verts.Count; verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(s0); tris.Add(s0 + 1); tris.Add(s0 + 2);
        }
        for (int r = 0; r < rings.Count - 1; r++)
            for (int i = 0; i < Around; i++)
            {
                int j = (i + 1) % Around;
                var a = rings[r][i]; var b = rings[r][j]; var c = rings[r + 1][j]; var d = rings[r + 1][i];
                Tri(a, c, b); Tri(a, d, c);
            }
        var last = rings[rings.Count - 1];
        for (int i = 0; i < Around; i++) Tri(last[i], tip, last[(i + 1) % Around]);
        // lid, in case the surface has gaps
        var top = rings[0];
        var mid = new Vector3(0, -0.05f, 0);
        for (int i = 0; i < Around; i++) Tri(top[i], top[(i + 1) % Around], mid);

        var m = new Mesh();
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static Material EarthMaterial(string scene, Dictionary<Material, MeshBuild> groups)
    {
        string path = $"{Folder}/IslandEarth.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Directory.CreateDirectory(Folder);
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.24f, 0.18f, 0.15f));
            mat.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }
}
