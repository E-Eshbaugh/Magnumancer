using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Run in an isolated project with -batchmode -executeMethod ZombiesAbilityChecks.Run.
// These checks use real physics, a baked NavMesh and the production ability components.
[InitializeOnLoad]
public static class ZombiesAbilityChecks
{
    const string Pending = "Magnumancer.ZombiesAbilityChecks";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new();
    static string current;
    static bool failed;
    static double deadline;

    static ZombiesAbilityChecks()
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
        System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.dataPath, "../zombies-checks.txt"), results);
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
        yield return LootChecks();

        current = "Enemy resolution and hordes";
        yield return Reset();
        var player = Player(Vector3.zero);
        var ally = Player(Vector3.forward);
        var enemy = Goblin(Vector3.forward * 3, true);
        Physics.SyncTransforms();
        Check(DamageEvents.RootOf(enemy.GetComponentInChildren<Collider>()) == enemy.gameObject, "Child hitboxes resolve to goblin health");
        Check(AbilityKit.NearestEnemy(player, 8) == enemy.gameObject, "Auto-target spells prefer goblins over co-op teammates");
        for (int i = 0; i < 80; i++) Goblin(new Vector3((i % 10) * 0.5f - 2, 0, 4 + i / 10 * 0.5f));
        Physics.SyncTransforms();
        Check(AbilityKit.Enemies(Vector3.zero, 20, player).Count == 81, "Area spells find all 81 goblins beyond the old buffer limit");
        Teams.FreeForAll();
        Check(AbilityKit.Enemies(Vector3.zero, 20, player).Contains(ally), "Deathmatch still targets opposing wizards");

        current = "Soul Swap and Fireball";
        yield return Reset();
        player = Player(Vector3.zero);
        enemy = Goblin(Vector3.forward * 3);
        player.GetComponent<PlayerHealthControl>().SetHealthFraction(0.25f);
        enemy.SetHealthFraction(0.8f);
        Physics.SyncTransforms();
        var swap = player.AddComponent<SoulSwapAbility>();
        swap.Activate(player);
        Check(!swap.Fizzled && Mathf.Approximately(enemy.currentHealth, 250) && Mathf.Approximately(player.GetComponent<PlayerHealthControl>().currentHealth, 80), "Soul Swap trades player and boss health percentages");
        var fireball = player.AddComponent<FireballAbility>();
        Check((GameObject)Call(fireball, "NearestEnemy", player) == enemy.gameObject, "Fireball locks onto goblins");
        fireball.Activate(player);
        yield return Wait(1.1f);
        Check(enemy.currentHealth < 250, "Fireball blast and lava damage goblins");
        Object.Destroy(enemy.gameObject);
        yield return null;
        swap.Activate(player);
        Check(swap.Fizzled, "Soul Swap fizzles when no opponent remains");

        current = "Knockback, zones and cleanup";
        yield return Reset();
        player = Player(new Vector3(-10, 0, 0), PassiveType.Undercurrent);
        enemy = Goblin(Vector3.zero);
        yield return null;
        var agent = enemy.GetComponent<NavMeshAgent>();
        var status = StatusEffects.Of(enemy.gameObject);
        status.AddFreeze();
        status.SetSpeedModifier("test-zone", 0.5f);
        Check(Mathf.Abs(agent.speed - 1.84f) < 0.01f, "Zone slows combine with goblin freeze counters");
        status.ClearSpeedModifier("test-zone");
        Check(Mathf.Abs(agent.speed - 3.68f) < 0.01f, "Leaving a zone preserves the remaining freeze slow");
        status.ClearFreeze();
        AbilityKit.Knockback(enemy.gameObject, Vector3.right * 12);
        yield return Wait(0.2f);
        Check(enemy.transform.position.x > 0.5f && agent.isOnNavMesh, "Knockback moves goblins along the NavMesh");
        yield return Wait(0.5f);
        Check(!agent.isStopped, "Goblin navigation resumes after knockback");
        agent.Warp(Vector3.right * 2);
        var hazard = GroundHazard.Spawn(player, Vector3.zero, 4, 2, Color.cyan);
        hazard.element = Element.Water; hazard.pullStrength = 4.5f; hazard.slowMultiplier = 0.5f;
        yield return Wait(0.3f);
        Check(enemy.transform.position.x < 1.8f && agent.speed < 4, "Undertow pulls and slows goblins");
        Object.Destroy(hazard.gameObject);
        yield return Wait(0.3f);
        Check(Mathf.Approximately(agent.speed, 4), "Destroyed slowing zones restore goblin speed");

        current = "Lava and poison compound colliders";
        yield return Reset();
        player = Player(new Vector3(-10, 0, 0), PassiveType.BrandOfFlereous);
        enemy = Goblin(Vector3.zero, true);
        var extra = enemy.gameObject.AddComponent<BoxCollider>(); extra.center = Vector3.up;
        var lava = new GameObject("LavaCheck").AddComponent<LavaTrail>(); lava.owner = player;
        Call(lava, "OnTriggerEnter", enemy.GetComponentInChildren<CapsuleCollider>());
        Call(lava, "OnTriggerEnter", extra);
        yield return Wait(0.1f);
        Check(enemy.currentHealth < 1000 && enemy.GetComponent<NavMeshAgent>().speed < 4, "Blazing Ruin lava damages and slows goblins");
        Call(lava, "OnTriggerExit", extra);
        Check(enemy.GetComponent<NavMeshAgent>().speed < 4, "One collider exiting lava does not clear another collider's slow");
        Object.Destroy(lava.gameObject);
        yield return null;
        Check(Mathf.Approximately(enemy.GetComponent<NavMeshAgent>().speed, 4), "Lava cleanup restores goblin speed");
        StatusEffects.Of(enemy.gameObject).ClearElementStatuses();
        var poison = new GameObject("PoisonCheck").AddComponent<PoisonCloudHazard>(); poison.owner = player;
        float hp = enemy.currentHealth;
        Call(poison, "OnTriggerEnter", enemy.GetComponentInChildren<CapsuleCollider>());
        Call(poison, "OnTriggerEnter", extra);
        Check(Mathf.Approximately(enemy.currentHealth, hp - poison.damagePerSecond), "Poison handles child hitboxes and damages a compound enemy once");

        current = "Quake, Blinkstorm and void beam";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.Stonebind);
        enemy = Goblin(Vector3.forward * 3, true);
        Physics.SyncTransforms();
        var quake = player.AddComponent<EarthquakeAbility>(); quake.damageLayers = ~0; quake.windup = 0; quake.totalDuration = 0.25f;
        quake.Activate(player);
        yield return Wait(0.1f);
        Check(enemy.currentHealth < 1000 && enemy.GetComponent<NavMeshAgent>().speed < 4, "Seismic Judgement damages and stuns goblins with child hitboxes");
        yield return Wait(0.5f);
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.forward * 2);
        Physics.SyncTransforms();
        hp = enemy.currentHealth;
        var blast = player.AddComponent<LightningBlastDamage>(); blast.hitLayer = ~0;
        blast.TriggerBlast(player.transform.position, player);
        Check(enemy.currentHealth < hp, "Blinkstorm damages goblins through their child hitboxes");
        var beam = new GameObject("BeamCheck").AddComponent<VoidShotProjectile>(); beam.caster = player;
        hp = enemy.currentHealth;
        Call(beam, "OnTriggerEnter", enemy.GetComponentInChildren<Collider>());
        Call(beam, "OnTriggerEnter", enemy.GetComponentInChildren<Collider>());
        Check(Mathf.Approximately(enemy.currentHealth, hp - beam.damage) && StatusEffects.Of(enemy.gameObject).VoidMarkedBy == player, "Soulfracture marks and damages each goblin once");
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.position = new Vector3(0, 1, 1);
        Physics.SyncTransforms();
        Check(!AbilityKit.ClearPath(Vector3.up, enemy.gameObject, player), "Solid cover still blocks area spell line of sight");

        current = "Clone distraction and defensive cover";
        yield return Reset();
        player = Player(Vector3.forward * 8, PassiveType.Undercurrent); player.tag = "Player1";
        enemy = Goblin(Vector3.zero);
        var clone = new GameObject("CloneCheck").AddComponent<WaterCloneDecoy>(); clone.transform.position = Vector3.forward * 2;
        clone.Setup(player, 5, Color.cyan);
        var chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        yield return Wait(0.3f);
        var target = (Transform)typeof(GoblinChaseNav).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chase);
        Check(target == clone.transform, "Goblins chase a nearer Shadow Clone");
        Object.Destroy(clone.gameObject);
        yield return Wait(0.3f);
        target = (Transform)typeof(GoblinChaseNav).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chase);
        Check(target == player.transform, "Goblins retarget the player when the clone disappears");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = wall.AddComponent<MeshCollider>(); mesh.sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh; mesh.convex = true;
        AbilityKit.BlockNavigation(mesh);
        Check(wall.GetComponent<NavMeshObstacle>().carving, "Runtime cover carves goblin navigation");
        var bastion = player.AddComponent<BastionStanceAbility>(); bastion.Activate(player);
        yield return Wait(0.3f);
        Check(Object.FindObjectsByType<NavMeshObstacle>().Length >= 7, "Bastion's six slabs block the horde");

        current = "Alternate offensive runes";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.LightningReflex);
        enemy = Goblin(Vector3.forward * 3);
        Physics.SyncTransforms();
        player.AddComponent<ChainSurgeAbility>().Activate(player);
        Check(enemy.currentHealth < 1000 && enemy.GetComponent<NavMeshAgent>().speed < 4, "Chain Surge damages and stuns goblins");
        var fence = new GameObject("FenceCheck").AddComponent<LightningFence>();
        fence.Init(player, Vector3.forward * 2, Vector3.forward * 4, Color.cyan);
        hp = enemy.currentHealth;
        yield return Wait(0.1f);
        Check(enemy.currentHealth < hp, "Stormrunner lightning fences shock goblins");
        player.GetComponent<PlayerMovement3D>().wizard.passive = PassiveType.FractalshotShield;
        var wind = player.AddComponent<WintersWindAbility>(); wind.Activate(player);
        yield return Wait(0.25f);
        Check(StatusEffects.Of(enemy.gameObject).FreezeStacks > 0, "Winter's Wind chills goblins");
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.forward * 2);
        player.AddComponent<FlashFreezeAbility>().Activate(player);
        yield return Wait(0.15f);
        Check(StatusEffects.Of(enemy.gameObject).Has(ElementStatus.Rooted), "Flash Freeze encases goblins");

        current = "Nature and poison runes";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.VerdantResurgence);
        player.GetComponent<PlayerHealthControl>().SetHealthFraction(0.5f);
        enemy = Goblin(Vector3.forward * 3);
        Physics.SyncTransforms();
        var vine = player.AddComponent<SiphoningVineAbility>(); vine.Activate(player);
        yield return Wait(0.5f);
        Check(enemy.currentHealth < 1000 && player.GetComponent<PlayerHealthControl>().currentHealth > 50, "Siphoning Vine drains goblins to heal its caster");
        var snare = player.AddComponent<ThornsnareAbility>(); snare.distance = 3; snare.Activate(player);
        yield return Wait(0.8f);
        Check(StatusEffects.Of(enemy.gameObject).Has(ElementStatus.Rooted), "Thornsnare roots goblins");
        player.GetComponent<PlayerMovement3D>().wizard.passive = PassiveType.VirulentShroud;
        hp = enemy.currentHealth;
        player.AddComponent<PlaguebearerAbility>().Activate(player);
        yield return Wait(0.6f);
        Check(enemy.currentHealth < hp && enemy.GetComponent<Plague>() != null, "Plaguebearer builds damaging plague on goblins");
        player.AddComponent<ContagionAbility>().Activate(player);
        yield return Wait(0.5f);
        Check(enemy.GetComponent<Infection>() != null, "Contagion infects goblins");

        current = "Portals, Riptide and Inferno Rounds";
        yield return Reset();
        player = Player(new Vector3(-10, 0, 0), PassiveType.Undercurrent);
        enemy = Goblin(Vector3.forward * 2);
        var rift = new GameObject("RiftCheck").AddComponent<VoidRift>();
        rift.Begin(player, Vector3.zero, 1, Color.magenta, 1.4f, 2);
        rift.Link(Vector3.right * 10, 5);
        yield return Wait(0.5f);
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.zero);
        yield return Wait(0.15f);
        Check(Vector3.Distance(enemy.transform.position, Vector3.right * 10) < 1, "Void Rift transports goblins to a valid NavMesh landing");
        yield return Wait(0.2f);
        Check(enemy.transform.position.x > 9, "Goblins do not bounce repeatedly between portals");
        Object.Destroy(rift.gameObject);
        player.GetComponent<PlayerMovement3D>().Teleport(Vector3.zero, Quaternion.identity);
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.forward * 1.5f);
        Physics.SyncTransforms();
        hp = enemy.currentHealth;
        player.AddComponent<RiptideAbility>().Activate(player);
        yield return Wait(0.3f);
        Check(enemy.currentHealth < hp && player.transform.position.z > 1, "Riptide surges through and damages goblins");
        player.AddComponent<InfernoRoundsAbility>().Activate(player);
        hp = enemy.currentHealth;
        enemy.TakeDamage(10, player);
        Check(Mathf.Abs(hp - enemy.currentHealth - 13.5f) < 0.01f, "Inferno Rounds amplifies damage against goblins");

        current = "Ice wall and Earthwork Parapet";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.Stonebind);
        enemy = Goblin(Vector3.forward * 1.5f);
        Physics.SyncTransforms();
        var parapet = player.AddComponent<EarthworkParapetAbility>(); parapet.Activate(player);
        yield return Wait(0.5f);
        Check(enemy.currentHealth < 1000 && Object.FindObjectsByType<NavMeshObstacle>().Length > 0, "Earthwork Parapet damages nearby goblins and creates navigation cover");
        var ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ice.transform.position = Vector3.right * 10;
        var iceMesh = ice.AddComponent<MeshCollider>(); iceMesh.sharedMesh = ice.GetComponent<MeshFilter>().sharedMesh; iceMesh.convex = true;
        var wallEffect = ice.AddComponent<IceWallEffect>(); wallEffect.BeginRise();
        yield return Wait(0.4f);
        Check(ice.GetComponent<NavMeshObstacle>() != null && ice.GetComponent<NavMeshObstacle>().carving, "Covenant of Frostgrave blocks goblin navigation after rising");
        wallEffect.Shatter();
        yield return Wait(0.6f);
        Check(ice == null, "Melted ice removes its navigation obstacle");

        current = "Blazing Ruin and Viper mines";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.BrandOfFlereous);
        enemy = Goblin(Vector3.forward * 1.5f, true);
        Physics.SyncTransforms();
        var dash = player.AddComponent<FireDashAbility>();
        var trailTemplate = new GameObject("TrailTemplate"); trailTemplate.transform.position = Vector3.right * 100;
        Set(dash, "lavaTrailPrefab", trailTemplate); Set(dash, "enemyLayer", (LayerMask)(~0));
        dash.Activate(player);
        yield return Wait(0.25f);
        Check(enemy.currentHealth < 1000 && !player.GetComponent<PlayerHealthControl>().invincible, "Blazing Ruin hits goblin child hitboxes and ends dash invulnerability");
        var cloudTemplate = new GameObject("CloudTemplate"); cloudTemplate.transform.position = Vector3.right * 100;
        cloudTemplate.AddComponent<PoisonCloudHazard>();
        var mine = new GameObject("MineCheck").AddComponent<MineExplosionController>();
        mine.owner = player; mine.poisonCloudPrefab = cloudTemplate; mine.transform.position = enemy.transform.position;
        yield return Wait(0.7f);
        Check(mine == null && Object.FindObjectsByType<PoisonCloudHazard>().Length == 2, "Viper mines detect goblin child hitboxes and release poison");

        current = "Healing totem";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.VerdantResurgence); player.tag = "Player1";
        var mid = new GameObject("PlayerMidPos"); mid.transform.SetParent(player.transform, false); mid.transform.localPosition = Vector3.up;
        player.GetComponent<PlayerHealthControl>().SetHealthFraction(0.5f);
        var totemTemplate = new GameObject("TotemTemplate"); totemTemplate.SetActive(false);
        totemTemplate.transform.position = Vector3.right * 100;
        var crystal = new GameObject("Crystal"); crystal.transform.SetParent(totemTemplate.transform, false); crystal.AddComponent<CrystalHealth>();
        var beams = totemTemplate.AddComponent<HealingBeamController>(); beams.beamOrigin = crystal.transform;
        var beamTemplate = new GameObject("HealBeamTemplate"); beamTemplate.AddComponent<LineRenderer>(); beamTemplate.SetActive(false);
        beams.beamPrefab = beamTemplate;
        var healing = totemTemplate.AddComponent<HealingZone>(); healing.beamController = beams;
        totemTemplate.SetActive(true);
        var heal = player.AddComponent<HealAbilityActivator>(); heal.healCirclePrefab = totemTemplate; heal.Activate(player);
        yield return Wait(0.4f);
        Check(player.GetComponent<PlayerHealthControl>().currentHealth > 50, "Seed of Aloria heals the co-op wizard");

        current = "Passive rewards and reentrant damage";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.FractalshotShield);
        enemy = Goblin(Vector3.forward * 3);
        var frost = player.AddComponent<FractalshotShieldPassive>(); frost.Init(player.GetComponent<PlayerMovement3D>().wizard);
        enemy.TakeDamage(100, player);
        Check(frost.Shield > 0 && enemy.GetComponent<NavMeshAgent>().speed < 4, "Fractalshot gains a shield by freezing a goblin");
        Object.Destroy(frost);
        yield return null;
        player.GetComponent<PlayerHealthControl>().SetHealthFraction(0.5f);
        var leech = player.AddComponent<LeechSporesPassive>(); leech.Init(player.GetComponent<PlayerMovement3D>().wizard);
        enemy.TakeDamage(20, player);
        Check(player.GetComponent<PlayerHealthControl>().currentHealth > 50, "Leech Spores heals from goblin damage");
        Object.Destroy(leech);
        yield return null;
        var souls = player.AddComponent<SoulHarvestPassive>(); souls.Init(player.GetComponent<PlayerMovement3D>().wizard);
        var cold = player.AddComponent<ColdPrecisionPassive>(); cold.Init(player.GetComponent<PlayerMovement3D>().wizard);
        enemy.currentHealth = 10; StatusEffects.Of(enemy.gameObject).FreezeAtLeast(5);
        int deaths = 0;
        Action<GameObject, GameObject, Vector3> killed = (victim, attacker, at) => { if (victim == enemy.gameObject) deaths++; };
        DamageEvents.Killed += killed;
        enemy.TakeDamage(8, player); // bonus damage kills inside the outer damage event
        DamageEvents.Killed -= killed;
        Check(deaths == 1 && Mathf.Abs(souls.ModifyOutgoing(100) - 106) < 0.01f, "A nested passive kill awards one death and one Soul Harvest stack");
        var cooldown = player.AddComponent<AbilityCooldown>();
        cooldown.TriggerCooldown(); cooldown.Refresh();
        Check(!cooldown.IsOnCooldown() && cooldown.Charge == 1, "Kindling's refresh immediately makes an ability ready");
        current = "Strict humans versus zombies damage rules";
        yield return Reset();
        player = Player(Vector3.zero, PassiveType.BrandOfFlereous);
        ally = Player(Vector3.forward * 2, PassiveType.FractalshotShield);
        enemy = Goblin(Vector3.right * 3);
        var human = ally.GetComponent<PlayerHealthControl>();
        int humanHits = 0;
        Action<GameObject, GameObject, float> damaged = (victim, attacker, amount) => { if (victim == ally) humanHits++; };
        DamageEvents.Damaged += damaged;
        human.TakeDamage(1000, player);
        human.TakeDamage(1000, ally);
        human.TakeDamage(1000, null);
        DamageEvents.Deal(ally, 1000, player);
        ElementReactions.DealAs(Reaction.Combust, ally, 1000, player);
        ElementReactions.DealAs(Reaction.Conduct, ally, 1000, null);
        Check(human.currentHealth == 100 && humanHits == 0, "Friendly, self, environmental, and reaction damage cannot hurt humans or trigger hit passives");
        var bullet = new GameObject("FriendlyBulletCheck").AddComponent<Bullet>(); bullet.owner = player; bullet.damage = 90;
        Call(bullet, "HandleHit", ally.GetComponent<CharacterController>(), ally.transform.position + Vector3.up, Vector3.back);
        Check(human.currentHealth == 100, "Friendly gunfire deals zero damage");
        AbilityKit.Knockback(ally, Vector3.right * 30, player);
        var velocity = (Vector3)typeof(PlayerMovement3D).GetField("knockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ally.GetComponent<PlayerMovement3D>());
        Check(velocity == Vector3.zero, "Friendly bullets and explosions cannot knock back humans");
        ElementReactions.AbilityHit(ally, player, Element.Fire, 50);
        ElementReactions.ZoneHit(ally, null, Element.Poison, 50);
        Check(ally.GetComponent<StatusEffects>() == null, "Friendly spells and environmental zones cannot apply harmful elemental statuses");
        var friendlyZone = GroundHazard.Spawn(player, ally.transform.position, 3, 1, Color.red);
        friendlyZone.damagePerSecond = 100; friendlyZone.slowMultiplier = 0.2f; friendlyZone.pullStrength = 10; friendlyZone.fromReaction = Reaction.Combust;
        yield return Wait(0.3f);
        Check(human.currentHealth == 100 && ally.GetComponent<PlayerMovement3D>().moveSpeedMultiplier == 1, "Lingering reaction zones neither damage nor slow teammates");
        var friendlyPoison = new GameObject("FriendlyPoisonCheck").AddComponent<PoisonCloudHazard>(); friendlyPoison.owner = player;
        Call(friendlyPoison, "OnTriggerEnter", ally.GetComponent<CharacterController>());
        Check(human.currentHealth == 100, "Friendly poison clouds spare teammates");
        float monsterHealth = enemy.currentHealth;
        enemy.TakeDamage(10, player);
        Check(enemy.currentHealth < monsterHealth, "Human damage still hurts zombies");
        monsterHealth = enemy.currentHealth;
        enemy.TakeDamage(10, enemy.gameObject);
        Check(enemy.currentHealth == monsterHealth, "Zombies cannot friendly-fire other zombies");
        human.TakeDamage(25, enemy.gameObject);
        Check(human.currentHealth == 75 && humanHits == 1, "Zombie attacks still damage humans and raise hit events");
        human.Heal(10);
        Check(human.currentHealth == 85, "Co-op healing still works under the damage guard");
        DamageEvents.Damaged -= damaged;
        var thorns = ally.AddComponent<ThornhidePassive>(); thorns.Init(ally.GetComponent<PlayerMovement3D>().wizard);
        monsterHealth = enemy.currentHealth;
        yield return Wait(human.hordeHitGrace + 0.05f);
        human.TakeDamage(8, enemy.gameObject);
        Check(enemy.currentHealth < monsterHealth, "Thornhide reflects a nearby zombie's attack");
        Object.Destroy(thorns);
        yield return null;
        Object.Destroy(friendlyZone.gameObject);
        yield return Wait(human.hordeHitGrace + 0.05f);
        Teams.FreeForAll();
        float humanHealth = human.currentHealth;
        human.TakeDamage(5, player); human.TakeDamage(5, null);
        Check(Mathf.Approximately(human.currentHealth, humanHealth - 10), "Leaving zombies restores deathmatch damage rules");

        yield return NavigationChecks();
        yield return MeleeChecks();
        results.Add("All zombies ability and melee gameplay checks completed.");
    }

    static GameObject Block(string name, Vector3 center, Vector3 size)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = center;
        block.transform.localScale = size;
        return block;
    }

    static void BakeNavigation()
    {
        Physics.SyncTransforms();
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.1f;
        surface.buildHeightMesh = true;
        surface.BuildNavMesh();
    }

    static void HordeBody(GoblinHealth enemy, bool boss = false)
    {
        var agent = enemy.GetComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        var body = enemy.GetComponent<CapsuleCollider>();
        body.radius = boss ? 0.5f : 0.7f;
        body.height = boss ? 2.41f : 2.64f;
        body.center = Vector3.up * (body.height * 0.5f);
    }

    static IEnumerator NavigationChecks()
    {
        current = "Steady pursuit and intentional slows";
        yield return Reset();
        // Exercise the scene-load rebuild, including duplicate whole-scene surfaces.
        var duplicate = new GameObject("DuplicateSurface").AddComponent<NavMeshSurface>();
        duplicate.BuildNavMesh();
        typeof(DestructibleSetup).GetMethod("PrepareNavmesh", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new List<Destructible>() });
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        int activeSurfaces = 0;
        foreach (var surface in surfaces)
            if (surface.enabled && surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders && surface.buildHeightMesh) activeSurfaces++;
        Check(activeSurfaces == 1 && surfaces.Length == 2,
            "Scene rebuild keeps one collision-based navigation surface with accurate stair heights");
        var player = Player(Vector3.forward * 20); player.tag = "Player1";
        var enemy = Goblin(Vector3.zero);
        var chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        var agent = enemy.GetComponent<NavMeshAgent>();
        yield return Wait(0.7f);
        Check(agent.velocity.magnitude > 3.5f && !agent.autoBraking, "Open-ground pursuit reaches a steady running speed");
        var status = StatusEffects.Of(enemy.gameObject);
        status.SetSpeedModifier("navigation-check", 0.5f);
        yield return Wait(0.4f);
        Check(Mathf.Approximately(agent.speed, 2f) && agent.velocity.magnitude < 2.2f, "Navigation preserves intentional spell slows");
        status.ClearSpeedModifier("navigation-check");
        yield return Wait(0.4f);
        Check(agent.velocity.magnitude > 3.5f, "Full movement speed returns after the slow ends");

        current = "Stair pursuit and elevated melee";
        yield return Reset();
        for (int i = 0; i < 10; i++)
            Block("Step", new Vector3(0, (i + 1) * 0.1f, 2 + i * 0.3f), new Vector3(5, (i + 1) * 0.2f, 0.3f));
        Block("StairLanding", new Vector3(0, 1, 6), new Vector3(5, 2, 3));
        BakeNavigation();
        player = Player(new Vector3(0, 2, 6)); player.tag = "Player1";
        enemy = Goblin(Vector3.zero);
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        yield return Windup(chase);
        yield return Wait(chase.attackWindup + 0.1f);
        Check(enemy.transform.position.y > 0.5f && player.GetComponent<PlayerHealthControl>().currentHealth < 100,
            "Zombie climbs stairs and attacks a wizard on the landing");
        // Explicitly exercise the old 1.1-unit height cutoff on one continuous slope.
        chase.enabled = false;
        enemy.GetComponent<NavMeshAgent>().Warp(new Vector3(0, 0.6f, 2.6f));
        player.transform.position = new Vector3(0, 1.8f, 4.35f);
        Physics.SyncTransforms();
        Check((bool)Call(chase, "InReach", player.transform), "Grounded melee reaches along a staircase above the old height cutoff");
        player.transform.position += Vector3.up * 2;
        Physics.SyncTransforms();
        Check(!(bool)Call(chase, "InReach", player.transform), "Jumping above the staircase still dodges melee");

        current = "Jump onto a ledge and resume combat";
        yield return Reset();
        Block("JumpPlatform", new Vector3(0, 0.7f, 5), new Vector3(8, 1.4f, 5));
        BakeNavigation();
        player = Player(new Vector3(0, 1.4f, 5)); player.tag = "Player1";
        enemy = Goblin(Vector3.zero);
        HordeBody(enemy);
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        var traversal = enemy.GetComponent<GoblinTraversal>();
        bool jumped = false;
        float until = Time.time + 7;
        while (Time.time < until && !chase.IsWindingUp)
        { jumped |= traversal.IsJumping; yield return null; }
        Check(jumped && enemy.transform.position.y > 1.2f && chase.IsWindingUp, "Zombie jumps onto a reachable ledge and resumes attacking");
        Check(enemy.GetComponent<NavMeshAgent>().updatePosition && !traversal.IsJumping, "Landing restores normal navigation ownership");

        current = "Vault low cover, reject tall walls and ceilings";
        yield return Reset();
        var cover = Block("LowCover", new Vector3(0, 0.4f, 3), new Vector3(20, 0.8f, 0.5f));
        // Real destructible cover is excluded from the bake and carves at runtime.
        cover.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var obstacle = cover.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        BakeNavigation();
        yield return Wait(0.3f);
        player = Player(Vector3.forward * 7); player.tag = "Player1";
        enemy = Goblin(Vector3.forward);
        HordeBody(enemy, true);
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        traversal = enemy.GetComponent<GoblinTraversal>();
        jumped = false;
        until = Time.time + 7;
        while (Time.time < until && !chase.IsWindingUp)
        { jumped |= traversal.IsJumping; yield return null; }
        Check(jumped && enemy.transform.position.z > 3.5f, "Boss-sized zombie vaults carved cover instead of taking a long detour");

        for (int obstruction = 0; obstruction < 2; obstruction++)
        {
            yield return Reset();
            Block("Cover", new Vector3(0, obstruction == 0 ? 2 : 0.4f, 3), new Vector3(20, obstruction == 0 ? 4 : 0.8f, 0.5f));
            if (obstruction == 1) Block("LowCeiling", new Vector3(0, 2.4f, 3), new Vector3(20, 0.2f, 5));
            BakeNavigation();
            player = Player(Vector3.forward * 7); player.tag = "Player1";
            enemy = Goblin(Vector3.forward * 1.5f);
            chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
            traversal = enemy.GetComponent<GoblinTraversal>();
            jumped = false;
            until = Time.time + 1.5f;
            while (Time.time < until) { jumped |= traversal.IsJumping; yield return null; }
            Check(!jumped, obstruction == 0 ? "Tall walls cannot be jumped through" : "Low ceilings prevent unsafe vaults");
        }

        current = "Interrupted jump restores navigation";
        yield return Reset();
        Block("InterruptCover", new Vector3(0, 0.4f, 3), new Vector3(20, 0.8f, 0.5f));
        BakeNavigation();
        player = Player(Vector3.forward * 7); player.tag = "Player1";
        enemy = Goblin(Vector3.forward);
        HordeBody(enemy, true);
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        traversal = enemy.GetComponent<GoblinTraversal>();
        until = Time.time + 4;
        while (!traversal.IsJumping && Time.time < until) yield return null;
        Check(traversal.IsJumping, "Jump interruption fixture starts a vault");
        StatusEffects.Of(enemy.gameObject).Stun(0, 0.7f);
        yield return Wait(0.2f);
        agent = enemy.GetComponent<NavMeshAgent>();
        Check(!traversal.IsJumping && agent.isOnNavMesh && agent.updatePosition && enemy.transform.position.y < 0.3f,
            "Stun cancels a jump safely without leaving the zombie suspended");
        yield return Wait(4);
        Check(enemy.transform.position.z > 3.5f, "Zombie resumes pursuit and vaulting after interruption");
    }

    static IEnumerator LootChecks()
    {
        current = "Zombie kill loot";
        yield return Reset();
        var spawner = new GameObject("LootSpawner").AddComponent<GoblinSpawner>();
        spawner.totalRounds = 0;
        spawner.zombieDropChance = 0;
        var player = Player(Vector3.zero);
        var prefab = Goblin(Vector3.right * 25).gameObject;
        Physics.SyncTransforms();

        // Exercise the paced wave path, including fifth-wave boss classification.
        for (int category = 0; category < 3; category++)
        {
            yield return SpawnLootFixture(spawner, prefab, category);
            var tracker = Object.FindAnyObjectByType<GoblinDeathTracker>();
            var monster = tracker.GetComponent<GoblinHealth>();
            Vector3 killedAt = monster.transform.position;
            int before = Pickup.Live.Count;
            monster.TakeDamage(1);
            Check(Pickup.Live.Count == before, "Nonlethal hits do not drop loot for category " + category);
            monster.TakeDamage(10000);
            int expected = before + (category == 0 ? 0 : 1);
            Check(Pickup.Live.Count == expected, "Regular zombies respect zero chance; boss category " + category + " guarantees one drop");
            monster.TakeDamage(10000);
            DamageEvents.RaiseKilled(monster.gameObject, null, monster.transform.position);
            Check(Pickup.Live.Count == expected, "Repeated death callbacks cannot duplicate loot for category " + category);
            if (category != 0)
            {
                var drop = Pickup.Live[Pickup.Live.Count - 1];
                Check(Vector3.Distance(drop.transform.position, killedAt) < 0.2f,
                    "Boss loot appears on the floor at the kill location");
                Check(drop.Def.id != ItemId.HeartRelic, "Early boss loot respects the Heart Relic time gate");
            }
            yield return null;
            yield return null;
        }

        spawner.zombieDropChance = 1;
        spawner.lootLifetime = 0.5f;
        yield return SpawnLootFixture(spawner, prefab, 0);
        var regular = Object.FindAnyObjectByType<GoblinDeathTracker>().GetComponent<GoblinHealth>();
        int count = Pickup.Live.Count;
        regular.TakeDamage(10000);
        Check(Pickup.Live.Count == count + 1, "Regular kills drop existing PvP pickups when the chance succeeds");
        var timedLoot = Pickup.Live[Pickup.Live.Count - 1];
        yield return Wait(0.7f);
        Check(timedLoot == null, "Uncollected zombie loot expires");

        count = Pickup.Live.Count;
        var untracked = Goblin(Vector3.right * 8);
        untracked.TakeDamage(10000);
        Check(Pickup.Live.Count == count, "Untracked monsters do not award another spawner's loot");
        yield return SpawnLootFixture(spawner, prefab, 2);
        Object.Destroy(Object.FindAnyObjectByType<GoblinDeathTracker>().gameObject);
        yield return null;
        Check(Pickup.Live.Count == count, "Cleanup and despawning bosses do not create loot");
    }

    static IEnumerator SpawnLootFixture(GoblinSpawner spawner, GameObject prefab, int category)
    {
        spawner.baseMonsters = category == 0 ? new[] { prefab } : null;
        spawner.midBossMonsters = category == 1 ? new[] { prefab } : null;
        spawner.bossMonsters = category == 2 ? new[] { prefab } : null;
        spawner.openingZombieCount = 1;
        spawner.baseMonsterFactor = category == 0 ? 1 : 0;
        spawner.midBossFactor = category == 1 ? 1 : 0;
        spawner.bossMonsterFactor = category == 2 ? 1 : 0;
        int wave = category == 0 ? 1 : category == 1 ? 3 : 5;
        Set(spawner, "currentWave", wave - 1);
        spawner.totalRounds = wave;
        spawner.StartNextWave();
        float deadline = Time.time + 3f;
        while (spawner.AliveCount == 0 && Time.time < deadline) yield return null;
        Check(spawner.AliveCount == 1, "Paced loot fixture spawned category " + category);
    }

    static IEnumerator Windup(GoblinChaseNav chase)
    {
        float end = Time.time + 4;
        while (!chase.IsWindingUp && Time.time < end) yield return null;
        if (!chase.IsWindingUp) throw new Exception("Zombie never reached its target and began a swing.");
    }

    static IEnumerator MeleeChecks()
    {
        current = "Real zombie melee, attribution and recovery";
        yield return Reset();
        var player = Player(Vector3.forward * 4); player.tag = "Player1";
        var hp = player.GetComponent<PlayerHealthControl>();
        var enemy = Goblin(Vector3.zero);
        var chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        int hits = 0;
        GameObject source = null;
        Action<GameObject, GameObject, float> onHit = (victim, attacker, amount) =>
        { if (victim == player) { hits++; source = attacker; } };
        DamageEvents.Damaged += onHit;
        yield return Windup(chase);
        Check(hp.currentHealth == 100 && enemy.transform.position.z > 1,
            "Zombie navigates into range and warns before dealing damage");
        Check(enemy.transform.Find("GoblinAttackWarning") != null, "Melee windup creates a visible reach warning");
        yield return Wait(chase.attackWindup + 0.1f);
        Check(hp.currentHealth == 88 && hits == 1 && source == enemy.gameObject,
            "A real zombie swing deals 12 damage once and credits its attacker");
        yield return Wait(0.3f);
        Check(hits == 1 && enemy.transform.Find("GoblinAttackWarning") == null,
            "Recovery prevents repeated contact damage and removes the warning");
        yield return Wait(1.2f);
        Check(hits == 2, "Zombie resumes attacking after recovery with a fresh windup");
        DamageEvents.Damaged -= onHit;

        for (int dodge = 0; dodge < 4; dodge++)
        {
            current = "Melee dodge/cover " + dodge;
            yield return Reset();
            player = Player(Vector3.forward * 1.5f); player.tag = "Player1";
            player.GetComponent<PlayerMovement3D>().enabled = false;
            hp = player.GetComponent<PlayerHealthControl>();
            enemy = Goblin(Vector3.zero);
            chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
            yield return Windup(chase);
            if (dodge == 0) player.transform.position = Vector3.forward * 4;
            if (dodge == 1) player.transform.position = Vector3.back * 1.5f;
            if (dodge == 2) player.transform.position += Vector3.up * 2;
            if (dodge == 3)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0, 1, 0.75f);
                wall.transform.localScale = new Vector3(4, 2, 0.15f);
            }
            Physics.SyncTransforms();
            yield return Wait(chase.attackWindup + 0.1f);
            string[] names = { "Leaving melee range", "Dodging behind the committed swing", "Jumping above melee reach", "Raising solid cover during windup" };
            Check(hp.currentHealth == 100, names[dodge] + " avoids damage");
        }

        for (int interrupt = 0; interrupt < 4; interrupt++)
        {
            current = "Melee interruption " + interrupt;
            yield return Reset();
            player = Player(Vector3.forward * 1.5f); player.tag = "Player1";
            hp = player.GetComponent<PlayerHealthControl>();
            enemy = Goblin(Vector3.zero);
            chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
            chase.attackRecovery = 0.15f;
            yield return Windup(chase);
            var status = StatusEffects.Of(enemy.gameObject);
            if (interrupt == 0) status.Stun(0.2f, 0.7f);
            if (interrupt == 1) status.MarkFrozen(0.7f);
            if (interrupt == 2) status.Root(0.7f);
            if (interrupt == 3) AbilityKit.Knockback(enemy.gameObject, Vector3.back * 10, player);
            yield return Wait(0.55f);
            string[] names = { "Stun", "Freeze", "Root", "Knockback" };
            Check(hp.currentHealth == 100, names[interrupt] + " interrupts a pending zombie swing");
            yield return Wait(1.8f);
            Check(hp.currentHealth < 100, "Zombie resumes combat after " + names[interrupt].ToLowerInvariant());
        }

        current = "Clone absorbs a committed attack";
        yield return Reset();
        player = Player(Vector3.forward * 3); player.tag = "Player1";
        hp = player.GetComponent<PlayerHealthControl>();
        enemy = Goblin(Vector3.zero);
        var decoy = new GameObject("MeleeDecoy").AddComponent<WaterCloneDecoy>();
        decoy.transform.position = Vector3.forward;
        decoy.Setup(player, 5, Color.cyan);
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        yield return Windup(chase);
        yield return Wait(chase.attackWindup + 0.1f);
        Check(decoy == null && WaterCloneDecoy.Active.Count == 0 && hp.currentHealth == 100,
            "Zombie consumes a clone without transferring damage to its caster");
        yield return Wait(2.2f);
        Check(hp.currentHealth < 100, "Zombie returns to attacking humans after the decoy pops");

        current = "Spawn grace and cancelled attacks";
        yield return Reset();
        player = Player(Vector3.forward * 1.5f); player.tag = "Player1";
        hp = player.GetComponent<PlayerHealthControl>();
        enemy = Goblin(Vector3.zero);
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        var arrival = enemy.gameObject.AddComponent<GoblinAnimationControl>();
        arrival.spawnEffectDuration = 0.5f;
        yield return Wait(0.3f);
        Check(!chase.enabled && hp.currentHealth == 100, "Spawning zombies cannot immediately attack");
        yield return Windup(chase);
        chase.enabled = false;
        yield return Wait(0.6f);
        Check(hp.currentHealth == 100 && enemy.transform.Find("GoblinAttackWarning") == null,
            "Disabling a zombie cancels its attack and cleans up its warning");
        chase.enabled = true;
        yield return Windup(chase);
        enemy.TakeDamage(10000, player);
        yield return Wait(0.6f);
        Check(enemy == null && hp.currentHealth == 100, "Killing a winding-up zombie prevents its pending hit");

        current = "Defensive abilities against real melee";
        yield return Reset();
        player = Player(Vector3.forward * 1.5f); player.tag = "Player1";
        hp = player.GetComponent<PlayerHealthControl>();
        enemy = Goblin(Vector3.zero);
        var frost = player.AddComponent<FractalshotShieldPassive>();
        frost.Init(player.GetComponent<PlayerMovement3D>().wizard);
        enemy.TakeDamage(100, player);
        float shield = frost.Shield;
        chase = enemy.gameObject.AddComponent<GoblinChaseNav>();
        yield return Windup(chase);
        yield return Wait(chase.attackWindup + 0.1f);
        Check(frost.Shield < shield && hp.currentHealth > 88, "Fractalshot shield absorbs real zombie melee damage");
        Object.Destroy(frost);
        yield return null;
        var thorns = player.AddComponent<ThornhidePassive>();
        thorns.Init(player.GetComponent<PlayerMovement3D>().wizard);
        float monsterHp = enemy.currentHealth;
        yield return Windup(chase);
        yield return Wait(chase.attackWindup + 0.1f);
        Check(enemy.currentHealth < monsterHp, "Thornhide retaliates against the zombie that actually struck");
        hp.invincible = true;
        float humanHp = hp.currentHealth;
        yield return Windup(chase);
        yield return Wait(chase.attackWindup + 0.1f);
        Check(hp.currentHealth == humanHp, "Dash invulnerability blocks a real zombie swing");

        hp.invincible = false;
        enemy.currentHealth = 1;
        arrival = enemy.gameObject.AddComponent<GoblinAnimationControl>();
        arrival.spawnEffectDuration = 0.1f;
        yield return Windup(chase);
        yield return Wait(chase.attackWindup + 0.1f);
        Check(enemy == null && hp.currentHealth == humanHp - 12,
            "Lethal Thornhide retaliation safely cancels the attacker during its damage callback");
    }
}
