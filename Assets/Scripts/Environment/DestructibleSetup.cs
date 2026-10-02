using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the maps' props destructible at load, with no scene edits: every solid collider
/// whose object's name matches a rule below gets a Destructible sized to it. Walls,
/// buildings, stairs, floors and anything huge are left alone, so the arena's shape
/// holds while the cover inside it wears away. Props that already have a Destructible
/// (placed by hand) keep their settings.
///
/// Map props are KayKit / Nature MegaKit / mushroom-pack models with a convex
/// MeshCollider on the root, named after the model ("rubble_large (12)").
/// </summary>
public static class DestructibleSetup
{
    /// Master switch (e.g. a match option later)
    public static bool Enabled = true;

    public class Rule
    {
        public string[] stems;          // lower-case name fragments
        public PropMaterial material;
        public float toughness = 1f;    // health multiplier
        public float explodeDamage;     // > 0: blows up
        public float explodeRadius = 3.5f;
        public Color debris;            // alpha 0 = the material's default
        public bool stub;               // breaks down to a low stub first (still cover)
        public float stubHeight = 0.35f;
    }

    /// First match wins. Keep the specific stems above general ones.
    public static readonly Rule[] Rules =
    {
        // boom
        new Rule { stems = new[] { "barrel", "keg" }, material = PropMaterial.Wood, toughness = 0.6f, explodeDamage = 35f, explodeRadius = 3.5f },
        // wood
        new Rule { stems = new[] { "box_stacked", "crate", "chest", "trunk", "table", "chair", "fence_wood", "catapult", "trees_a_cut" }, material = PropMaterial.Wood },
        new Rule { stems = new[] { "commontree", "pine", "deadtree", "twistedtree" }, material = PropMaterial.Wood, toughness = 1.4f, stub = true, stubHeight = 0.12f },   // leaves a stump
        // stone
        new Rule { stems = new[] { "rubble", "rock", "resource_stone", "pillar", "column", "fence_stone", "barrier", "statue", "tomb", "gravestone" }, material = PropMaterial.Stone, toughness = 1.3f },
        // growth
        new Rule { stems = new[] { "fly_agaric" }, material = PropMaterial.Plant, toughness = 0.7f, debris = new Color(0.85f, 0.2f, 0.15f) },
        new Rule { stems = new[] { "inky_cap" }, material = PropMaterial.Plant, toughness = 0.7f, debris = new Color(0.8f, 0.78f, 0.74f) },
        new Rule { stems = new[] { "mushroom", "plant", "bush" }, material = PropMaterial.Plant, toughness = 0.7f },
        // odds and ends
        new Rule { stems = new[] { "bottle" }, material = PropMaterial.Crystal, toughness = 0.2f },
        new Rule { stems = new[] { "crystal" }, material = PropMaterial.Crystal, toughness = 0.8f },
        new Rule { stems = new[] { "skull", "bone" }, material = PropMaterial.Bone, toughness = 0.6f },
    };

    /// Interior walls: tough, and they break down to a low stub (half cover) before they go.
    /// Walls on the arena's edge never break (see IsBoundary), and in Zombies no wall does
    /// (they gate progress).
    public static readonly Rule WallRule = new Rule
    {
        stems = new[] { "wall" }, material = PropMaterial.Stone, toughness = 2.2f, stub = true, stubHeight = 0.35f,
    };

    /// Walls this close to the outer edge of all the walls count as the arena boundary
    public static float BoundaryMargin = 3f;
    /// Stone props taller than this leave a stub too (pillars, statues)
    public static float TallStub = 2.2f;

    /// Never destructible, even if a rule matches (arena shape, landmarks, floors).
    /// Wall joints (corners, crossings) hold the layout together.
    static readonly string[] Never =
    {
        "wall_corner", "wall_crossing", "building", "stairs", "ground", "floor", "terrain", "plane", "ramp",
        "lava", "water", "lake", "river", "light", "backdrop", "progress", "environment", "bridge", "tower", "castle",
    };

    /// In builds, rebuilding the Zombies navmesh needs Read/Write on the model imports
    public static bool RebuildZombiesNavmesh = true;

    /// Health from overall size: a small rock ~70, a rubble pile ~120, a tree ~300
    public static float BaseHealth(Bounds b) => 20f + 35f * b.size.magnitude;

    /// No prop takes more than this to break
    public static float MaxHealth = 400f;

    /// Too big to be cover (landmarks), or too small to matter
    const float MaxFootprint = 9f, MinSize = 0.15f;

    public static Rule RuleFor(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (var never in Never) if (n.Contains(never)) return null;
        foreach (var r in Rules)
            foreach (var s in r.stems)
                if (n.Contains(s)) return r;
        if (n.Contains("wall")) return WallRule;
        return null;
    }

