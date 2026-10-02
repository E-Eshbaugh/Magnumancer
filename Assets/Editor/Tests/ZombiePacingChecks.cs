using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// Run in an isolated project with -batchmode -executeMethod ZombiePacingChecks.Run.
// These checks use real physics, a baked NavMesh and the production ability components.
[InitializeOnLoad]
public static class ZombiePacingChecks
{
    const string Pending = "Magnumancer.ZombiePacingChecks";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new();
    static string current;
    static bool failed;
    static double deadline;

    static ZombiePacingChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run these checks in a separate batch Unity project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
        System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.dataPath, "../zombie-pacing-checks.txt"), results);
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
    static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);

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

    static GoblinHealth Goblin(Vector3 at, bool childCollider = false)
    {
        var go = new GameObject("CheckGoblin");
        go.transform.position = at;
        go.layer = 3; go.tag = "Monster";
        var agent = go.AddComponent<NavMeshAgent>();
        agent.speed = 4f; agent.radius = 0.3f;
        var body = childCollider ? new GameObject("ChildHitbox") : go;
        if (childCollider) { body.transform.SetParent(go.transform, false); body.layer = 3; }
        var collider = body.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up; collider.height = 2; collider.radius = 0.3f;
        var health = go.AddComponent<GoblinHealth>();
        health.maxHealth = health.currentHealth = 1000f;
        return health;
    }

    static IEnumerator Reset()
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
        yield return null;
        yield return null;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "CheckFloor";
        floor.transform.position = Vector3.down * 0.5f;
        floor.transform.localScale = new Vector3(80, 1, 80);
        Physics.SyncTransforms();
        var surface = floor.AddComponent<NavMeshSurface>();
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Teams.AllOneTeam();
        yield return null;
    }

    static IEnumerable<GoblinDeathTracker> Tracked(GoblinSpawner spawner)
    {
        foreach (var tracker in Object.FindObjectsByType<GoblinDeathTracker>(FindObjectsSortMode.None))
            if (tracker.spawner == spawner) yield return tracker;
    }

    static IEnumerator Suite()
    {
        current = "Balance curves";
        yield return Reset();
        var player = Player(Vector3.zero);
        var hp = player.GetComponent<PlayerHealthControl>(); hp.invincible = true;
        var spawner = new GameObject("PacingSpawner").AddComponent<GoblinSpawner>();
        spawner.openingDelay = 1000;
        var first = spawner.TuningForWave(1, 1);
        var fifth = spawner.TuningForWave(5, 1);
        var tenth = spawner.TuningForWave(10, 1);
        Check(first.regularCount == 8 && fifth.regularCount == 24 && tenth.regularCount == 44,
            "Solo wave budgets grow from 8 to 24 to 44 ordinary zombies");
        Check(first.damage == 50 && fifth.damage == 60 && tenth.damage == 70,
            "Ordinary damage scales 50 / 60 / 70 over waves 1 / 5 / 10");
        Check(Mathf.CeilToInt(300 / first.damage) == 6 && Mathf.CeilToInt(300 / tenth.damage) == 5
            && Mathf.CeilToInt(400 / first.damage) == 8 && Mathf.CeilToInt(400 / tenth.damage) == 6,
            "Heart stats retain a six-to-five versus eight-to-six hit survival distinction");
        Check(fifth.bossCount == 1 && tenth.bossCount == 1 && first.bossCount == 0
            && spawner.TuningForWave(3, 1).minibossCount == 1, "Elites appear on wave 3; boss waves are every fifth wave");
        var squad = spawner.TuningForWave(10, 4);
        Check(squad.regularCount == 124 && squad.aliveCap == 30 && squad.damage == tenth.damage,
            "Four-player co-op raises horde numbers and crowd limit without inflating per-hit damage");
        Check(squad.spawnInterval < tenth.spawnInterval && tenth.spawnInterval < first.spawnInterval,
            "Spawn cadence accelerates with waves and party size");
        var endless = spawner.TuningForWave(1000, 4);
        Check(endless.healthMultiplier == 3f && endless.damage == 70 && endless.aliveCap == 30 && endless.moveSpeed == 4.4f,
            "Health, damage, movement speed and concurrent crowd size have safety caps");

        current = "Timed spawns and real enemy stats";
        var template = Goblin(Vector3.right * 30);
        template.maxHealth = template.currentHealth = 50;
        template.gameObject.AddComponent<GoblinChaseNav>();
        spawner.baseMonsters = new[] { template.gameObject };
        spawner.openingDelay = 0.2f;
        spawner.openingZombieCount = 3;
        spawner.baseMonsterFactor = 1;
        spawner.openingAliveCap = spawner.soloAliveCap = 2;
        spawner.openingSpawnInterval = spawner.fastestSpawnInterval = 0.1f;
        spawner.waveBreak = 0.6f;
        spawner.totalRounds = 2;
        spawner.zombieDropChance = 0;
        GamePause.Pause();
        for (int i = 0; i < 5; i++) yield return null;
        Check(spawner.CurrentWave == 0 && spawner.AliveCount == 0, "Pause holds the opening countdown");
        GamePause.Resume();
        yield return Wait(0.55f);
        Check(spawner.CurrentWave == 1 && spawner.AliveCount == 2 && spawner.PendingCount == 1,
            "Wave streams enemies up to its cap and retains queued reinforcements");
        foreach (var tracker in Tracked(spawner))
        {
            Check(tracker.GetComponent<GoblinHealth>().maxHealth == 50 && tracker.GetComponent<GoblinChaseNav>().attackDamage == 50,
                "Actual spawned regular receives wave health and damage");
            Check(tracker.GetComponent<NavMeshAgent>().isOnNavMesh && tracker.transform.position.magnitude > 6,
                "Spawned zombie is on reachable navigation outside immediate melee range");
        }
        spawner.StartNextWave(); spawner.StartNextWave();
        Check(spawner.CurrentWave == 1 && spawner.PendingCount == 1, "Repeated start requests cannot overlap active waves");
        foreach (var tracker in Tracked(spawner)) { tracker.GetComponent<GoblinHealth>().TakeDamage(10000, player); break; }
        yield return Wait(0.2f);
        Check(spawner.CurrentWave == 1 && spawner.AliveCount == 2 && spawner.PendingCount == 0,
            "A kill frees one slot for a reinforcement without prematurely ending the wave");
        foreach (var tracker in Tracked(spawner)) tracker.GetComponent<GoblinHealth>().TakeDamage(10000, player);
        yield return Wait(0.1f);
        Check(!spawner.WaveActive && spawner.CurrentWave == 1, "Clearing all queued and living enemies begins the breather");
        spawner.NotifyEnemyDeath(null); spawner.NotifyEnemyDeath(null);
        yield return Wait(0.7f);
        Check(spawner.CurrentWave == 2 && spawner.WaveActive, "Breather ends in exactly one new wave");
        Object.Destroy(spawner.gameObject);
        yield return null;

        current = "Boss fallback and guaranteed loot";
        yield return Reset();
        player = Player(Vector3.zero); player.GetComponent<PlayerHealthControl>().invincible = true;
        template = Goblin(Vector3.right * 30);
        template.maxHealth = template.currentHealth = 200;
        template.gameObject.AddComponent<GoblinChaseNav>();
        spawner = new GameObject("BossSpawner").AddComponent<GoblinSpawner>();
        spawner.openingDelay = 1000;
        spawner.baseMonsterFactor = 0;
        spawner.midBossMonsters = new[] { template.gameObject };
        spawner.bossMonsters = System.Array.Empty<GameObject>();
        spawner.totalRounds = 5;
        Set(spawner, "currentWave", 4);
        spawner.StartNextWave();
        yield return Wait(0.2f);
        Check(spawner.AliveCount == 1, "The current scene's empty boss pool falls back to its large monster");
        foreach (var tracker in Tracked(spawner))
        {
            Check(tracker.guaranteedLoot && Mathf.Approximately(tracker.GetComponent<GoblinHealth>().maxHealth, 546)
                && tracker.GetComponent<GoblinChaseNav>().attackDamage == 105,
                "Fifth-wave boss has scaled health, stronger damage and guaranteed loot");
            Check(tracker.GetComponent<GoblinChaseNav>().attackWindup == 0.65f,
                "Hard-hitting elites give a longer dodge warning");
            tracker.GetComponent<GoblinHealth>().TakeDamage(10000, player);
        }
        yield return null; yield return null;
        Check(spawner.AllWavesComplete, "Final wave completion is reported after the last monster dies");
        Check(Pickup.Live.Count > 0, "Real boss death still awards loot");

        current = "Spawn safety and wipe";
        yield return Reset();
        player = Player(Vector3.zero);
        hp = player.GetComponent<PlayerHealthControl>();
        template = Goblin(Vector3.right * 30);
        spawner = new GameObject("BlockedSpawner").AddComponent<GoblinSpawner>();
        spawner.baseMonsters = new[] { template.gameObject };
        spawner.minimumSpawnDistance = 1000;
        spawner.StartNextWave();
        int queued = spawner.PendingCount;
        yield return Wait(0.2f);
        Check(spawner.AliveCount == 0 && spawner.PendingCount == queued,
            "No safe spawn point leaves tickets queued instead of placing zombies on players");
        hp.TakeDamage(1000, template.gameObject);
        spawner.minimumSpawnDistance = 8;
        yield return Wait(0.6f);
        Check(spawner.AliveCount == 0, "A team wipe stops queued spawns");

        current = "Crowd-hit grace";
        yield return Reset();
        player = Player(Vector3.zero);
        hp = player.GetComponent<PlayerHealthControl>(); hp.SetHealthStat(3);
        var enemy = Goblin(Vector3.right * 10);
        hp.TakeDamage(50, enemy.gameObject); hp.TakeDamage(50, enemy.gameObject); hp.TakeDamage(50, enemy.gameObject);
        Check(hp.currentHealth == 250, "Overlapping zombie hits cannot all land in one frame");
        yield return Wait(0.4f);
        hp.TakeDamage(50, enemy.gameObject);
        Check(hp.currentHealth == 200, "Zombie damage resumes when the brief grace period expires");
        yield return Wait(0.4f);
        Teams.FreeForAll();
        hp.TakeDamage(20, enemy.gameObject); hp.TakeDamage(20, enemy.gameObject);
        Check(hp.currentHealth == 160, "The new crowd-hit grace does not change free-for-all damage");
        results.Add("All zombie pacing checks completed.");
    }
}
