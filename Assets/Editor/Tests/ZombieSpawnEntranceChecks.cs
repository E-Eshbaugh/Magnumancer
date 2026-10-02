using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated project with -batchmode -executeMethod ZombieSpawnEntranceChecks.Run.
// These checks use the actual Zombies map, progression barriers and runtime NavMesh.
[InitializeOnLoad]
public static class ZombieSpawnEntranceChecks
{
    const string Pending = "Magnumancer.ZombieSpawnEntranceChecks";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new();
    static string current;
    static bool failed;
    static double deadline;

    static ZombieSpawnEntranceChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run these checks in a separate batch Unity project.");
        EditorSceneManager.OpenScene("Assets/Scenes/CinderCrucibleZombies.unity");
        foreach (var shake in Object.FindObjectsByType<CameraShake>(FindObjectsSortMode.None)) Object.DestroyImmediate(shake);
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
        foreach (var setup in Object.FindObjectsByType<MultiplayerManager>(FindObjectsSortMode.None)) setup.enabled = false;
        foreach (var spawner in Object.FindObjectsByType<GoblinSpawner>(FindObjectsSortMode.None)) spawner.enabled = false;
        foreach (var player in Object.FindObjectsByType<PlayerMovement3D>(FindObjectsSortMode.None)) player.enabled = false;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        deadline = EditorApplication.timeSinceStartup + 240;
        Application.logMessageReceived += Log;
        routines.Push(Suite());
        EditorApplication.update += Tick;
    }

    static void Log(string message, string stack, LogType type)
    {
        // Unity 6000.6's background search index can throw on an empty batch project.
        // Keep it visible, but separate editor indexing from gameplay failures.
        if (stack.Contains("UnityEditor.Search.") && !stack.Contains("Assets/Scripts/"))
        {
            results.Add("EDITOR NOTE " + message);
            return;
        }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            failed = true;
            results.Add("ERROR [" + current + "] " + message + "\n" + stack);
        }
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Gameplay checks timed out.");
            if (routines.Count == 0) { Finish(); return; }
            var top = routines.Peek();
            if (!top.MoveNext()) { routines.Pop(); return; }
            if (top.Current is IEnumerator child) routines.Push(child);
        }
        catch (Exception e)
        {
            failed = true;
            results.Add("FAIL [" + current + "] " + e);
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.dataPath, "../zombie-spawn-entrance-checks.txt"), results);
        Debug.Log(string.Join("\n", results));
        EditorApplication.Exit(failed ? 1 : 0);
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        results.Add("PASS " + message);
    }

    static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

    static GameObject Player(Vector3 at, PassiveType faction = PassiveType.LastRites)
    {
        var go = new GameObject("CheckPlayer");
        go.transform.position = at;
        go.layer = 3;
        var cc = go.AddComponent<CharacterController>();
        cc.center = Vector3.up; cc.height = 2; cc.radius = 0.3f;
        var mover = go.AddComponent<PlayerMovement3D>();
        var wizard = ScriptableObject.CreateInstance<WizardData>();
        wizard.passive = faction;
        wizard.themeColor = Color.cyan;
        mover.wizard = wizard;
        go.AddComponent<PlayerHealthControl>();
        return go;
    }

    static bool Position(GoblinSpawner spawner, GameObject prefab, out Vector3 position)
    {
        object[] args = { prefab, Vector3.zero };
        bool ok = (bool)Call(spawner, "TryGetSpawnPosition", args);
        position = (Vector3)args[1];
        return ok;
    }

    static IEnumerator Suite()
    {
        current = "Actual Zombies map entrance layout";
        yield return Wait(2f);
        var spawner = Object.FindAnyObjectByType<GoblinSpawner>();
        Check(spawner != null && spawner.spawnPoints.Length == 24, "Actual Zombies map contains 24 wired spawn entrances");
        var points = spawner.spawnPoints;
        var gates = Object.FindObjectsByType<DestructibleWall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var byName = new Dictionary<string, DestructibleWall>();
        foreach (var gate in gates) byName.Add(gate.name, gate);
        var prefab = spawner.baseMonsters[0];
        var player = PlayerHealthControl.ActivePlayers[0];
        player.invincible = true;
        // Keep one actual starting location, preventing inactive menu selections from
        // influencing the safety-radius and origin checks in this fixture.
        foreach (var other in new List<PlayerHealthControl>(PlayerHealthControl.ActivePlayers))
            if (other != player) other.gameObject.SetActive(false);
        Check(spawner.minimumSpawnDistance == 8f && spawner.maximumEntrancePath == 45f, "Scene uses the intended safety distance and route limit");
        Check(Position(spawner, prefab, out _), "Starting party has a safe entrance at normal gameplay distances");
        spawner.minimumSpawnDistance = 0;
        spawner.maximumEntrancePath = 1000;
        // The last hall is across an existing navigation break. Reaching that side
        // must not unlock its gates, but supplies a reachable target once they open.
        var farPlayer = Player(points[16].transform.position);
        farPlayer.GetComponent<PlayerHealthControl>().invincible = true;
        int[] expected = { 7, 10, 13, 16, 24 };
        string[] opens = { "ProgressBlock (3)", "ProgressBlock", "ProgressBlock (1)", "ProgressBlock (2)" };
        for (int stage = 0; stage < expected.Length; stage++)
        {
            if (stage > 0)
            {
                byName[opens[stage-1]].TakeDamage(100000);
                yield return Wait(1f);
            }
            spawner.minimumSpawnDistance = 0;
            spawner.maximumEntrancePath = 1000;
            int available = 0, gated = 0;
            foreach (var point in points)
            {
                spawner.spawnPoints = new[] { point };
                bool eligible = Position(spawner, prefab, out var position);
                if (point.GatesOpen) gated++;
                if (eligible) available++;
                if (!point.GatesOpen) Check(!eligible, "Locked entrance stays off: " + point.name + " at stage " + stage);
                if (point.GatesOpen && !eligible) results.Add("UNREACHABLE " + point.name + " at stage " + stage + " " + point.transform.position);
            }
            Check(gated == expected[stage], "Gate state enables exactly " + expected[stage] + " entrances at stage " + stage);
            Check(available == expected[stage], "All " + expected[stage] + " unlocked entrances have a live route on the real map at stage " + stage);
        }
        spawner.spawnPoints = new[] { points[16] };
        farPlayer.SetActive(false);
        Check(!Position(spawner, prefab, out _), "Unlocked far-hall entrance waits for a teammate it can actually reach");
        farPlayer.SetActive(true);
        Check(Position(spawner, prefab, out _), "Entering the unlocked far hall enables its reachable entrance");
        spawner.spawnPoints = points;
        // Reset rotation history; all locations must be used before any repeats.
        foreach (var point in points)
            point.GetType().GetField("<LastSpawnTime>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(point, float.NegativeInfinity);
        yield return null;
        var selected = new HashSet<ZombieSpawnPoint>();
        for (int i = 0; i < points.Length; i++)
        {
            Check(Position(spawner, prefab, out _), "Open network supplies spawn " + i);
            ZombieSpawnPoint newest = null;
            foreach (var point in points)
                if (newest == null || point.LastSpawnTime > newest.LastSpawnTime) newest = point;
            selected.Add(newest);
            yield return null;
        }
        Check(selected.Count == points.Length, "Spawn rotation uses every available entrance before repeating");
        spawner.minimumSpawnDistance = 1000;
        Check(!Position(spawner, prefab, out _), "Blocked authored network never falls back to random room spawns");
        var one = points[0];
        spawner.spawnPoints = new[] { one };
        spawner.minimumSpawnDistance = 8;
        player.transform.position = one.transform.position;
        Check(!Position(spawner, prefab, out _), "Entrance cannot spawn on top of a standing player");
        one.enabled = false;
        spawner.minimumSpawnDistance = 0;
        Check(!Position(spawner, prefab, out _), "Disabled entrances are excluded");
        results.Add("All actual-map spawn entrance checks completed.");
    }
}
