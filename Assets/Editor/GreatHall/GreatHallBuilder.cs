using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Generates the Great Hall: Assets/Scenes/GreatHall.unity plus its assets in
/// Assets/Art/GreatHall (catalog, wizard animator, the War Table's land). The hall is
/// KayKit dungeon pieces placed as prefab instances, so after generating, the scene is
/// the source of truth — move things around freely. Re-running this overwrites the scene.
///   Magnumancer > Great Hall > Build Great Hall Scene
/// </summary>
public static class GreatHallBuilder
{
    public const string ScenePath = "Assets/Scenes/GreatHall.unity";
    public const string ArtFolder = "Assets/Art/GreatHall";
    const string Kit = "Assets/Art/3DAssets/KayKit_DungeonRemastered_1.1_FREE/KayKit_DungeonRemastered_1.1_FREE/Assets/fbx(unity)/";
    const string MageFbx = "Assets/Art/3DAssets/KayKit_Adventurers_1.0_FREE/KayKit_Adventurers_1.0_FREE/Characters/fbx/Mage.fbx";

    // table land, in screen-aligned table units (x = screen right, y = screen up along the floor)
    public const float LandLength = 8.2f, LandDepth = 4.4f, TableHeight = 1.05f;

    [MenuItem("Magnumancer/Great Hall/Build Great Hall Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(ArtFolder);

        // scene opens unload loose assets, so remember paths and load them after the last one
        BuildCatalog();
        string volumePath = ArenaVolumeProfilePath();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var catalog = AssetDatabase.LoadAssetAtPath<HallCatalog>($"{ArtFolder}/HallCatalog.asset");
        var volumeProfile = string.IsNullOrEmpty(volumePath) ? null : AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
        Lighting();

        var root = new GameObject("GreatHall");
        var director = root.AddComponent<HallDirector>();
        director.catalog = catalog;

        // camera
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(root.transform, false);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.016f, 0.035f);
        cam.orthographic = true;
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.None;
        var hallCam = camGo.AddComponent<HallCamera>();
        hallCam.hallFocus = new Vector3(1.9f, 1.2f, 1.9f);
        hallCam.hallSize = 8.2f;
        hallCam.tableFocus = new Vector3(0f, TableHeight + 1.25f, 0f) + HallCamera.GroundUp * 0.15f;
        hallCam.tableSize = 3.9f;
        hallCam.transform.rotation = Quaternion.Euler(hallCam.viewEuler);
        hallCam.transform.position = hallCam.hallFocus - hallCam.transform.forward * hallCam.distance;
        cam.orthographicSize = hallCam.hallSize;
        director.hallCamera = hallCam;

        if (volumeProfile != null)
        {
            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.transform.SetParent(root.transform, false);
            vol.isGlobal = true;
            vol.sharedProfile = volumeProfile;
        }

        var hall = new GameObject("Hall").transform;
        hall.SetParent(root.transform, false);
        Floor(hall);
        Walls(hall);
        AetherCore(hall);
        Dressing(hall, catalog);

        director.table = WarTableObject(root.transform, catalog);
        director.stations = Stations(root.transform, hall, catalog);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild();
        AssetDatabase.SaveAssets();
        Debug.Log("[GreatHallBuilder] Built " + ScenePath);
    }

    // =====================================================================
    // catalog + animator
    // =====================================================================

    static HallCatalog BuildCatalog()
    {
        // wizards and weapon tiers exactly as the old book menu had them (read before the
        // catalog asset is loaded: opening a scene unloads unreferenced assets)
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var cs = Object.FindAnyObjectByType<CharacterSelectController>(FindObjectsInactive.Include);
        var ws = Object.FindAnyObjectByType<WeaponSelectControl>(FindObjectsInactive.Include);
        var wizards = cs.allWizards.ToArray();
        var initiate = ws.initiateWeapons.ToArray();
        var ascendant = ws.ascendantWeapons.ToArray();
        var archon = ws.archonWeapons.ToArray();
        var placeholder = ws.InventoryPlaceHolder;
        var heart = cs.hearts[0].GetComponent<UnityEngine.UI.Image>().sprite;
        var orb = cs.orbs[0].GetComponent<UnityEngine.UI.Image>().sprite;

        string path = $"{ArtFolder}/HallCatalog.asset";
        var cat = AssetDatabase.LoadAssetAtPath<HallCatalog>(path);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<HallCatalog>();
            AssetDatabase.CreateAsset(cat, path);
        }
        cat.wizards = wizards;
        cat.initiate = initiate;
        cat.ascendant = ascendant;
        cat.archon = archon;
        cat.placeholder = placeholder;
        cat.heart = heart;
        cat.orb = orb;

        cat.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Dadako/BitmapFonts/Pixel/Bone.fontsettings");
        cat.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Dadako/BitmapFonts/Pixel/Help.fontsettings");
        cat.wizardModel = AssetDatabase.LoadAssetAtPath<GameObject>(MageFbx);
        cat.wizardAnimator = BuildAnimator();

