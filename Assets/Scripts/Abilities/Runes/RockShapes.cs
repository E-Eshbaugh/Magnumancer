using System.Collections.Generic;
using UnityEngine;

/// Procedural low-poly rock meshes for Granite Vow's stonework (Earthwork Parapet's crag,
/// Bastion Stance's wall).
public static class RockShapes
{
    /// Stitches stacked rings (each the same vertex count, bottom to top) into a faceted rock
    /// closed by a fan up to `cap`. Every triangle gets its own vertices so the normals come
    /// out flat — chunky facets that read as broken stone. The bottom is left open (buried).
    public static Mesh Faceted(Vector3[][] rings, Vector3 cap)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int n = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
        }
        int sides = rings[0].Length;
        for (int r = 0; r < rings.Length - 1; r++)
            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                Vector3 a = rings[r][j], b = rings[r][k], c = rings[r + 1][j], d = rings[r + 1][k];
                Tri(a, b, c);
                Tri(b, d, c);
            }
        var top = rings[rings.Length - 1];
        for (int j = 0; j < sides; j++) Tri(cap, top[j], top[(j + 1) % sides]);

        var mesh = new Mesh { name = "Rock" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// Builds a faceted rock as a child of `parent`; the mesh is freed with the root's OwnedMeshes
    public static GameObject Spawn(Transform parent, OwnedMeshes owner, string name, Vector3[][] rings, Vector3 cap,
                                   Material mat, Vector3 localPos, Quaternion localRot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        var mesh = Faceted(rings, cap);
        owner.meshes.Add(mesh);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Material darkStone, moss;

    public static Material DarkStone()
    {
        if (darkStone == null)
        {
            darkStone = new Material(RockDebris.StoneMaterial(Color.gray)) { name = "EarthStoneDark" };
            darkStone.color = new Color(0.32f, 0.28f, 0.24f);
        }
        return darkStone;
    }

    public static Material Moss()
    {
        if (moss == null)
        {
            moss = new Material(RockDebris.StoneMaterial(Color.gray)) { name = "EarthMoss" };
            moss.color = new Color(0.3f, 0.42f, 0.2f);
        }
        return moss;
    }
}
