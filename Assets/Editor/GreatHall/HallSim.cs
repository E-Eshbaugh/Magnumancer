using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the Great Hall headless with virtual gamepads: joins four players, picks
/// wizards/runes/guns, readies up, goes to the War Table, picks a map and launches,
/// saving screenshots of each step and checking what lands in DataManager.
///   Unity -batchmode -projectPath . -executeMethod HallSim.Run   (HALL_OUT = screenshot dir)
/// Also on the menu: Magnumancer > Great Hall > Simulate Session (screenshots in Temp/HallSim).
/// </summary>
[InitializeOnLoad]
public static class HallSim
{
    const string Key = "HallSim.Active";
    static string Out => Environment.GetEnvironmentVariable("HALL_OUT") ?? "Temp/HallSim";

    static HallSim()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Drive;
    }

    [MenuItem("Magnumancer/Great Hall/Simulate Session")]
    public static void Run()
    {
        Directory.CreateDirectory(Out);
        EditorSceneManager.OpenScene(GreatHallBuilder.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetInt("HallSim.Step", 0);
        EditorApplication.update -= Drive;
        EditorApplication.update += Drive;
        EditorApplication.EnterPlaymode();
    }

    // ---------- the script ----------

    static int step;
    static double next;
    static Gamepad[] pads;
    static readonly List<string> log = new();
    static int failures;

    static void Drive()
    {
        if (!EditorApplication.isPlaying) return;
        if (pads != null) ProcessReleases();
        if (EditorApplication.timeSinceStartup < next || releases.Count > 0) return;
        try { Step(); }
        catch (Exception e) { Fail("exception: " + e); Finish(); }
    }

    static void Wait(double s) => next = EditorApplication.timeSinceStartup + s;

    static void Step()
    {
        var d = HallDirector.Instance;
        switch (step++)
        {
            case 0:
                // input goes to the game even with no focused game view
                var settings = ScriptableObject.CreateInstance<InputSettings>();
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings;
                foreach (var g in Gamepad.all.ToArray()) InputSystem.RemoveDevice(g);
                pads = new Gamepad[4];
                for (int i = 0; i < 4; i++) pads[i] = InputSystem.AddDevice<Gamepad>($"SimPad{i + 1}");
                CaptureRig();
                Wait(2.0);
                break;
            case 1:
                Shot("01_empty");
                Check(d != null, "director exists");
                Press(0, GamepadButton.West);
                Wait(0.4);
                break;
            case 2:
                Press(1, GamepadButton.West);
                Wait(0.3);
                break;
            case 3:
                Press(2, GamepadButton.West);
                Wait(1.6);
                break;
            case 4:
                Shot("02_three_joined");
                Check(d.Stations.Count(s => s.Joined) == 3, "three players joined");
                // P1 flips wizard and rune; P2 goes shopping; P3 reads lore
                Press(0, GamepadButton.RightShoulder);
                Press(1, GamepadButton.South);
                Press(2, GamepadButton.North);
                Wait(0.5);
                break;
            case 5:
                Press(0, GamepadButton.DpadRight);
                Stick(1, 1f);
                Wait(0.35);
                break;
            case 6:
                Stick(1, 0f);
                Press(1, GamepadButton.DpadUp);
                Wait(0.5);
                break;
            case 7:
                Press(1, GamepadButton.RightShoulder);
                Wait(0.4);
                break;
            case 8:
                Press(1, GamepadButton.DpadLeft);
                Press(3, GamepadButton.West);   // P4 arrives late
                Wait(1.6);
                break;
            case 9:
                Shot("03_picking");
                var p2 = d.Stations[1];
                Check(p2.CurrentStage == HallStation.Stage.Armory, "P2 in the armory");
                Check(p2.Loadout.Count(w => w != null) >= 1, $"P2 packed guns ({p2.Spent}/{p2.Budget} orbs)");
                Check(d.Stations[0].WizardIndex != 0 || d.Stations[0].ActiveRune == 1, "P1 changed wizard/rune");
                // everyone to the armory
                Press(0, GamepadButton.South);
                Press(2, GamepadButton.North);
                Press(3, GamepadButton.South);
                Wait(0.4);
                break;
            case 10:
                Press(2, GamepadButton.South);
                Press(0, GamepadButton.DpadRight);
                Press(3, GamepadButton.DpadDown);
                Wait(0.4);
                break;
            case 11:
                Press(2, GamepadButton.DpadUp);
                Press(0, GamepadButton.LeftShoulder);
                Wait(0.4);
                break;
            case 12:
                Press(0, GamepadButton.DpadLeft);
                Press(2, GamepadButton.DpadDown);
                Wait(0.8);
                break;
            case 13:
                Shot("04_armory");
                // ready up: P1, P3, P4 (P2 stays to show a mixed state)
                Press(0, GamepadButton.South);
                Press(2, GamepadButton.South);
                Press(3, GamepadButton.South);
                Wait(1.4);
                break;
            case 14:
                Shot("05_mostly_ready");
                Press(1, GamepadButton.South);
                Wait(1.6);
                break;
            case 15:
                Shot("06_all_ready");
                Check(d.Stations.Where(s => s.Joined).All(s => s.IsReady), "everyone ready");
                Press(0, GamepadButton.South);
                Wait(2.0);
                break;
            case 16:
                Check(d.CurrentPhase == HallDirector.Phase.Table, $"at the War Table (phase {d.CurrentPhase})");
                Shot("07_war_table");
                Stick(0, 1f, 0f);
                Wait(0.3);
                break;
            case 17:
                Stick(0, 0f, 0f);
                Press(0, GamepadButton.RightShoulder);
                Wait(0.8);
                break;
            case 18:
                Shot("08_war_table_pick");
                Stick(1, 0f, 1f);
                Wait(0.3);
                break;
            case 19:
                Stick(1, 0f, 0f);
                Wait(0.6);
                break;
            case 20:
                Shot("09_war_table_pick2");
                Press(0, GamepadButton.South);
                Wait(0.6);
                break;
            case 21:
                Shot("10_launch");
                Wait(2.5);
                break;
            case 22:
                var dm = DataManager.Instance;
                Check(dm.NumPlayers == 4, $"DataManager has 4 players ({dm.NumPlayers})");
                for (int i = 0; i < dm.NumPlayers; i++)
                    log.Add($"  P{i + 1}: {dm.GetWizard(i)?.wizardName} runes {dm.GetActiveRune(i)}/{dm.GetPassiveRune(i)} pad {dm.GetPad(i)?.name} guns [{string.Join(", ", dm.GetLoadout(i).Select(w => w ? w.weaponName : "-"))}]");
                log.Add($"  mode {dm.SelectedMode} map {dm.SelectedMap} scene '{SceneManager.GetActiveScene().name}'");
                Check(SceneManager.GetActiveScene().name != GreatHallBuilder.ScenePath.Replace("Assets/Scenes/", "").Replace(".unity", ""), "an arena loaded");
                Shot("11_arena", world: true);
                Wait(0.5);
                break;
            default:
                Finish();
                break;
        }
    }

    // ---------- input ----------

    static readonly Dictionary<int, GamepadState> held = new();

    static GamepadState State(int p) => held.TryGetValue(p, out var s) ? s : new GamepadState();

    static readonly List<(int pad, double at)> releases = new();

    /// Holds the button for a few frames, then lets go (the game sees one press)
    static void Press(int p, GamepadButton b)
    {
        var s = State(p);
        InputSystem.QueueStateEvent(pads[p], s.WithButton(b, true));
        releases.Add((p, EditorApplication.timeSinceStartup + 0.12));
    }

    static void ProcessReleases()
    {
        for (int i = releases.Count - 1; i >= 0; i--)
        {
            if (EditorApplication.timeSinceStartup < releases[i].at) continue;
            InputSystem.QueueStateEvent(pads[releases[i].pad], State(releases[i].pad));
            releases.RemoveAt(i);
        }
    }

    static void Stick(int p, float x, float y = 0f)
    {
        var s = State(p);
        s.leftStick = new Vector2(x, y);
        held[p] = s;
        InputSystem.QueueStateEvent(pads[p], s);
    }

    // ---------- screenshots ----------

    static Camera capture;
    static RenderTexture captureRT;

    /// Overlay canvases don't render without a display: from the first call on, they draw
    /// through an off-screen camera instead (needs a frame to take effect, see step 0)
    static void CaptureRig()
    {
        if (capture != null) return;
        captureRT = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var go = new GameObject("SimCapture", typeof(Camera));
        go.transform.position = new Vector3(0f, 5000f, 0f);
        capture = go.GetComponent<Camera>();
        capture.clearFlags = CameraClearFlags.SolidColor;
        capture.backgroundColor = Color.black;
        capture.orthographic = true;
        capture.targetTexture = captureRT;
        capture.depth = 100;
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
        {
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = capture;
            c.planeDistance = 50f - c.sortingOrder * 0.1f;
        }
    }

    static void Shot(string name, bool world = false)
    {
        const int W = 1920, H = 1080;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        RenderTexture src;
        if (world || HallDirector.Instance == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            src = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = src;
            cam.Render();
            cam.targetTexture = prev;
        }
        else
        {
            CaptureRig();
            var hall = HallDirector.Instance.hallCamera;
            if (hall != null) hall.Cam.Render();   // the pixel render the screen canvas shows
            Canvas.ForceUpdateCanvases();
            capture.Render();
            src = captureRT;
        }
        RenderTexture.active = src;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        if (src != captureRT) src.Release();
        log.Add($"shot {name}");
    }

    // ---------- results ----------

    static void Check(bool ok, string what)
    {
        log.Add((ok ? "PASS " : "FAIL ") + what);
        if (!ok) failures++;
    }

    static void Fail(string what) { log.Add("FAIL " + what); failures++; }

    static void Finish()
    {
        EditorApplication.update -= Drive;
        SessionState.SetBool(Key, false);
        log.Add(failures == 0 ? "ALL PASSED" : $"{failures} FAILED");
        File.WriteAllLines(Path.Combine(Out, "sim.txt"), log);
        Debug.Log("[HallSim]\n" + string.Join("\n", log));
        foreach (var p in pads ?? new Gamepad[0]) if (p != null && p.added) InputSystem.RemoveDevice(p);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