    static bool IsWall(string name) => name.ToLowerInvariant().Contains("wall");

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Enabled) return;
        // gameplay maps only (the menus have no WinManager / wave spawner)
        if (UnityEngine.Object.FindAnyObjectByType<WinManager>() == null &&
            UnityEngine.Object.FindAnyObjectByType<GoblinSpawner>() == null) return;

        bool zombies = UnityEngine.Object.FindAnyObjectByType<GoblinSpawner>() != null;

        // every solid collider's owning object, once
        var candidates = new List<(GameObject go, Collider col)>();
        var seen = new HashSet<GameObject>();
        bool anyWall = false;
        var wallRect = new Bounds();
        foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (col.isTrigger || col.gameObject.scene != scene) continue;
            var go = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (!seen.Add(go)) continue;
            candidates.Add((go, col));
            // the walls' outer edge = the arena boundary
            if (IsWall(go.name))
            {
                if (!anyWall) { wallRect = col.bounds; anyWall = true; }
                else wallRect.Encapsulate(col.bounds);
            }
        }

        var made = new List<Destructible>();
        foreach (var (go, col) in candidates)
        {
            if (go.GetComponentInParent<Destructible>() != null) continue;
            if (!Eligible(go, col)) continue;

            var rule = RuleFor(go.name);
            if (rule == null) continue;
            var b = col.bounds;
            if (Mathf.Max(b.size.x, b.size.z) > MaxFootprint || b.size.magnitude < MinSize) continue;
            if (rule == WallRule && (zombies || IsBoundary(b, wallRect))) continue;

            var d = go.AddComponent<Destructible>();
            d.material = rule.material;
            d.SetMaxHealth(Mathf.Round(Mathf.Clamp(BaseHealth(d.Bounds) * rule.toughness, 5f, MaxHealth)));
            d.explodeDamage = rule.explodeDamage;
            d.explodeRadius = rule.explodeRadius;
            d.debrisColor = rule.debris;
            d.leavesStub = rule.stub || (rule.material == PropMaterial.Stone && d.Bounds.size.y > TallStub);
            d.stubHeight = rule.stub ? rule.stubHeight : 0.35f;
            made.Add(d);
        }
        if (made.Count > 0) Debug.Log($"[Destructibles] {made.Count} props in {scene.name} can be destroyed");

        if (zombies && RebuildZombiesNavmesh) PrepareNavmesh(made);
    }

    static bool IsBoundary(Bounds wall, Bounds rect)
        => wall.min.x - rect.min.x < BoundaryMargin || rect.max.x - wall.max.x < BoundaryMargin
        || wall.min.z - rect.min.z < BoundaryMargin || rect.max.z - wall.max.z < BoundaryMargin;

    /// Zombies: the baked navmesh has holes where props stood, and a hole can't reopen at
    /// runtime. So rebuild it once at load without the props (same settings as the bake),
    /// and give every prop a carving obstacle instead: breaking it just turns that off.
    static void PrepareNavmesh(List<Destructible> props)
    {
        var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length == 0) return;

        foreach (var d in props)
        {
            if (d.GetComponentInChildren<NavMeshObstacle>() == null)
            {
                var o = d.gameObject.AddComponent<NavMeshObstacle>();
                o.shape = NavMeshObstacleShape.Box;
                var b = d.Bounds;
                Vector3 s = d.transform.lossyScale;
                o.center = d.transform.InverseTransformPoint(b.center);
                o.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)),
                                     b.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)),
                                     b.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
                o.carving = true;
                o.carveOnlyStationary = true;
            }
            d.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }
        // Progression barriers must carve a temporary obstacle, not bake a permanent
        // hole. Opening one then connects its room without another full rebuild.
        foreach (var wall in UnityEngine.Object.FindObjectsByType<DestructibleWall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var modifier = wall.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = wall.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            if (wall.GetComponentInChildren<NavMeshObstacle>(true) == null)
            {
                var collider = wall.GetComponentInChildren<Collider>(true);
                if (collider != null)
                {
                    var obstacle = collider.gameObject.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    var bounds = collider.bounds;
                    Vector3 scale = collider.transform.lossyScale;
                    obstacle.center = collider.transform.InverseTransformPoint(bounds.center);
                    obstacle.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                        bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
                    obstacle.carving = true;
                    obstacle.carveOnlyStationary = false;
                }
            }
        }
        // players standing at their spawns shouldn't punch holes either
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerMovement3D>(FindObjectsSortMode.None))
            if (p.GetComponent<NavMeshModifier>() == null) p.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        var wholeSceneSurfaces = new HashSet<(int agent, int layers, int area)>();
        foreach (var surface in surfaces)
        {
            // The Zombies scene has two whole-scene surfaces collecting identical
            // geometry. Overlaid meshes create ambiguous edges on the stairs.
            var coverage = (surface.agentTypeID, surface.layerMask.value, surface.defaultArea);
            if (surface.collectObjects == CollectObjects.All && wholeSceneSurfaces.Contains(coverage))
            {
                surface.RemoveData();
                surface.enabled = false;
                continue;
            }
            // Follow the same ramps and solid surfaces as CharacterController.
            // Finer voxels preserve stair connections and small landing platforms.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.buildHeightMesh = true;
            try
            {
                surface.BuildNavMesh();
                if (surface.navMeshData != null && surface.collectObjects == CollectObjects.All)
                    wholeSceneSurfaces.Add(coverage);
            }
            catch (Exception e) { Debug.LogWarning($"[Destructibles] Couldn't rebuild the Zombies navmesh ({e.Message}); broken props will leave their old hole."); }
        }
        Debug.Log($"[Destructibles] Rebuilt the Zombies navmesh around {props.Count} destructible props " +
                  "(in builds this needs Read/Write enabled on the prop models' import settings).");
    }

    // Leave gameplay objects alone: players, monsters, ability objects, walls with their own health
    static bool Eligible(GameObject go, Collider col)
    {
        if (DamageEvents.IsCombatant(DamageEvents.RootOf(col))) return false;
        if (go.GetComponentInParent<PlayerMovement3D>() != null) return false;
        if (go.GetComponentInParent<IceWallEffect>() != null || go.GetComponentInParent<CrystalHealth>() != null) return false;
        if (go.GetComponentInParent<DestructibleWall>() != null || go.GetComponentInParent<MineExplosionController>() != null) return false;
        if (go.CompareTag("ProgressWall")) return false;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // idempotent across play sessions
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}