        // xbox glyphs from the gdb sheet, found by where they sit on it (x, y from top-left)
        const string sheet = "Assets/Art/UI/gdb-gamepad-2(all)/gdb-xbox-2.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToArray();
        Sprite At(int x, int y) => sprites.FirstOrDefault(s => s.rect.Contains(new Vector2(x, 640 - y)));
        cat.btnX = At(24, 184);
        cat.btnA = At(24, 200);
        cat.btnY = At(24, 216);
        cat.btnB = At(24, 232);
        cat.btnLB = At(344, 88);
        cat.btnRB = At(344, 104);
        cat.btnDpad = At(144, 64);
        cat.btnStick = At(216, 72);
        cat.btnMenu = At(24, 120);

        cat.modes = new[]
        {
            new HallCatalog.ModeEntry { name = "Deathmatch", description = "Every magus for themselves. Last soul standing drinks the fading light." },
            new HallCatalog.ModeEntry { name = "Team Deathmatch", description = "Two pacts, two sworn foes. Hold formation and reap their lives in tandem." },
            new HallCatalog.ModeEntry { name = "Waves", description = "Goblin tides claw up from a Dark Rift. Hold the line together until it stills." },
        };

        var m = new List<HallCatalog.MapEntry>();
        void Map(string scene, string name, string flavor, Vector2 pos, float h, Color glow)
            => m.Add(new HallCatalog.MapEntry
            {
                scene = scene, displayName = name, flavor = flavor, tablePos = pos, height = h, glow = glow,
                miniature = AssetDatabase.LoadAssetAtPath<GameObject>(MiniatureBaker.PrefabPath(scene)),
            });
        // index = DataManager.SelectedMap (same order the book menu used)
        Map("Oldwoods3D", "The Oldwoods", "The last Lost Grove. The Verdant Circle buried its traps under every root.", new Vector2(-1.05f, -1.35f), 0.05f, new Color(0.45f, 1f, 0.45f));
        Map("Stormspire", "Stormspire", "Storm-wracked peaks where the Voltborn learned to strike and vanish.", new Vector2(-1.05f, 1.35f), 0.55f, new Color(1f, 0.92f, 0.35f));
        Map("FungalHollow", "Fungal Hollow", "A grove gone to spore. The Blightward still tend what grows here.", new Vector2(-3.15f, -1.35f), 0.1f, new Color(0.75f, 1f, 0.3f));
        Map("Riftforge", "Riftforge", "A canyon fortress on the lip of a Dark Rift. Granite Vow ground.", new Vector2(1.05f, 1.35f), 0.45f, new Color(1f, 0.6f, 0.3f));
        Map("CinderCrucibleZombies", "Cinder Crucible", "The Emberguard's volcanic forge. Goblins crawl up from its depths.", new Vector2(3.15f, 1.35f), 0.5f, new Color(1f, 0.4f, 0.15f));
        Map("DrownedSanctum", "Drowned Sanctum", "Flooded ruins of the old healing springs, held by the Tidebound.", new Vector2(1.05f, -1.35f), 0f, new Color(0.3f, 0.7f, 1f));
        Map("BlackOsuary", "Black Ossuary", "Cursed tunnels where the Hollow chase the magic nobody should.", new Vector2(3.15f, -1.35f), 0.1f, new Color(0.7f, 0.35f, 1f));
        Map("Frostgrave", "Frostgrave", "A shattered glacier sanctuary. The Frostwardens never miss twice.", new Vector2(-3.15f, 1.35f), 0.6f, new Color(0.6f, 0.9f, 1f));
        cat.maps = m.ToArray();

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        return cat;
    }

    static RuntimeAnimatorController BuildAnimator()
    {
        string path = $"{ArtFolder}/HallWizard.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var clips = AssetDatabase.LoadAllAssetsAtPath(MageFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        var sm = ctrl.layers[0].stateMachine;

        AnimatorState S(string name, string clip, Vector3 pos)
        {
            var s = sm.AddState(name, pos);
            if (clips.TryGetValue(clip, out var c)) s.motion = c;
            else Debug.LogWarning($"[GreatHallBuilder] clip {clip} missing");
            return s;
        }
        void Loop(AnimatorState s)
        {
            if (s.motion is AnimationClip c && !c.isLooping)
            {
                var t = s.AddTransition(s);
                t.hasExitTime = true; t.exitTime = 0.98f; t.duration = 0.05f;
            }
        }
        void Then(AnimatorState from, AnimatorState to, float exit = 0.85f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true; t.exitTime = exit; t.duration = 0.2f;
        }

        var idle = S("Idle", "Idle", new Vector3(300, 0));
        var ready = S("ReadyIdle", "2H_Melee_Idle", new Vector3(300, 200));
        Loop(idle); Loop(ready);
        Then(S("Arrive", "Spellcast_Raise", new Vector3(0, -120)), idle);
        Then(S("Swap", "Spellcast_Shoot", new Vector3(0, -40)), idle);
        Then(S("Rune", "Interact", new Vector3(0, 40)), idle);
        Then(S("Pick", "Use_Item", new Vector3(0, 120)), idle);
        Then(S("Cheer", "Cheer", new Vector3(0, 200)), ready, 0.9f);
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static string ArenaVolumeProfilePath()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Oldwoods3D.unity", OpenSceneMode.Single);
        var v = Object.FindAnyObjectByType<Volume>();
        return v != null && v.sharedProfile != null ? AssetDatabase.GetAssetPath(v.sharedProfile) : null;
    }

    static void Lighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.22f, 0.2f, 0.34f);
        RenderSettings.ambientEquatorColor = new Color(0.14f, 0.12f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.08f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;

        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.72f, 0.74f, 1f);
        sun.intensity = 0.75f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(52f, 330f, 0f);
    }

    // =====================================================================
    // the hall
    // =====================================================================

    static GameObject Piece(string name, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".fbx");
        if (prefab == null) { Debug.LogWarning($"[GreatHallBuilder] missing piece {name}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject Model(string path, Transform parent, Vector3 pos, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"[GreatHallBuilder] missing model {path}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    const float Half = 12f;   // the hall floor runs -12..12 on x and z (6x6 KayKit tiles)
    static float[] Lanes => new[] { -10f, -6f, -2f, 2f, 6f, 10f };

    static void Floor(Transform hall)
    {
        var g = Group(hall, "Floor");
        var rnd = new System.Random(7);
        foreach (float x in Lanes)
            foreach (float z in Lanes)
                Piece("floor_tile_large", g, new Vector3(x, 0f, z), 90f * rnd.Next(4));

        // a low parapet along the open front edges, gap at the front corner for the stairs down
        var edge = Group(hall, "Parapet");
        foreach (float p in Lanes)
        {
            if (p > -8f)
            {
                Piece("barrier", edge, new Vector3(p, 0f, -Half - 0.25f), 0f);
                Piece("barrier", edge, new Vector3(-Half - 0.25f, 0f, p), 90f);
            }
        }
        foreach (float p in new[] { -8f, -4f, 0f, 4f, 8f, 12f })
        {
            Piece("barrier_column", edge, new Vector3(p, 0f, -Half - 0.25f), 0f);
            Piece("barrier_column", edge, new Vector3(-Half - 0.25f, 0f, p), 0f);
        }
        // the way in: stairs falling away at the front corner
        Piece("stairs_wide", edge, new Vector3(-Half + 0.5f, -5.1f, -Half - 4.2f), 0f, 1f);
    }

    static void Walls(Transform hall)
    {
        var g = Group(hall, "Walls");
        // +z wall (upper left on screen) and +x wall (upper right), facing into the hall
        string[] left = { "wall_window_open", "wall_shelves", "wall_arched", "wall_pillar", "wall_shelves", "wall_archedwindow_open" };
        string[] right = { "wall_archedwindow_open", "wall_shelves", "wall_pillar", "wall_arched", "wall_shelves", "wall_window_open" };
        for (int i = 0; i < 6; i++)
        {
            float p = Lanes[i];
            Piece(left[i], g, new Vector3(p, 0f, Half + 0.5f), 180f);
            Piece(right[i], g, new Vector3(Half + 0.5f, 0f, p), 270f);
        }
        Piece("wall_corner", g, new Vector3(Half, 0f, Half), 0f);
        Piece("wall_endcap", g, new Vector3(-Half - 0.5f, 0f, Half + 0.5f), 180f);
        Piece("wall_endcap", g, new Vector3(Half + 0.5f, 0f, -Half - 0.5f), 270f);

        // torches between the windows, warm light
        var lights = Group(hall, "Torches");
        foreach (float p in new[] { -8f, 0f, 8f })
        {
            var a = Piece("torch_mounted", lights, new Vector3(p, 2.6f, Half), 180f);
            Torch(a, new Vector3(p, 3.2f, Half - 0.8f));
            var b = Piece("torch_mounted", lights, new Vector3(Half, 2.6f, p), 270f);
            Torch(b, new Vector3(Half - 0.8f, 3.2f, p));
        }
        // weapons hung on the walls
        Piece("sword_shield_gold", g, new Vector3(-4f, 2.7f, Half), 180f);
        Piece("sword_shield", g, new Vector3(Half, 2.7f, -4f), 270f);
        Piece("sword_shield", g, new Vector3(4f, 2.7f, Half), 180f);
        Piece("sword_shield_gold", g, new Vector3(Half, 2.7f, 4f), 270f);
    }

    static void Torch(GameObject torch, Vector3 lightPos)
    {
        if (torch == null) return;
        var l = new GameObject("Flame").AddComponent<Light>();
        l.transform.SetParent(torch.transform.parent, false);
        l.transform.localPosition = lightPos;
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.28f);
        l.intensity = 2.2f;
        l.range = 7f;
        l.shadows = LightShadows.None;
        var f = l.gameObject.AddComponent<RandomFlicker>();
        f.baseIntensity = 2.2f; f.pulseAmplitude = 0.35f; f.pulseSpeed = 7f;
    }

    /// Scales `go` uniformly so its rendered height is `height`, standing on its position
    static void FitHeight(GameObject go, float height)
    {
        if (go == null) return;
        var b = HallFx.BoundsOf(go.transform);
        if (b.size.y < 0.001f) return;
        go.transform.localScale *= height / b.size.y;
        b = HallFx.BoundsOf(go.transform);
        go.transform.position += Vector3.up * (go.transform.position.y - b.min.y);
    }

    /// The hall's heart in the back corner: a huge crystal humming with orb energy,
    /// two dormant war-mechs bound to it, floating gems
    static void AetherCore(Transform hall)
    {
        var g = Group(hall, "AetherCore");
        var c = new Vector3(Half - 2.6f, 0f, Half - 2.6f);
        Piece("floor_tile_small_decorated", g, c, 45f, 1.4f);
        var crystal = Model("Assets/Art/3DAssets/fbxFiles/Crystal_002.fbx", g, c + Vector3.up * 0.6f, 20f, 1f);
        FitHeight(crystal, 5.2f);
        if (crystal != null)
        {
            crystal.transform.position = c + Vector3.up * 0.6f;
            Glow(crystal, new Color(0.35f, 0.85f, 1f), 1.8f);
        }
        foreach (var (pos, yaw, gem) in new[] {
                     (c + new Vector3(-2.0f, 3.4f, 0.6f), 0f, "Gem_001"),
                     (c + new Vector3(0.6f, 4.2f, -2.0f), 40f, "Hexagon_Gem_001"),
                     (c + new Vector3(-1.2f, 5.6f, -1.2f), 70f, "Octagonal_Gem_001") })
        {
            var gm = Model($"Assets/Art/3DAssets/fbxFiles/{gem}.fbx", g, pos, yaw, 0.5f);
            if (gm == null) continue;
            Glow(gm, new Color(0.6f, 0.4f, 1f), 1.5f);
            var bob = gm.AddComponent<HallBob>(); bob.amplitude = 0.25f; bob.speed = 0.8f; bob.spin = 30f;
        }
        var core = new GameObject("CoreLight").AddComponent<Light>();
        core.transform.SetParent(g, false);
        core.transform.localPosition = c + Vector3.up * 3f;
        core.type = LightType.Point;
        core.color = new Color(0.4f, 0.85f, 1f);
        core.intensity = 5f;
        core.range = 11f;
        var pulse = core.gameObject.AddComponent<LightSoftPulse>();
        pulse.minIntensity = 3.5f; pulse.maxIntensity = 6f; pulse.pulseSpeed = 0.35f;

        // sentinels: dormant mechs against the walls either side of the core
        foreach (var p in new[] { new Vector3(Half - 1.6f, 0f, Half - 6.6f), new Vector3(Half - 6.6f, 0f, Half - 1.6f) })
        {
            var mech = Model("Assets/Art/3DAssets/MechaTrooper/Package/MechaTrooper.obj", g, p, 225f, 1f);
            FitHeight(mech, 3.6f);
        }
    }

    static void Glow(GameObject go, Color color, float intensity)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var src = r.sharedMaterial;
            string path = $"{ArtFolder}/Glow_{ColorUtility.ToHtmlStringRGB(color)}_{(src != null ? src.name : "none")}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (m.shader == null || !m.shader.name.Contains("Universal")) m.shader = Shader.Find("Universal Render Pipeline/Lit");
                m.SetColor("_BaseColor", Color.Lerp(color, Color.white, 0.2f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                AssetDatabase.CreateAsset(m, path);
            }
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// Loot and battle gear around the edges
    static void Dressing(Transform hall, HallCatalog cat)
    {
        var g = Group(hall, "Loot");
        float e = Half - 0.9f;   // against the walls
        // far left corner: supplies
        Piece("crates_stacked", g, new Vector3(-Half + 1.3f, 0f, e - 0.3f), 20f);
        Piece("barrel_large", g, new Vector3(-Half + 3.8f, 0f, e), 0f);
        Piece("barrel_small_stack", g, new Vector3(-Half + 0.9f, 0f, e - 3.1f), 40f);
        // treasure heaped by the core
        Piece("chest_gold", g, new Vector3(Half - 8.8f, 0f, e), 200f);
        Piece("coin_stack_large", g, new Vector3(Half - 10.3f, 0f, e + 0.2f), 10f);
        Piece("chest_gold", g, new Vector3(e, 0f, Half - 8.8f), 250f);
        Piece("coin_stack_medium", g, new Vector3(e + 0.2f, 0f, Half - 10.3f), 60f);
        Piece("coin_stack_small", g, new Vector3(Half - 4.6f, 0f, Half - 5.2f), 0f);
        // far right corner: the armoury's overflow
        Piece("box_stacked", g, new Vector3(e - 0.4f, 0f, -Half + 1.6f), 10f, 0.75f);
        Piece("keg_decorated", g, new Vector3(e, 0f, -Half + 4.6f), 90f, 0.85f);
        // war spoils on long tables against the walls, guns laid out
        GunTable(g, cat, new Vector3(-5.2f, 0f, e - 0.2f), 90f, new[] { cat.archon.ElementAtOrDefault(0), cat.ascendant.ElementAtOrDefault(1) });
        GunTable(g, cat, new Vector3(e - 0.2f, 0f, -5.2f), 0f, new[] { cat.archon.ElementAtOrDefault(4), cat.initiate.ElementAtOrDefault(2) });
        Piece("candle_triple", g, new Vector3(-3.8f, 1.0f, e), 0f);
        Piece("candle_triple", g, new Vector3(e, 1.0f, -3.8f), 0f);
    }

    static void GunTable(Transform parent, HallCatalog cat, Vector3 pos, float yaw, WeaponData[] guns)
    {
        var t = Piece("table_long", parent, pos, yaw);
        if (t == null) return;
        var along = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        for (int i = 0; i < guns.Length; i++)
        {
            var w = guns[i];
            if (w == null || w.prefab == null) continue;
            var d = HallFx.GunDisplay(w.prefab, 1.5f, parent);
            d.name = $"Spoils {w.weaponName}";
            d.position = pos + Vector3.up * 1.12f + along * (i == 0 ? -0.9f : 0.9f);
            // lying on its side, barrel along the table
            d.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
        }
    }

    // =====================================================================
    // the War Table
    // =====================================================================

    static WarTable WarTableObject(Transform root, HallCatalog cat)
    {
        var g = new GameObject("WarTable");
        g.transform.SetParent(root, false);
        var wt = g.AddComponent<WarTable>();
        var R = HallCamera.GroundRight;
        var U = HallCamera.GroundUp;
        float yaw = Quaternion.LookRotation(R).eulerAngles.y;   // long side along screen-right

        // four long tables make the frame
        foreach (var (u, v) in new[] { (-2.05f, -1.05f), (2.05f, -1.05f), (-2.05f, 1.05f), (2.05f, 1.05f) })
        {
            var p = Piece("table_long", g.transform, R * u + U * v, yaw, 1f);
            if (p != null) p.transform.localScale = new Vector3(1.08f, TableHeight, 1.04f);
        }

        // the land on top
        var land = new GameObject("Land");
        land.transform.SetParent(g.transform, false);
        land.transform.localPosition = Vector3.up * TableHeight;
        land.transform.localRotation = Quaternion.LookRotation(U);   // local x = screen right, local z = screen up
        var mesh = LandMesh(out var tex, out var emit);
        land.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = land.AddComponent<MeshRenderer>();
        mr.sharedMaterial = LandMaterial(tex, emit);
        mr.shadowCastingMode = ShadowCastingMode.Off;

        // glowing trim around the land
        var trim = TrimMaterial();
        foreach (var (pos, size) in new[] {
                     (new Vector3(0, 0.03f, LandDepth * 0.5f + 0.05f), new Vector3(LandLength + 0.2f, 0.06f, 0.06f)),
                     (new Vector3(0, 0.03f, -LandDepth * 0.5f - 0.05f), new Vector3(LandLength + 0.2f, 0.06f, 0.06f)),
                     (new Vector3(LandLength * 0.5f + 0.05f, 0.03f, 0), new Vector3(0.06f, 0.06f, LandDepth + 0.2f)),
                     (new Vector3(-LandLength * 0.5f - 0.05f, 0.03f, 0), new Vector3(0.06f, 0.06f, LandDepth + 0.2f)) })
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.name = "Trim";
            bar.transform.SetParent(land.transform, false);
            bar.transform.localPosition = pos;
            bar.transform.localScale = size;
            bar.GetComponent<MeshRenderer>().sharedMaterial = trim;
            bar.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // stabiliser gems at the corners
        foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var gem = Model("Assets/Art/3DAssets/fbxFiles/Dipyramid_001.fbx", land.transform, new Vector3(x * (LandLength * 0.5f + 0.1f), 0.55f, z * (LandDepth * 0.5f + 0.1f)), 0f, 0.13f);
            if (gem == null) continue;
            Glow(gem, new Color(0.35f, 0.85f, 1f), 2f);
            var bob = gem.AddComponent<HallBob>(); bob.amplitude = 0.08f; bob.speed = 1.2f; bob.spin = 45f;
        }

        var light = new GameObject("TableLight").AddComponent<Light>();
        light.transform.SetParent(g.transform, false);
        light.transform.localPosition = Vector3.up * (TableHeight + 2.6f);
        light.type = LightType.Point;
        light.color = new Color(0.45f, 0.8f, 1f);
        light.intensity = 1.6f;
        light.range = 6.5f;

        wt.surface = land.transform;
        wt.islandSize = 1.2f;
        wt.floatHeight = 0.7f;
        return wt;
    }

    static Material TrimMaterial()
    {
        string path = $"{ArtFolder}/TableTrim.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.3f, 0.8f, 1f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 2.5f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material LandMaterial(Texture2D tex, Texture2D emit)
    {
        string path = $"{ArtFolder}/WarTableLand.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.15f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", emit);
        m.SetColor("_EmissionColor", Color.white * 1.6f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- the unknown land ----------

    static float Noise(float x, float y, int oct)
    {
        float s = 0f, a = 0.5f, f = 1f;
        for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i); a *= 0.5f; f *= 2.03f; }
        return s;
    }

    // where each map sits on the land, and the colour of the land around it
    static readonly (Vector2 pos, Color color, float rise)[] Biomes =
    {
        (new Vector2(-1.05f, -1.35f), new Color(0.18f, 0.36f, 0.17f), 0.05f),  // Oldwoods: deep forest
        (new Vector2(-1.05f, 1.35f), new Color(0.46f, 0.47f, 0.56f), 0.35f),    // Stormspire: slate peaks
        (new Vector2(-3.15f, -1.35f), new Color(0.36f, 0.28f, 0.4f), 0.0f),    // Fungal Hollow: spore swamp
        (new Vector2(1.05f, 1.35f), new Color(0.6f, 0.38f, 0.24f), 0.25f),      // Riftforge: red canyon
        (new Vector2(3.15f, 1.35f), new Color(0.2f, 0.15f, 0.14f), 0.2f),      // Cinder Crucible: basalt
        (new Vector2(1.05f, -1.35f), new Color(0.22f, 0.4f, 0.38f), 0.0f),     // Drowned Sanctum: marsh
        (new Vector2(3.15f, -1.35f), new Color(0.27f, 0.24f, 0.31f), 0.05f),   // Black Ossuary: ash
        (new Vector2(-3.15f, 1.35f), new Color(0.82f, 0.88f, 0.94f), 0.4f),    // Frostgrave: ice
    };

    /// Continent height at table point (x along screen-right, y along screen-up); < 0 is sea
    static float LandHeight(float x, float y, out Color biome, out float wsum)
    {
        float nx = x / (LandLength * 0.5f), ny = y / (LandDepth * 0.5f);
        float edge = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
        float n = Noise(x * 0.55f + 3f, y * 0.55f + 9f, 5);
        float c = n * 1.25f - 0.38f - Mathf.Pow(Mathf.Clamp01(edge), 4f) * 0.9f;
        biome = new Color(0.34f, 0.36f, 0.27f) * 0.6f;
        wsum = 0.6f;
        foreach (var b in Biomes)
        {
            float d = Vector2.Distance(new Vector2(x, y), b.pos);
            float w = Mathf.Exp(-d * d / 0.55f);
            biome += b.color * w;
            wsum += w;
            c += w * (0.32f + b.rise * 0.6f);   // every location stands on land
        }
        biome /= wsum;
        return c;
    }

    static Mesh LandMesh(out Texture2D tex, out Texture2D emit)
    {
        const int TW = 512, TH = 276;
        tex = new Texture2D(TW, TH, TextureFormat.RGB24, true) { name = "WarTableLand", wrapMode = TextureWrapMode.Clamp };
        emit = new Texture2D(TW, TH, TextureFormat.RGB24, true) { name = "WarTableLandGlow", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[TW * TH];
        var ex = new Color[TW * TH];
        var heights = new float[TW * TH];
        var biomes = new Color[TW * TH];
        for (int j = 0; j < TH; j++)
            for (int i = 0; i < TW; i++)
            {
                float x = (i / (TW - 1f) - 0.5f) * LandLength, y = (j / (TH - 1f) - 0.5f) * LandDepth;
                heights[j * TW + i] = LandHeight(x, y, out var b, out _);
                biomes[j * TW + i] = b;
            }
        var light = new Vector3(-0.6f, 0.75f, 0.35f).normalized;
        for (int j = 0; j < TH; j++)
            for (int i = 0; i < TW; i++)
            {
                int k = j * TW + i;
                float x = (i / (TW - 1f) - 0.5f) * LandLength, y = (j / (TH - 1f) - 0.5f) * LandDepth;
                float h = heights[k];
                Color c;
                Color e = Color.black;
                if (h <= 0f)
                {
                    // sea: dark and deep, lighter at the coast, a faint arcane grid on it
                    float depth = Mathf.Clamp01(-h * 3f);
                    c = Color.Lerp(new Color(0.11f, 0.22f, 0.27f), new Color(0.03f, 0.06f, 0.1f), depth);
                    if (h > -0.04f) c = Color.Lerp(c, new Color(0.5f, 0.62f, 0.62f), 0.5f);
                    float gx = Mathf.Abs(Mathf.Repeat(x, 0.5f) - 0.25f), gy = Mathf.Abs(Mathf.Repeat(y, 0.5f) - 0.25f);
                    if (gx > 0.243f || gy > 0.243f) e += new Color(0.05f, 0.16f, 0.22f) * (1f - depth * 0.5f);
                }
                else
                {
                    float hx = heights[j * TW + Mathf.Min(TW - 1, i + 1)] - heights[j * TW + Mathf.Max(0, i - 1)];
                    float hy = heights[Mathf.Min(TH - 1, j + 1) * TW + i] - heights[Mathf.Max(0, j - 1) * TW + i];
                    var nrm = new Vector3(-hx * 18f, 1f, -hy * 18f).normalized;
                    float shade = Mathf.Lerp(0.62f, 1.15f, Mathf.Clamp01(Vector3.Dot(nrm, light)));
                    c = biomes[k] * shade;
                    // high ground goes rocky, then snowy
                    float hi = Mathf.InverseLerp(0.45f, 0.85f, h);
                    c = Color.Lerp(c, new Color(0.55f, 0.55f, 0.6f) * shade, hi * 0.5f);
                    // contour lines like an old survey map
                    if (Mathf.Abs(Mathf.Repeat(h, 0.12f) - 0.06f) > 0.055f) c *= 0.82f;
                }
                // the Dark Rifts: thin glowing cracks across land and sea
                float r = Mathf.Abs(Noise(x * 0.9f + 40f, y * 0.9f + 11f, 3) - 0.5f);
                if (r < 0.006f && Noise(x * 0.4f + 70f, y * 0.4f, 2) > 0.5f) { c = Color.Lerp(c, new Color(0.15f, 0.05f, 0.2f), 0.6f); e += new Color(0.5f, 0.15f, 0.85f) * (1f - r / 0.006f) * 0.8f; }
                // lava veins around the Crucible
                float dl = Vector2.Distance(new Vector2(x, y), Biomes[4].pos);
                float lava = Mathf.Abs(Noise(x * 2.2f, y * 2.2f + 5f, 2) - 0.5f);
                if (dl < 1.1f && lava < 0.03f && h > 0f) { c = new Color(0.9f, 0.35f, 0.1f); e += new Color(1f, 0.35f, 0.05f) * (1f - dl / 1.1f); }
                // vignette: the edges of the known world fade into dark
                float vx = Mathf.Abs(x) / (LandLength * 0.5f), vy = Mathf.Abs(y) / (LandDepth * 0.5f);
                float vig = 1f - Mathf.Pow(Mathf.Clamp01(Mathf.Max(vx, vy)), 3f) * 0.6f;
                // a little mystery: desaturate and darken
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = Color.Lerp(c, new Color(lum, lum, lum), 0.15f) * 0.88f * vig;
                px[k] = c;
                ex[k] = e;
            }
        tex.SetPixels(px); tex.Apply(true);
        emit.SetPixels(ex); emit.Apply(true);
        tex = SaveTexture(tex, $"{ArtFolder}/WarTableLand.png");
        emit = SaveTexture(emit, $"{ArtFolder}/WarTableLandGlow.png");

        // mesh: a low-relief heightfield; the sea is flat
        const int MW = 140, MH = 76;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int j = 0; j <= MH; j++)
            for (int i = 0; i <= MW; i++)
            {
                float u = i / (float)MW, v = j / (float)MH;
                float x = (u - 0.5f) * LandLength, y = (v - 0.5f) * LandDepth;
                float h = LandHeight(x, y, out _, out _);
                verts.Add(new Vector3(x, Mathf.Max(0f, h) * 0.32f, y));
                uvs.Add(new Vector2(u, v));
            }
        for (int j = 0; j < MH; j++)
            for (int i = 0; i < MW; i++)
            {
                int a = j * (MW + 1) + i, b = a + 1, c = a + MW + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        var mesh = new Mesh { name = "WarTableLand", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        string mp = $"{ArtFolder}/WarTableLand.asset";
        AssetDatabase.DeleteAsset(mp);
        AssetDatabase.CreateAsset(mesh, mp);
        return mesh;
    }

    static Texture2D SaveTexture(Texture2D t, string path)
    {
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // =====================================================================
    // stations
    // =====================================================================

    static HallStation[] Stations(Transform root, Transform hall, HallCatalog cat)
    {
        var g = Group(root, "Stations");
        // P1 top-left, P2 top-right, P3 bottom-left, P4 bottom-right on screen
        Vector3[] at = { new(0f, 0f, StationDistance), new(StationDistance, 0f, 0f), new(-StationDistance, 0f, 0f), new(0f, 0f, -StationDistance) };
        string[] colors = { "red", "blue", "green", "yellow" };
        // two spots for loot behind each station, away from the table and the camera
        Vector3[][] props = { new[] { Vector3.right, Vector3.forward }, new[] { Vector3.right, Vector3.forward }, new[] { Vector3.forward, Vector3.left }, new[] { Vector3.right, Vector3.back } };
        string[][] loot = { new[] { "chest", "barrel_large" }, new[] { "crates_stacked", "chest" }, new[] { "keg", "chest_gold" }, new[] { "chest", "box_large" } };

        var dais = DaisMaterials(out var trim);
        var result = new HallStation[4];
        for (int i = 0; i < 4; i++)
        {
            var s = new GameObject($"Station P{i + 1}");
            s.transform.SetParent(g, false);
            s.transform.localPosition = at[i];
            var st = s.AddComponent<HallStation>();
            st.index = i;

            // the dais: a low stone drum with a glowing rim
            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(drum.GetComponent<Collider>());
            drum.name = "Dais";
            drum.transform.SetParent(s.transform, false);
            drum.transform.localPosition = Vector3.up * -0.02f;
            drum.transform.localScale = new Vector3(3.3f, 0.06f, 3.3f);
            drum.GetComponent<MeshRenderer>().sharedMaterial = dais;
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(rim.GetComponent<Collider>());
            rim.name = "DaisRim";
            rim.transform.SetParent(s.transform, false);
            rim.transform.localPosition = Vector3.up * -0.03f;
            rim.transform.localScale = new Vector3(3.45f, 0.05f, 3.45f);
            rim.GetComponent<MeshRenderer>().sharedMaterial = trim;

            // loot behind
            for (int k = 0; k < 2; k++)
            {
                var p = Piece(loot[i][k], s.transform, props[i][k] * 3.1f + (k == 0 ? Vector3.zero : Vector3.zero), Random(i, k) * 360f);
                if (p != null) p.transform.LookAt(s.transform.position + Vector3.up * p.transform.position.y);
            }

            // banner: hung on the back wall for P1/P2, on a pillar beside P3/P4
            Transform bannerParent = s.transform;
            Vector3 bannerPos; float bannerYaw;
            if (i == 0) { bannerPos = new Vector3(0f, 0.1f, Half - at[i].z); bannerYaw = 180f; }
            else if (i == 1) { bannerPos = new Vector3(Half - at[i].x, 0.1f, 0f); bannerYaw = 270f; }
            else
            {
                // a standard: a dark pole with the banner hanging from its crossbar
                var side = i == 2 ? -HallCamera.GroundRight : HallCamera.GroundRight;
                var polePos = side * 2.5f + HallCamera.GroundUp * 0.7f;
                Pole(s.transform, polePos);
                bannerPos = polePos - HallCamera.GroundUp * 0.3f + Vector3.down * 0.25f;
                bannerYaw = 225f;
            }
            var roll = new GameObject("BannerRoll").transform;
            roll.SetParent(s.transform, false);
            roll.localPosition = bannerPos + Vector3.up * 3.73f;
            roll.localRotation = Quaternion.Euler(0f, bannerYaw, 0f);
            var banner = Piece($"banner_patternA_{colors[i]}", roll, new Vector3(0f, -3.73f, 0f), 0f);
            st.banner = roll;
            result[i] = st;
        }
        return result;
    }

    const float StationDistance = 7.3f;

    static void Pole(Transform parent, Vector3 pos)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ArtFolder}/DaisRim.mat");
        foreach (var (p, sc) in new[] { (pos + Vector3.up * 2.1f, new Vector3(0.14f, 2.1f, 0.14f)), (pos + Vector3.up * 3.95f - HallCamera.GroundUp * 0.25f, new Vector3(0.1f, 0.85f, 0.1f)) })
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(c.GetComponent<Collider>());
            c.name = "Pole";
            c.transform.SetParent(parent, false);
            c.transform.localPosition = p;
            c.transform.localScale = sc;
            if (sc.y < 1f) c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, HallCamera.GroundUp);
            c.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.name = "PoleCap";
        cap.transform.SetParent(parent, false);
        cap.transform.localPosition = pos + Vector3.up * 4.3f;
        cap.transform.localScale = Vector3.one * 0.3f;
        cap.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static float Random(int a, int b) => Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f, 1f);

    static Material DaisMaterials(out Material trim)
    {
        string p1 = $"{ArtFolder}/Dais.mat", p2 = $"{ArtFolder}/DaisRim.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p1);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.17f, 0.15f, 0.21f));
            m.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(m, p1);
        }
        trim = AssetDatabase.LoadAssetAtPath<Material>(p2);
        if (trim == null)
        {
            trim = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            trim.SetColor("_BaseColor", new Color(0.32f, 0.27f, 0.2f));
            trim.SetFloat("_Metallic", 0.7f);
            trim.SetFloat("_Smoothness", 0.5f);
            AssetDatabase.CreateAsset(trim, p2);
        }
        return m;
    }

    static void AddToBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        int at = Mathf.Min(1, scenes.Count);
        scenes.Insert(at, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
