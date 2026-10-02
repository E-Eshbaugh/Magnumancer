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

// Run in an isolated project with -batchmode -executeMethod ReviveGameplayChecks.Run.
// These checks use real physics, a baked NavMesh and the production ability components.
[InitializeOnLoad]
public static class ReviveGameplayChecks
{
    const string Pending = "Magnumancer.ReviveGameplayChecks";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new();
    static string current;
    static bool failed;
    static double deadline;

    static ReviveGameplayChecks()
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
        System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.dataPath, "../revive-checks.txt"), results);
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

    static IEnumerator Suite()
    {
        current = "Single health pool and downing";
        yield return Reset();
        var player = Player(Vector3.zero);
        var ally = Player(Vector3.right * 8);
        var enemy = Goblin(Vector3.forward * 8);
        var hp = player.GetComponent<PlayerHealthControl>();
        var allyHp = ally.GetComponent<PlayerHealthControl>();
        hp.SetHealthStat(3);
        allyHp.SetHealthStat(1);
        Check(hp.maxHealth == 300 && hp.currentHealth == 300 && allyHp.maxHealth == 100,
            "Wizard hearts scale a single health pool: three hearts 300 HP, one heart 100 HP");
        Check(hp.LivesLeft == 1 && hp.stockCount == 0, "No extra stocks remain");
        hp.TakeDamage(110, enemy.gameObject);
        Check(hp.currentHealth == 190 && hp.IsStanding, "Damage carries across old 100-HP stock boundaries");
        int downs = 0, deaths = 0, revives = 0, kills = 0;
        hp.OnDowned += () => downs++;
        hp.OnDeath += () => deaths++;
        hp.OnRevived += () => revives++;
        Action<GameObject, GameObject, Vector3> onKill = (victim, attacker, point) => { if (victim == player) kills++; };
        DamageEvents.Killed += onKill;
        var win = new GameObject("WinCheck").AddComponent<WinManager>();
        Call(win, "Update");
        yield return Wait(hp.hordeHitGrace + 0.05f);
        hp.TakeDamage(500, enemy.gameObject);
        Check(hp.IsDowned && !hp.IsDead && player.activeSelf && hp.currentHealth == 0,
            "Lethal damage leaves a downed wizard active at zero HP");
        Check(downs == 1 && kills == 1 && deaths == 0, "Downing reports one kill and no final death");
        Check(player.transform.Find("ReviveCircle").gameObject.activeSelf, "A visible rescue circle appears");
        Check(!DamageEvents.IsAlive(player) && !DropDirector.LivingPlayers().Contains(hp),
            "Downed players are excluded from combat and pickup collection");
        var chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        Check(!(bool)typeof(GoblinChaseNav).GetMethod("CanTarget", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player.transform }), "Zombies cannot target a downed wizard");
        Object.Destroy(chase);
        hp.Heal(100); hp.SetHealthFraction(1); hp.AddLife(); hp.TakeDamage(200, enemy.gameObject);
        Check(hp.currentHealth == 0 && hp.maxHealth == 300 && downs == 1,
            "Healing, health swaps, relics and lingering damage cannot bypass revival");
        Call(win, "Update");
        Check(!(bool)win.GetType().GetField("endSequenceStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(win),
            "Co-op does not end with one standing rescuer");
        var mover = player.GetComponent<PlayerMovement3D>();
        mover.AddKnockback(Vector3.right * 50);
        Check((Vector3)mover.GetType().GetField("knockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mover) == Vector3.zero,
            "Downed wizards ignore knockback");

        var pad = InputSystem.AddDevice<Gamepad>();
        mover.gamepad = pad;
        Vector3 beforeInput = player.transform.position;
        InputSystem.QueueDeltaStateEvent(pad.leftStick, Vector2.right);
        InputSystem.Update();
        Call(mover, "Update");
        Call(mover, "FixedUpdate");
        Check(Mathf.Approximately(player.transform.position.x, beforeInput.x) && mover.StickMagnitude == 0,
            "Controller movement input is ignored while downed");
        mover.gamepad = null;
        InputSystem.RemoveDevice(pad);
        player.transform.position = beforeInput;
        var gun = new GameObject("DownedGunCheck");
        gun.SetActive(false);
        gun.transform.SetParent(player.transform, false);
        var fire = gun.AddComponent<FireController3D>();
        var ammo = gun.AddComponent<AmmoControl>();
        Check(ammo.FiringBlocked, "Gun fire and weapon abilities are blocked while downed");
        // No fire point: a blocked shot must return before touching projectile setup.
        fire.Shoot(null, 0, 0);
        Check(PlayerHealthControl.IsIncapacitated(gun.transform), "Child weapons inherit the player's downed state");
        Object.Destroy(gun);

        current = "Rescue eligibility, interruption and timing";
        yield return Wait(0.2f);
        Check(hp.ReviveProgress == 0, "No progress while allies are outside the circle");
        ally.transform.position = Vector3.right * 2;
        Check(hp.CanBeRevivedBy(allyHp) && !hp.CanBeRevivedBy(hp), "Only a standing teammate inside the circle can revive");
        ally.transform.position = Vector3.right * 2 + Vector3.up * 2;
        Check(!hp.CanBeRevivedBy(allyHp), "Teammates on another elevation cannot revive");
        ally.transform.position = Vector3.right * 2;
        var coOpTeams = Teams.TeamOf;
        Teams.TeamOf = go => go == ally ? 1 : 0;
        Check(!hp.CanBeRevivedBy(allyHp), "Opponents cannot revive");
        Teams.TeamOf = coOpTeams;
        hp.reviveDuration = 1f;
        yield return Wait(0.3f);
        Check(hp.ReviveProgress > 0 && hp.ReviveProgress < 0.8f && hp.IsDowned,
            "Standing in the circle accumulates progress without instantly reviving");
        float beforePause = hp.ReviveProgress;
        GamePause.Pause();
        for (int i = 0; i < 5; i++) yield return null;
        Check(Mathf.Approximately(hp.ReviveProgress, beforePause), "Pause freezes revive progress");
        GamePause.Resume();
        ally.transform.position = Vector3.right * 8;
        yield return Wait(0.1f);
        Check(hp.ReviveProgress == 0 && hp.IsDowned, "Leaving the circle resets unfinished progress");
        ally.transform.position = Vector3.right * 2;
        yield return Wait(1.15f);
        Check(hp.IsStanding && !hp.IsDowned && hp.currentHealth == 150 && revives == 1,
            "A complete uninterrupted revive restores half the wizard's scaled HP");
        Check(player.transform.position == Vector3.zero && !player.transform.Find("ReviveCircle").gameObject.activeSelf,
            "Revival happens in place and hides the rescue marker");
        hp.TakeDamage(40, enemy.gameObject);
        Check(hp.currentHealth == 150, "Newly revived wizards have damage protection");
        yield return Wait(2.1f);
        hp.TakeDamage(40, enemy.gameObject);
        Check(hp.currentHealth == 110, "Revive protection expires and damage resumes");
        hp.AddLife();
        Check(hp.maxHealth == 400 && hp.currentHealth == 210 && hp.stockCount == 0,
            "Heart Relic adds HP without reintroducing stocks");

        current = "Repeat downing, wipe and solo";
        yield return Wait(hp.hordeHitGrace + 0.05f);
        hp.TakeDamage(500, enemy.gameObject);
        Check(hp.IsDowned && downs == 2 && deaths == 0, "A revived wizard can go down again");
        Check(!allyHp.CanBeRevivedBy(hp), "A downed wizard cannot rescue someone else");
        allyHp.TakeDamage(500, enemy.gameObject);
        Call(win, "Update");
        Check((bool)win.GetType().GetField("endSequenceStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(win),
            "A complete co-op wipe starts the return-to-menu sequence");
        Object.Destroy(win);
        Check(hp.IsDead && allyHp.IsDead && !player.activeSelf && !ally.activeSelf,
            "The last teammate falling eliminates the entire downed team");
        Check(deaths == 1 && kills == 2, "Team wipe emits one final death and no duplicate kill credit");
        DamageEvents.Killed -= onKill;
        yield return Reset();
        player = Player(Vector3.zero);
        hp = player.GetComponent<PlayerHealthControl>();
        enemy = Goblin(Vector3.forward * 8);
        hp.TakeDamage(500, enemy.gameObject);
        Check(hp.IsDead && !player.activeSelf, "Solo runs end instead of waiting for an impossible revive");

        current = "Free-for-all elimination and Last Rites";
        yield return Reset();
        player = Player(Vector3.zero);
        ally = Player(Vector3.right * 8);
        hp = player.GetComponent<PlayerHealthControl>();
        Teams.FreeForAll();
        hp.SetHealthStat(2);
        hp.TakeDamage(500, ally);
        Check(hp.IsDead && ally.GetComponent<PlayerHealthControl>().IsStanding,
            "Free-for-all eliminates only the fallen opponent without consuming stocks");
        yield return Reset();
        player = Player(Vector3.zero);
        hp = player.GetComponent<PlayerHealthControl>();
        var passive = player.AddComponent<LastRitesPassive>();
        passive.Init(player.GetComponent<PlayerMovement3D>().wizard);
        yield return null;
        Check(!passive.Empowered, "Last Rites does not become permanently available at full health");
        hp.SetHealthFraction(0.3f);
        yield return null;
        Check(passive.Empowered, "Last Rites activates at critical health");
        current = "Multiple helpers and lost teammate";
        yield return Reset();
        player = Player(Vector3.zero);
        hp = player.GetComponent<PlayerHealthControl>();
        ally = Player(Vector3.right * 2);
        var secondHelper = Player(Vector3.left * 2);
        enemy = Goblin(Vector3.forward * 8);
        hp.reviveDuration = 4f;
        hp.TakeDamage(500, enemy.gameObject);
        yield return Wait(0.4f);
        Check(hp.ReviveProgress > 0f && hp.ReviveProgress < 0.18f,
            "Multiple helpers do not multiply revival speed");
        ally.SetActive(false); secondHelper.SetActive(false);
        yield return null;
        yield return null;
        Check(hp.IsDead, "Losing the last available teammates cannot leave an endless downed state");
        results.Add("All revival gameplay checks completed.");
    }
}
