using System;
using System.Collections.Generic;
using UnityEngine;
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
    }

    /// First match wins. Keep the specific stems above general ones.
    public static readonly Rule[] Rules =
    {
        // boom
        new Rule { stems = new[] { "barrel", "keg" }, material = PropMaterial.Wood, toughness = 0.6f, explodeDamage = 35f, explodeRadius = 3.5f },
        // wood
        new Rule { stems = new[] { "box_stacked", "crate", "chest", "trunk", "table", "chair", "fence_wood", "catapult", "trees_a_cut" }, material = PropMaterial.Wood },
        new Rule { stems = new[] { "commontree", "pine", "deadtree", "twistedtree" }, material = PropMaterial.Wood, toughness = 1.4f },
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

    /// Never destructible, even if a rule matches (arena shape, landmarks, floors)
    static readonly string[] Never =
    {
        "wall", "building", "stairs", "ground", "floor", "terrain", "plane", "ramp", "lava", "water",
        "lake", "river", "light", "backdrop", "progress", "environment", "bridge", "tower", "castle",
    };

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
        return null;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Enabled) return;
        // gameplay maps only (the menus have no WinManager / wave spawner)
        if (UnityEngine.Object.FindAnyObjectByType<WinManager>() == null &&
            UnityEngine.Object.FindAnyObjectByType<GoblinSpawner>() == null) return;

        int made = 0;
        var seen = new HashSet<GameObject>();
        foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (col.isTrigger || col.gameObject.scene != scene) continue;
            var go = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (!seen.Add(go)) continue;
            if (go.GetComponentInParent<Destructible>() != null) continue;
            if (!Eligible(go, col)) continue;

            var rule = RuleFor(go.name);
            if (rule == null) continue;
            var b = col.bounds;
            if (Mathf.Max(b.size.x, b.size.z) > MaxFootprint || b.size.magnitude < MinSize) continue;

            var d = go.AddComponent<Destructible>();
            d.material = rule.material;
            d.SetMaxHealth(Mathf.Round(Mathf.Clamp(BaseHealth(d.Bounds) * rule.toughness, 5f, MaxHealth)));
            d.explodeDamage = rule.explodeDamage;
            d.explodeRadius = rule.explodeRadius;
            d.debrisColor = rule.debris;
            made++;
        }
        if (made > 0) Debug.Log($"[Destructibles] {made} props in {scene.name} can be destroyed");
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
