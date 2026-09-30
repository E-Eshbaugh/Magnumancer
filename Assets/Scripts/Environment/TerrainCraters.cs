using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Real craters in maps built on Unity Terrain (Cinder Crucible, Drowned Sanctum, Fungal
/// Hollow, Riftforge): big blasts push the ground down into a bowl with a raised rim.
/// Each match works on a copy of the terrain data (made at load), so the map asset on
/// disk is never changed. Ground can't sink more than MaxDepth below where it started.
/// </summary>
public static class TerrainCraters
{
    public static bool Enabled = true;
    /// How deep a crater goes per metre of blast radius, and the most one can dig
    public static float DepthPerRadius = 0.12f;
    public static float MaxCraterDepth = 0.5f;
    /// However many blasts land in one spot, it never sinks more than this
    public static float MaxDepth = 1.2f;
    /// Rim: pushed up this fraction of the depth, out to this multiple of the radius
    public static float RimHeight = 0.25f, RimReach = 1.35f;

    class Dug { public TerrainData data; public float[,] original; }
    static readonly Dictionary<Terrain, Dug> terrains = new();

    /// Dents the terrain under `at`; returns how deep (metres, 0 = no terrain there)
    public static float Blast(Vector3 at, float radius)
    {
        if (!Enabled) return 0f;
        var t = TerrainAt(at);
        if (t == null || !terrains.TryGetValue(t, out var dug)) return 0f;

        var data = dug.data;
        Vector3 local = at - t.transform.position;
        Vector3 size = data.size;
        int res = data.heightmapResolution;
        float outer = radius * RimReach;

        // the heightmap window the crater touches
        int x0 = Mathf.Clamp(Mathf.FloorToInt((local.x - outer) / size.x * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((local.x + outer) / size.x * (res - 1)), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((local.z - outer) / size.z * (res - 1)), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((local.z + outer) / size.z * (res - 1)), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        if (w <= 1 || h <= 1) return 0f;

        float depth = Mathf.Min(MaxCraterDepth, radius * DepthPerRadius) / size.y;   // heights are 0..1
        float floor = MaxDepth / size.y;
        var heights = data.GetHeights(x0, z0, w, h);   // [z, x]
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float wx = (x0 + x) / (float)(res - 1) * size.x;
                float wz = (z0 + z) / (float)(res - 1) * size.z;
                float d = Mathf.Sqrt((wx - local.x) * (wx - local.x) + (wz - local.z) * (wz - local.z));
                float delta;
                if (d < radius)
                {
                    float k = d / radius;
                    delta = -depth * (1f - k * k);                 // smooth bowl
                }
                else if (d < outer)
                {
                    float k = (d - radius) / (outer - radius);
                    delta = depth * RimHeight * Mathf.Sin(k * Mathf.PI);   // a lip of thrown-up dirt
                }
                else continue;

                float start = dug.original[z0 + z, x0 + x];
                heights[z, x] = Mathf.Clamp(heights[z, x] + delta, start - floor, start + floor * 0.5f);
            }
        data.SetHeightsDelayLOD(x0, z0, heights);
        dirty.Add(data);
        return depth * size.y;
    }

    static readonly HashSet<TerrainData> dirty = new();

    static Terrain TerrainAt(Vector3 p)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            if (t == null) continue;
            Vector3 local = p - t.transform.position;
            var s = t.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= s.x && local.z <= s.z) return t;
        }
        return null;
    }

    // Each match digs into its own copy of the terrain
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        terrains.Clear();
        dirty.Clear();
        if (!Enabled) return;
        foreach (var t in Terrain.activeTerrains)
        {
            if (t == null || t.terrainData == null || t.gameObject.scene != scene) continue;
            var copy = Object.Instantiate(t.terrainData);
            copy.name = t.terrainData.name + " (match copy)";
            t.terrainData = copy;
            var col = t.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = copy;
            int res = copy.heightmapResolution;
            terrains[t] = new Dug { data = copy, original = copy.GetHeights(0, 0, res, res) };
        }
        EnsureRunner();
    }

    // Collision and LOD catch up once per frame, however many blasts landed
    internal static void Flush()
    {
        if (dirty.Count == 0) return;
        foreach (var d in dirty) if (d != null) d.SyncHeightmap();
        dirty.Clear();
    }

    static TerrainCraterRunner runner;
    static void EnsureRunner()
    {
        if (runner == null) runner = new GameObject("[TerrainCraters]").AddComponent<TerrainCraterRunner>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        terrains.Clear();
        dirty.Clear();
        runner = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}

[AddComponentMenu("")]
class TerrainCraterRunner : MonoBehaviour
{
    void LateUpdate() => TerrainCraters.Flush();
}
