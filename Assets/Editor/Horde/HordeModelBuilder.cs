using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Authors the horde's meshes in the existing KayKit bind pose. No runtime mesh generation.</summary>
public static class HordeModelBuilder
{
    public const string Output = "Assets/Art/Horde";
    const string GoblinGuid = "8055dbf9c249f3748974801fd1b86274";
    const string KnightGuid = "a30cab616b80f9d42a854cffbcbf6ed2";
    // Palette: skin, shadowed skin, bone, recess, cloth, dark cloth, iron, iron edge, rust, ember, leather, wrap.
    static readonly string[] Palette = { "8EAA91", "536D66", "E2D3A3", "182323", "773C42", "422D37", "303F4B", "60717A", "AF7845", "FFB640", "4B3930", "A69877" };

    [MenuItem("Magnumancer/Art/Rebuild Horde Models")]
    public static void BuildAndApply()
    {
        Build();
        Apply("Assets/Prefabs/Goblin.prefab", "Ashwalker");
        Apply("Assets/Prefabs/LargeBoss.prefab", "Cinderbound");
        AssetDatabase.SaveAssets();
    }

    public static void Build()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var mat = MakeMaterial();
        BuildCharacter(GoblinGuid, "Goblin", "Ashwalker", false, mat);
        BuildCharacter(KnightGuid, "Knight", "Cinderbound", true, mat);
        AssetDatabase.SaveAssets();
    }

    static Material MakeMaterial()
    {
        foreach (bool emission in new[] { false, true })
        {
            var tex = new Texture2D(16, 1, TextureFormat.RGB24, false);
            for (int i = 0; i < 16; i++)
            {
                ColorUtility.TryParseHtmlString("#" + Palette[Math.Min(i, Palette.Length - 1)], out var color);
                tex.SetPixel(i, 0, emission && i != 9 ? Color.black : color);
            }
            tex.Apply();
            string path = Output + (emission ? "/HordeEmission.png" : "/HordePalette.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Horde.mat");
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, Output + "/Horde.mat"); }
        material.shader = shader;
        var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/HordePalette.png");
        material.SetTexture(material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", palette);
        material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", Color.white);
        material.SetFloat("_Metallic", .05f);
        material.SetFloat(material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness", .18f);
        material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/HordeEmission.png"));
        material.SetColor("_EmissionColor", Color.white * 1.6f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        material.EnableKeyword("_EMISSION");
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static string SourcePath(string guid, string fallback)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? "Assets/" + fallback + ".fbx" : path;
    }

    static void BuildCharacter(string guid, string fallback, string name, bool heavy, Material material)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath(guid, fallback));
        if (!source) throw new InvalidOperationException("Missing source rig: " + fallback);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string part = renderer.name.Substring(renderer.name.IndexOf('_') + 1);
                var mesh = new Sculpt(renderer, root.transform);
                switch (part)
                {
                    case "Head": Head(mesh, heavy); break;
                    case "Body": Body(mesh, heavy); break;
                    case "ArmLeft": Arm(mesh, -1, heavy); break;
                    case "ArmRight": Arm(mesh, 1, heavy); break;
                    case "LegLeft": Leg(mesh, -1, heavy); break;
                    case "LegRight": Leg(mesh, 1, heavy); break;
                    default: throw new InvalidOperationException("Unexpected rig part: " + part);
                }
                Mesh result = mesh.Finish(name + "_" + part);
                string path = Output + "/" + result.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(result, existing); Object.DestroyImmediate(result); result = existing; }
                else AssetDatabase.CreateAsset(result, path);
                renderer.sharedMesh = result;
                renderer.sharedMaterials = new[] { material };
                renderer.localBounds = result.bounds;
                EditorUtility.SetDirty(result);
            }
            // Sample the controller's three motions into a conservative culling envelope.
            // This costs nothing at runtime and avoids clipping extended claws during a stride.
            var clips = AssetDatabase.LoadAllAssetsAtPath(SourcePath(guid, fallback)).OfType<AnimationClip>();
            var parts = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var baked = new Mesh();
            try
            {
                foreach (string motion in new[] { "Idle", "Walking_B", "Running_A" })
                {
                    var clip = clips.First(c => c.name == motion);
                    for (int frame = 0; frame <= 16; frame++)
                    {
                        clip.SampleAnimation(root, clip.length * frame / 16);
                        foreach (var part in parts)
                        {
                            part.BakeMesh(baked);
                            var bounds = part.sharedMesh.bounds;
                            bounds.Encapsulate(baked.bounds);
                            part.sharedMesh.bounds = bounds;
                        }
                    }
                }
                foreach (var part in parts)
                {
                    var bounds = part.sharedMesh.bounds;
                    bounds.Expand(.08f);
                    part.sharedMesh.bounds = bounds;
                    EditorUtility.SetDirty(part.sharedMesh);
                }
            }
            finally { Object.DestroyImmediate(baked); }
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void Apply(string path, string name)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Stock helmets, weapons, and shields are separate rigid meshes.
            foreach (var accessory in root.GetComponentsInChildren<MeshRenderer>(true)) accessory.enabled = false;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string part = r.name.Substring(r.name.IndexOf('_') + 1);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Output + "/" + name + "_" + part + ".asset");
                if (!mesh) throw new InvalidOperationException("Missing horde mesh: " + part);
                r.sharedMesh = mesh;
                r.sharedMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>(Output + "/Horde.mat") };
                r.localBounds = mesh.bounds;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static void Head(Sculpt m, bool heavy)
    {
        const string b = "head";
        // Skull leans forward; cheek planes and a separate jaw leave a deep mouth opening.
        m.Gem(V(0, 1.64f, .07f), V(.36f, .39f, .29f), heavy ? 1 : 0, b);
        m.Gem(V(0, 1.91f, .035f), V(.31f, .10f, .255f), heavy ? 0 : 1, b);
        m.Gem(V(0, 1.40f, .18f), V(.255f, .105f, .22f), 1, b);
        m.Box(V(0, 1.48f, .336f), V(.38f, .13f, .037f), 3, b);
        // Angular eye sockets and narrow amber eyes survive the pixelated game camera.
        for (int s = -1; s <= 1; s += 2)
        {
            m.Gem(V(s * .164f, 1.705f, .312f), V(.133f, .10f, .044f), 3, b);
            m.Box(V(s * .164f, 1.708f, .354f), V(.126f, .046f, .014f), 9, b, V(0, 0, s * -12));
            m.Box(V(s * .163f, 1.788f, .327f), V(.255f, .065f, .07f), heavy ? 2 : 1, b, V(0, 0, s * 14));
            m.Gem(V(s * .253f, 1.565f, .29f), V(.086f, .096f, .075f), 2, b);
            // Torn pointed ears, retaining the goblin ancestry.
            m.Spike(V(s * .31f, 1.71f, .04f), V(s * (heavy ? .45f : .51f), 1.79f, -.005f), .09f, 1, b);
            m.Box(V(s * .16f, 1.46f, .367f), V(.053f, .10f, .052f), 2, b, V(0, 0, s * 9));
        }
        m.Spike(V(0, 1.66f, .325f), V(0, 1.565f, .40f), .055f, 1, b);
        for (int i = 0; i < 5; i++)
            if (i != 1) m.Box(V((i - 2) * .054f, 1.51f, .368f), V(.04f, i == 3 ? .034f : .05f, .028f), 2, b);
        // Visible scars and one missing patch of scalp.
        m.Box(V(-.13f, 1.90f, .21f), V(.035f, .15f, .018f), 3, b, V(-25, 0, -24));
        if (!heavy)
        {
            m.Box(V(.265f, 1.86f, .13f), V(.09f, .13f, .24f), 11, b, V(0, 0, -20));
            return;
        }
        // Broken iron crown: open face, asymmetric silhouette, tarnished rim.
        m.Box(V(0, 1.97f, .035f), V(.67f, .085f, .45f), 6, b);
        m.Box(V(0, 1.945f, .276f), V(.65f, .033f, .023f), 8, b);
        for (int i = -2; i <= 2; i++)
            m.Spike(V(i * .14f, 1.995f, .075f), V(i * .175f, 2.14f + (i == -2 ? .10f : i == 1 ? -.04f : .04f), .025f), .065f, i % 2 == 0 ? 6 : 8, b);
        m.Gem(V(0, 1.985f, .285f), V(.054f, .065f, .027f), 9, b);
    }

    static void Body(Sculpt m, bool heavy)
    {
        m.Gem(V(0, .63f, 0), V(.25f, .22f, .18f), 5, "hips");
        m.Gem(V(0, .87f, -.025f), V(heavy ? .36f : .28f, .30f, heavy ? .23f : .19f), heavy ? 6 : 0, "chest");
        m.Gem(V(0, 1.13f, -.09f), V(.30f, .14f, .19f), heavy ? 6 : 1, "chest");
        m.Gem(V(0, 1.245f, .035f), V(.12f, .14f, .13f), 1, "head");
        // Exposed, recessed rib cage with bent bone bars, or furnace ribs on the brute.
        m.Gem(V(.025f, .91f, .185f), V(.21f, .22f, .045f), 3, "chest");
        for (int row = 0; row < 3; row++)
        for (int s = -1; s <= 1; s += 2)
            m.Box(V(.02f + s * (.089f - row * .013f), 1.055f - row * .098f, .233f), V(.153f - row * .02f, heavy ? .045f : .035f, .04f), heavy ? 9 : 2, "chest", V(0, s * 12, s * -16));
        m.Box(V(.02f, .97f, .252f), V(.038f, .30f, .04f), heavy ? 8 : 2, "chest");
        // Leather cross strap and buckle; hem is made of overlapping pointed cloth panels.
        m.Box(V(-.18f, .99f, .16f), V(.075f, .45f, .05f), 10, "chest", V(0, -18, -19));
        m.Box(V(0, .625f, .185f), V(.47f, .085f, .035f), 10, "hips");
        m.Box(V(-.02f, .63f, .215f), V(.09f, .085f, .028f), 8, "hips");
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2 / 7;
            var c = V(Mathf.Sin(a) * .23f, .49f, Mathf.Cos(a) * .18f);
            m.Cloth(c, .19f, .26f + (i % 3) * .07f, i % 2 == 0 ? 4 : 5, "hips", a * Mathf.Rad2Deg);
        }
        if (heavy)
        {
            m.Gem(V(-.285f, 1.075f, .045f), V(.14f, .24f, .235f), 6, "chest");
            m.Gem(V(.285f, 1.075f, .045f), V(.14f, .24f, .235f), 6, "chest");
            m.Box(V(0, 1.18f, .225f), V(.46f, .07f, .07f), 8, "chest");
            // Spine plates keep the rear view identifiable too.
            for (int i = 0; i < 4; i++) m.Gem(V(0, .82f + i * .10f, -.235f), V(.11f, .075f, .065f), 7, "chest");
        }
        else
        {
            m.Cloth(V(.23f, 1.01f, -.12f), .21f, .47f, 4, "chest", 155);
            for (int i = 0; i < 3; i++) m.Gem(V(0, .91f + i * .105f, -.218f), V(.054f, .06f, .036f), 2, "chest");
        }
    }

    static void Arm(Sculpt m, int s, bool heavy)
    {
        string side = s < 0 ? ".l" : ".r";
        string upper = "upperarm" + side, lower = "lowerarm" + side, hand = "hand" + side;
        m.Limb(V(s * .225f, 1.107f, 0), V(s * .455f, 1.107f, -.014f), heavy ? .145f : .10f, .078f, 0, upper);
        m.Gem(V(s * .455f, 1.107f, -.014f), V(.078f, .083f, .083f), 1, lower);
        m.Limb(V(s * .465f, 1.107f, -.014f), V(s * .733f, 1.107f, 0), heavy ? .13f : .076f, .06f, heavy ? 6 : 0, lower);
        m.Gem(V(s * .792f, 1.10f, .015f), V(.12f, .08f, .11f), heavy ? 1 : 0, hand);
        // Three separated, hooked fingers and an opposing thumb instead of mittens.
        for (int i = 0; i < 3; i++)
        {
            float z = (i - 1) * .07f;
            var start = V(s * .84f, 1.10f, z);
            var knuckle = V(s * (.96f - Math.Abs(i - 1) * .018f), 1.075f, z);
            m.Limb(start, knuckle, .033f, .024f, 0, hand);
            m.Spike(knuckle, V(knuckle.x + s * .025f, 1.012f, z + .008f), .026f, 2, hand);
        }
        m.Limb(V(s * .77f, 1.08f, .08f), V(s * .82f, 1.015f, .13f), .04f, .027f, 1, hand);
        if (heavy || s < 0)
        {
            m.Gem(V(s * .31f, 1.215f, -.015f), V(heavy ? .245f : .18f, heavy ? .14f : .085f, heavy ? .235f : .17f), 6, upper);
            m.Box(V(s * .34f, 1.215f, .175f), V(.26f, .065f, .035f), 8, upper);
            for (int i = 0; i < (heavy ? 3 : 1); i++)
                m.Spike(V(s * (.22f + i * .095f), 1.31f, -.025f), V(s * (.245f + i * .13f), 1.48f + (i == 1 ? .05f : 0), -.035f), .055f, heavy ? 2 : 8, upper);
        }
        if (!heavy && s > 0)
        {
            for (int i = 0; i < 3; i++) m.Limb(V(.53f + i * .05f, 1.107f, -.008f), V(.555f + i * .05f, 1.107f, -.005f), .083f, .081f, 11, lower);
        }
        if (heavy)
        {
            m.Box(V(s * .59f, 1.18f, .012f), V(.23f, .055f, .23f), 7, lower);
            m.Limb(V(s * .69f, 1.107f, 0), V(s * .725f, 1.107f, 0), .088f, .086f, 8, lower);
        }
    }

    static void Leg(Sculpt m, int s, bool heavy)
    {
        string side = s < 0 ? ".l" : ".r";
        m.Limb(V(s * .171f, .52f, 0), V(s * .171f, .29f, .008f), .103f, .083f, 5, "upperleg" + side);
        m.Gem(V(s * .171f, .285f, .032f), V(.091f, .084f, .085f), heavy ? 8 : 2, "lowerleg" + side);
        m.Limb(V(s * .171f, .28f, .008f), V(s * .171f, .12f, -.019f), .07f, .059f, heavy ? 6 : 1, "lowerleg" + side);
        m.Gem(V(s * .171f, .075f, .065f), V(.12f, .085f, .205f), heavy || s < 0 ? 6 : 0, "foot" + side);
        if (heavy || s < 0)
        {
            m.Box(V(s * .171f, .075f, .225f), V(.21f, .065f, .038f), 7, "foot" + side);
            m.Box(V(s * .171f, .205f, .075f), V(.145f, .20f, .065f), heavy ? 7 : 10, "lowerleg" + side);
        }
        else
        {
            for (int i = 0; i < 3; i++) m.Gem(V(s * .171f + (i - 1) * .065f, .054f, .24f), V(.028f, .035f, .049f), 2, "foot" + side);
            m.Limb(V(s * .171f, .22f, .008f), V(s * .171f, .19f, .002f), .078f, .075f, 11, "lowerleg" + side);
        }
    }

    // Small flat-shaded mesh authoring vocabulary. Every vertex is weighted to the source rig.
    sealed class Sculpt
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<BoneWeight> weights = new List<BoneWeight>();
        readonly SkinnedMeshRenderer renderer;
        readonly Matrix4x4 toLocal;
        readonly Dictionary<string, int> bones;
        public Sculpt(SkinnedMeshRenderer r, Transform root)
        {
            renderer = r; toLocal = r.transform.worldToLocalMatrix * root.localToWorldMatrix;
            bones = r.bones.Select((b, i) => new { b.name, i }).ToDictionary(b => b.name, b => b.i);
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c, int color, string bone)
        {
            int index = vertices.Count;
            foreach (var v in new[] { a, b, c })
            {
                vertices.Add(toLocal.MultiplyPoint3x4(v));
                uv.Add(new Vector2((color + .5f) / 16, .5f));
                weights.Add(new BoneWeight { boneIndex0 = bones[bone], weight0 = 1 });
            }
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int col, string bone)
        { Tri(a, b, c, col, bone); Tri(a, c, d, col, bone); }
        public void Box(Vector3 center, Vector3 size, int col, string bone, Vector3 rotation = default)
        {
            var q = Quaternion.Euler(rotation); var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = center + q * Vector3.Scale(size * .5f, V((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Quad(v[0], v[2], v[3], v[1], col, bone); Quad(v[4], v[5], v[7], v[6], col, bone);
            Quad(v[0], v[4], v[6], v[2], col, bone); Quad(v[1], v[3], v[7], v[5], col, bone);
            Quad(v[2], v[6], v[7], v[3], col, bone); Quad(v[0], v[1], v[5], v[4], col, bone);
        }
        public void Gem(Vector3 center, Vector3 radius, int col, string bone)
        {
            const int sides = 8;
            var rings = new Vector3[4, sides];
            float[] heights = { -1, -.57f, .52f, 1 }; float[] widths = { .48f, 1, 1, .60f };
            for (int y = 0; y < 4; y++) for (int i = 0; i < sides; i++)
            { float a = (i + .5f) * Mathf.PI * 2 / sides; rings[y, i] = center + Vector3.Scale(radius, V(Mathf.Sin(a) * widths[y], heights[y], Mathf.Cos(a) * widths[y])); }
            for (int y = 0; y < 3; y++) for (int i = 0; i < sides; i++)
            { int j = (i + 1) % sides; Quad(rings[y, i], rings[y, j], rings[y + 1, j], rings[y + 1, i], col, bone); }
            for (int i = 0; i < sides; i++)
            { int j = (i + 1) % sides; Tri(center + V(0, radius.y, 0), rings[3, i], rings[3, j], col, bone); Tri(center - V(0, radius.y, 0), rings[0, j], rings[0, i], col, bone); }
        }
        public void Limb(Vector3 a, Vector3 b, float r0, float r1, int col, string bone)
        {
            var axis = (b - a).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
            var v = Vector3.Cross(axis, u);
            for (int i = 0; i < 8; i++)
            {
                float t = i * Mathf.PI / 4, t1 = (i + 1) * Mathf.PI / 4;
                var n = u * Mathf.Cos(t) + v * Mathf.Sin(t); var n1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
                Quad(a + n * r0, a + n1 * r0, b + n1 * r1, b + n * r1, col, bone);
                Tri(a, a + n1 * r0, a + n * r0, col, bone); Tri(b, b + n * r1, b + n1 * r1, col, bone);
            }
        }
        public void Spike(Vector3 a, Vector3 b, float radius, int col, string bone) => Limb(a, b, radius, .002f, col, bone);
        public void Cloth(Vector3 center, float width, float length, int col, string bone, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var a = center + q * V(-width / 2, length / 2, 0); var b = center + q * V(width / 2, length / 2, 0);
            var c = center + q * V(width / 2, -length * .28f, .02f); var d = center + q * V(-width * .18f, -length / 2, .04f);
            var e = center + q * V(-width / 2, -length * .25f, .02f);
            Tri(a, d, b, col, bone); Tri(b, d, c, col, bone); Tri(a, e, d, col, bone);
            Tri(a, b, d, col, bone); Tri(b, c, d, col, bone); Tri(a, d, e, col, bone);
        }
        public Mesh Finish(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.boneWeights = weights.ToArray();
            mesh.bindposes = renderer.bones.Select(b => b.worldToLocalMatrix * renderer.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
