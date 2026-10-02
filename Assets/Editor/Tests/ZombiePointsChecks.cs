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

// Run in an isolated project with -batchmode -executeMethod ZombiePointsChecks.Run.
// Uses real health events and all four HUDs; no monsters, scene assets or controllers required.
[InitializeOnLoad]
public static class ZombiePointsChecks
{
    const string Pending = "Magnumancer.ZombiePointsChecks";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new();
    static string current;
    static bool failed;
    static double deadline;

    static ZombiePointsChecks()
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
        System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.dataPath, "../zombie-points-checks.txt"), results);
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

    static void Call(string method, params object[] args)
        => typeof(ZombiesPoints).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

    static GameObject Player(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<PlayerHealthControl>();
        return go;
    }

    static CircleAbilityUI Hud(Transform canvas, int index)
    {
        var go = new GameObject("Crest" + index, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.5f + index * 0.1f, 0.8f);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(index % 2 == 0 ? 0 : 1, index < 2 ? 1 : 0);
        rect.anchoredPosition = new Vector2(index % 2 == 0 ? 100 : -100, index < 2 ? -90 : 130);
        rect.sizeDelta = new Vector2(100, 100);
        var hud = go.AddComponent<CircleAbilityUI>();
        hud.crestImage = hud.glowImage = image;
        return hud;
    }

    static IEnumerator Suite()
    {
        current = "Points ledger and presentation";
        var spawner = new GameObject("Spawner").AddComponent<GoblinSpawner>();
        spawner.enabled = false;
        Call("OnSceneLoaded", UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
        Check(ZombiesPoints.Active, "Zombies scene enables points");
        var canvas = new GameObject("HUD", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var players = new GameObject[4];
        var labels = new TMPro.TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            players[i] = Player("Player" + i);
            players[i].transform.position = Vector3.right * i * 4;
            var hud = Hud(canvas.transform, i);
            var hp = players[i].GetComponent<PlayerHealthControl>();
            PlayerPointsDisplay.Bind(hp, hud);
            PlayerPointsDisplay.Bind(hp, hud);
            labels[i] = hud.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            Check(labels[i] != null && labels[i].text == "0", "Player " + i + " starts with a visible zero balance");
            Check(hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length == 1,
                "Repeated binding creates only one balance for player " + i);
        }
        var p = players[0];
        var child = new GameObject("OwnedGun"); child.transform.SetParent(p.transform);
        var enemy = new GameObject("Enemy").AddComponent<GoblinHealth>();
        enemy.currentHealth = enemy.maxHealth = 1000;
        enemy.TakeDamage(1, child);
        Check(ZombiesPoints.Get(p) == 10 && ZombiesPoints.Get(child) == 10, "Child-owned hits credit the owning wizard once");
        enemy.TakeDamage(9999, p);
        Check(ZombiesPoints.Get(p) == 80, "Lethal hit awards hit points and one kill reward");
        enemy.TakeDamage(9999, p);
        Check(ZombiesPoints.Get(p) == 80, "Dead enemies cannot be farmed for more points");
        DamageEvents.RaiseDamaged(players[1], p, 10);
        DamageEvents.RaiseDamaged(players[1], null, 10);
        Check(ZombiesPoints.Get(p) == 80, "Player damage never awards zombie points");
        for (int i = 1; i < 4; i++) Call("Earn", players[i], i * 100);
        Check(ZombiesPoints.Get(players[1]) == 100 && ZombiesPoints.Get(players[2]) == 200 &&
            ZombiesPoints.Get(players[3]) == 300 && labels[3].text == "300", "All four players have independent wallets and HUDs");
        ZombiesPoints.Multiplier = 2;
        Call("OnReacted", null, p, null, Vector3.zero);
        Call("OnComboEnded", p, 2);
        Check(ZombiesPoints.Get(p) == 240, "Double Points applies to reaction and combo rewards");
        int notices = 0;
        Action<GameObject, int, int> listener = (who, total, delta) => notices++;
        ZombiesPoints.PointsChanged += listener;
        Check(!ZombiesPoints.TrySpend(p, -50) && !ZombiesPoints.TrySpend(p, 0) &&
            !ZombiesPoints.TrySpend(p, 241) && !ZombiesPoints.TrySpend(null, 1) &&
            !ZombiesPoints.TrySpend(canvas.gameObject, 1), "Invalid purchases and insufficient funds are rejected");
        Check(notices == 0 && ZombiesPoints.Get(p) == 240, "Rejected purchases leave the wallet and displays unchanged");
        Check(ZombiesPoints.TrySpend(child, 40) && ZombiesPoints.Get(p) == 200 && notices == 1,
            "Successful spending emits exactly one debit and ignores the earning multiplier");
        var world = p.GetComponentInChildren<TMPro.TextMeshPro>(true);
        Check(world != null && world.gameObject.activeSelf && world.text.Contains("-40") && world.text.Contains("+240"),
            "Overhead readout shows both earning and spending without cancelling either");
        Check(labels[0].text == "200", "Corner balance immediately reflects spending");
        p.transform.position += Vector3.right * 5;
        yield return null;
        Check(Mathf.Abs(world.transform.position.x - p.transform.position.x) < 0.01f,
            "Activity follows the moving player");
        Time.timeScale = 0;
        double resumeAt = EditorApplication.timeSinceStartup + 1.8;
        while (EditorApplication.timeSinceStartup < resumeAt) yield return null;
        Check(world.gameObject.activeSelf && world.alpha > 0.99f, "Pause holds the activity timer");
        Time.timeScale = 1;
        yield return Wait(PlayerPointsDisplay.HoldSeconds + 0.15f);
        Check(world.gameObject.activeSelf && world.alpha < 1 && world.alpha > 0, "Activity fades after its hold time");
        Call("Earn", p, 5);
        Check(world.alpha == 1, "A new transaction restores fading activity immediately");
        yield return Wait(PlayerPointsDisplay.HoldSeconds + PlayerPointsDisplay.FadeSeconds + 0.1f);
        Check(!world.gameObject.activeSelf && labels[0].text == "210", "Idle overhead disappears while the corner total stays");
        Call("Earn", p, 5);
        Check(world.text.Contains("+10") && !world.text.Contains("-40"), "A fresh activity window discards the old burst");
        p.SetActive(false);
        Check(!ZombiesPoints.TrySpend(p, 1), "Inactive players cannot purchase upgrades");
        Call("Earn", p, 5);
        p.SetActive(true);
        Check(labels[0].text == "220", "Re-enabled HUD catches up with delayed earnings while inactive");
        Call("Earn", p, 5);
        Check(ZombiesPoints.Get(p) == 230 && world.text.Contains("+10"), "Re-enabling a player does not duplicate event subscriptions");
        Check(ZombiesPoints.TrySpend(p, 230) && ZombiesPoints.Get(p) == 0, "Exact-balance purchases reach zero safely");
        Call("OnSceneLoaded", UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Additive);
        Check(ZombiesPoints.Get(players[1]) == 100 && ZombiesPoints.Multiplier == 2, "Additive scenery preserves the current run");
        Call("Earn", p, int.MaxValue);
        Call("Earn", p, 10);
        Check(ZombiesPoints.Get(p) == int.MaxValue, "Large rewards saturate instead of overflowing");
        ZombiesPoints.Multiplier = float.NaN;
        Call("Earn", players[1], 50);
        Check(ZombiesPoints.Get(players[1]) == 100, "Invalid multipliers cannot corrupt balances");
        ZombiesPoints.PointsChanged -= listener;
        Object.Destroy(spawner.gameObject);
        yield return null;
        Call("OnSceneLoaded", UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
        Check(!ZombiesPoints.Active && ZombiesPoints.Get(p) == 0 && ZombiesPoints.Multiplier == 1,
            "A new non-Zombies scene resets wallets and Double Points");
        Call("Earn", p, 100);
        Check(ZombiesPoints.Get(p) == 0 && !ZombiesPoints.TrySpend(p, 1), "Economy is inactive outside Zombies");
        results.Add("All points checks completed.");
    }
}
