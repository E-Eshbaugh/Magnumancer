using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GoblinSpawner : MonoBehaviour
{
    [Header("Goblin Prefabs")]
    public GameObject[] baseMonsters;
    public GameObject[] midBossMonsters;
    public GameObject[] bossMonsters;

    [Header("Wave Settings")]
    public int totalRounds = 10;
    public int baseMonsterFactor = 4;
    public int midBossFactor = 1;
    public int bossMonsterFactor = 1;

    [Header("Pacing")]
    [Min(1)] public int openingZombieCount = 8;
    [Min(0f)] public float openingDelay = 3f;
    [Min(0f)] public float waveBreak = 8f;
    [Min(0f)] public float bossWaveBreak = 12f;
    [Min(0f)] public float extraPlayerCountScale = 0.6f;
    [Min(0.1f)] public float openingSpawnInterval = 1.1f;
    [Min(0.1f)] public float fastestSpawnInterval = 0.35f;
    [Min(1)] public int openingAliveCap = 8;
    [Min(1)] public int soloAliveCap = 18;
    [Min(0)] public int extraPlayerAliveCap = 4;

    [Header("Wave Combat Scaling")]
    [Min(1f)] public float openingDamage = 50f;
    [Min(1f)] public float maximumDamage = 70f;
    [Min(0f)] public float damagePerWave = 2.5f;
    [Min(0f)] public float healthGrowthPerWave = 0.14f;
    [Min(1f)] public float maximumHealthMultiplier = 3f;
    [Min(0.1f)] public float openingMoveSpeed = 3.2f;
    [Min(0.1f)] public float maximumMoveSpeed = 4.4f;
    [Min(0f)] public float minimumSpawnDistance = 8f;
    [Min(1f)] public float maximumSpawnDistance = 18f;

    [Header("Kill Loot")]
    [Tooltip("Chance for a regular zombie to drop a PvP pickup. Minibosses and bosses always drop one.")]
    [Range(0f, 1f)] public float zombieDropChance = 0.15f;
    [Tooltip("Seconds before uncollected kill loot disappears.")]
    [Min(1f)] public float lootLifetime = 30f;

    [Header("Map Entrances")]
    [Tooltip("Optional authored entrances. Empty uses child ZombieSpawnPoints, then the legacy random-area fallback.")]
    public ZombieSpawnPoint[] spawnPoints;
    [Tooltip("Keep distant unlocked rooms from sending long walks across the entire map")]
    [Min(1f)] public float maximumEntrancePath = 45f;
    Vector3 startingArea;
    bool hasStartingArea;
    readonly List<ZombieSpawnPoint> entranceCandidates = new();

    [Header("Spawn Area")]
    public Vector3 center = Vector3.zero;
    public Vector3 size = new Vector3(20f, 0f, 20f);

    [Header("UI (optional)")]
    [Tooltip("Assign your LegacyRomanWaveCounter, or leave empty to auto-find one in the scene.")]
    public RomanWaveCounter waveCounter;

    private int currentWave = 0;
    private readonly List<GameObject> currentEnemies = new List<GameObject>();
    private float matchStart;
    private ItemId? lastLoot;

    readonly Queue<SpawnRequest> pending = new();
    NavMeshPath spawnPath;
    float nextSpawnAt, nextWaveAt, nextSpawnWarning;
    int partySize = 1;
    bool waveActive;
    WaveTuning tuning;

    enum EnemyKind { Regular, Miniboss, Boss }
    struct SpawnRequest
    {
        public GameObject prefab;
        public EnemyKind kind;
    }

    public readonly struct WaveTuning
    {
        public readonly int regularCount, minibossCount, bossCount, aliveCap;
        public readonly float spawnInterval, damage, healthMultiplier, moveSpeed;
        public WaveTuning(int regular, int mini, int boss, int cap, float interval, float hit, float hp, float speed)
        {
            regularCount = regular; minibossCount = mini; bossCount = boss; aliveCap = cap;
            spawnInterval = interval; damage = hit; healthMultiplier = hp; moveSpeed = speed;
        }
    }

    public WaveTuning TuningForWave(int wave, int players)
    {
        int step = Mathf.Max(0, wave - 1);
        int extra = Mathf.Clamp(players, 1, 4) - 1;
        float countScale = 1f + extra * extraPlayerCountScale;
        int regular = baseMonsterFactor <= 0 ? 0 : Mathf.CeilToInt((openingZombieCount + step * baseMonsterFactor) * countScale);
        int bosses = wave > 0 && wave % 5 == 0 ? Mathf.Max(0, bossMonsterFactor) : 0;
        int minis = wave > 0 && wave % 3 == 0 && bosses == 0 ? Mathf.Max(0, midBossFactor) * (1 + extra / 2) : 0;
        return new WaveTuning(regular, minis, bosses,
            Mathf.Min(soloAliveCap, openingAliveCap + step * 2) + extra * extraPlayerAliveCap,
            Mathf.Max(fastestSpawnInterval, openingSpawnInterval - step * 0.075f) / (1f + extra * 0.3f),
            Mathf.Min(maximumDamage, openingDamage + step * damagePerWave),
            Mathf.Min(maximumHealthMultiplier, 1f + step * healthGrowthPerWave),
            Mathf.Min(maximumMoveSpeed, openingMoveSpeed + step * 0.15f));
    }

    public int CurrentWave => currentWave;
    public int PendingCount => pending.Count;
    public int AliveCount => currentEnemies.Count;
    public bool WaveActive => waveActive;
    public bool AllWavesComplete => currentWave >= totalRounds && !waveActive && pending.Count == 0;


    private void Awake()
    {
        matchStart = Time.time;
        spawnPath = new NavMeshPath();
        // Auto-find a counter if not assigned
        if (!waveCounter)
        {
#if UNITY_2023_1_OR_NEWER
            waveCounter = FindAnyObjectByType<RomanWaveCounter>(FindObjectsInactive.Include);
#else
            waveCounter = FindObjectOfType<LegacyRomanWaveCounter>();
#endif
        }
    }

    void OnEnable() => DamageEvents.Killed += OnEnemyKilled;
    void OnDisable() => DamageEvents.Killed -= OnEnemyKilled;

    void OnEnemyKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (!DropDirector.Enabled || victim == null) return;
        var health = victim.GetComponent<GoblinHealth>();
        var tracker = victim.GetComponentInParent<GoblinDeathTracker>();
        if (health == null || !health.IsDead || tracker == null || tracker.spawner != this || tracker.lootRolled) return;

        // Claim the roll before spawning effects: chained kills and repeated callbacks
        // must never award the same monster twice. Despawns/unloads do not award loot.
        tracker.lootRolled = true;
        if (!tracker.guaranteedLoot && Random.value >= zombieDropChance) return;

        var item = ItemBook.Roll(Time.time - matchStart, lastLoot);
        lastLoot = item.id;
        var ground = AbilityKit.Ground(position + Vector3.up);
        Pickup.Spawn(item, ground, lootLifetime);
        AbilityKit.Shockwave(ground, 1.8f, item.color, 0.35f);
        ItemAudio.Land(item.rarity >= Rarity.Rare);
    }

    void Start()
    {
        nextWaveAt = Time.time + openingDelay;
        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = GetComponentsInChildren<ZombieSpawnPoint>(true);
    }

    void Update()
    {
        if (GamePause.InputBlocked || !HasStandingPlayer()) return;
        currentEnemies.RemoveAll(enemy => enemy == null);
        if (!waveActive)
        {
            if (!AllWavesComplete && Time.time >= nextWaveAt) StartNextWave();
            return;
        }
        if (pending.Count > 0 && currentEnemies.Count < tuning.aliveCap && Time.time >= nextSpawnAt)
        {
            if (TrySpawn(pending.Peek()))
            {
                pending.Dequeue();
                nextSpawnAt = Time.time + tuning.spawnInterval;
            }
            else
            {
                // Keep the ticket and try again; never spawn on a player or off the NavMesh.
                nextSpawnAt = Time.time + 0.5f;
                if (Time.time >= nextSpawnWarning)
                {
                    nextSpawnWarning = Time.time + 10f;
                    Debug.LogWarning("[Horde] Waiting for a safe, reachable spawn location.");
                }
            }
        }
        CheckWaveComplete();
    }

    static bool HasStandingPlayer()
    {
        foreach (var player in PlayerHealthControl.ActivePlayers)
            if (player.IsStanding) return true;
        return false;
    }

    public void StartNextWave()
    {
        if (waveActive || pending.Count > 0 || currentWave >= totalRounds) return;
        currentWave++;
        // Never make a wave easier just because somebody is down or eliminated.
        partySize = Mathf.Clamp(Mathf.Max(partySize, PlayerHealthControl.ActivePlayers.Count), 1, 4);
        tuning = TuningForWave(currentWave, partySize);
        waveActive = true;
        if (waveCounter) waveCounter.SetWave(currentWave);
        Debug.Log($"Starting Wave {currentWave} ({partySize} players)");

        // Put the elite at the front so its arrival frames the wave; regulars stream in behind it.
        QueueEnemies(midBossMonsters, tuning.minibossCount, EnemyKind.Miniboss);
        var bossPool = HasPrefab(bossMonsters) ? bossMonsters : midBossMonsters;
        QueueEnemies(bossPool, tuning.bossCount, EnemyKind.Boss);
        QueueEnemies(baseMonsters, tuning.regularCount, EnemyKind.Regular);
        nextSpawnAt = Time.time;
        if (pending.Count == 0)
            Debug.LogWarning($"[Horde] Wave {currentWave} has no configured enemy prefabs.");
        CheckWaveComplete();
    }

    static bool HasPrefab(GameObject[] prefabs)
    {
        if (prefabs != null)
            foreach (var prefab in prefabs) if (prefab != null) return true;
        return false;
    }

    void QueueEnemies(GameObject[] prefabs, int count, EnemyKind kind)
    {
        if (!HasPrefab(prefabs) || count <= 0) return;
        var valid = new List<GameObject>();
        foreach (var prefab in prefabs) if (prefab != null) valid.Add(prefab);
        for (int i = 0; i < count; i++)
            pending.Enqueue(new SpawnRequest { prefab = valid[Random.Range(0, valid.Count)], kind = kind });
    }

    bool TrySpawn(SpawnRequest request)
    {
        if (request.prefab == null) return true; // removed prefab cannot hold the wave open forever
        if (!TryGetSpawnPosition(request.prefab, out var spawnPos)) return false;
        var enemy = Instantiate(request.prefab, spawnPos, Quaternion.identity);
        currentEnemies.Add(enemy);
        bool elite = request.kind != EnemyKind.Regular;
        bool boss = request.kind == EnemyKind.Boss;
        var health = enemy.GetComponent<GoblinHealth>();
        if (health != null)
            health.currentHealth = health.maxHealth = Mathf.Max(1f, health.maxHealth * tuning.healthMultiplier * (boss ? 1.75f : 1f));
        var chase = enemy.GetComponent<GoblinChaseNav>();
        if (chase != null)
        {
            chase.attackDamage = Mathf.Round(tuning.damage * (boss ? 1.75f : elite ? 1.4f : 1f));
            chase.attackWindup = elite ? 0.65f : 0.45f;
            chase.attackRecovery = elite ? 1.1f : Mathf.Max(0.65f, 0.9f - (currentWave - 1) * 0.025f);
        }
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed = tuning.moveSpeed * (elite ? 0.75f : Random.Range(0.92f, 1.08f));
        var arrival = enemy.GetComponent<GoblinAnimationControl>();
        if (arrival != null) arrival.spawnEffectDuration = 0.8f;

        var tracker = enemy.GetComponent<GoblinDeathTracker>();
        if (tracker == null) tracker = enemy.AddComponent<GoblinDeathTracker>();
        tracker.spawner = this;
        tracker.tracked = enemy;
        tracker.guaranteedLoot = elite;
        tracker.lootRolled = false;
        return true;
    }

    bool TryGetSpawnPosition(GameObject prefab, out Vector3 position)
    {
        position = default;
        var agent = prefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas
        };
        var players = PlayerHealthControl.ActivePlayers;
        if (players.Count == 0) return false;
        if (!hasStartingArea)
        {
            foreach (var player in players)
            {
                if (!player.IsStanding) continue;
                startingArea = player.transform.position;
                hasStartingArea = true;
                break;
            }
        }
        // An authored network is authoritative: never bypass a locked/unsafe entrance
        // by silently falling back to a random point inside a sealed room.
        if (spawnPoints != null && spawnPoints.Length > 0)
            return TryEntrance(filter, out position);
        float near = Mathf.Max(0f, minimumSpawnDistance);
        float far = Mathf.Max(near + 1f, maximumSpawnDistance);
        for (int attempt = 0; attempt < 32; attempt++)
        {
            var anchor = players[Random.Range(0, players.Count)];
            if (!anchor.IsStanding) continue;
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(near + 1f, far);
            Vector3 candidate = anchor.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            // The configured area also offers alternatives on narrow/irregular maps.
            if (attempt >= 24)
                candidate = center + new Vector3(Random.Range(-size.x * 0.5f, size.x * 0.5f), 0f, Random.Range(-size.z * 0.5f, size.z * 0.5f));
            if (!NavMesh.SamplePosition(candidate, out var nav, 3f, filter)) continue;
            bool safe = true;
            foreach (var player in players)
            {
                var delta = nav.position - player.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < near * near) { safe = false; break; }
            }
            if (!safe || !NavMesh.SamplePosition(anchor.transform.position, out var destination, 4f, filter)) continue;
            if (!NavMesh.CalculatePath(nav.position, destination.position, filter, spawnPath)
                || spawnPath.status != NavMeshPathStatus.PathComplete) continue;
            position = nav.position;
            return true;
        }
        return false;
    }

    bool TryEntrance(NavMeshQueryFilter filter, out Vector3 position)
    {
        position = default;
        if (!hasStartingArea || !NavMesh.SamplePosition(startingArea, out var origin, 3f, filter)) return false;
        entranceCandidates.Clear();
        foreach (var point in spawnPoints)
            if (point != null && point.isActiveAndEnabled && point.GatesOpen)
                entranceCandidates.Add(point);

        // Shuffle equal-age entrances; use the least recently used first. This spreads
        // pressure around the available map instead of repeatedly picking one doorway.
        for (int i = entranceCandidates.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (entranceCandidates[i], entranceCandidates[swap]) = (entranceCandidates[swap], entranceCandidates[i]);
        }
        // Stable insertion sort preserves that shuffled order for unused/equal-age points.
        for (int i = 1; i < entranceCandidates.Count; i++)
        {
            var point = entranceCandidates[i];
            int j = i - 1;
            while (j >= 0 && entranceCandidates[j].LastSpawnTime > point.LastSpawnTime)
            { entranceCandidates[j + 1] = entranceCandidates[j]; j--; }
            entranceCandidates[j + 1] = point;
        }
        foreach (var point in entranceCandidates)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector2 scatter = attempt == 2 ? Vector2.zero : Random.insideUnitCircle * point.scatterRadius;
                Vector3 candidate = point.transform.position + new Vector3(scatter.x, 0f, scatter.y);
                if (!NavMesh.SamplePosition(candidate, out var nav, 1.5f, filter)) continue;
                if (!SafeFromPlayers(nav.position)) continue;
                // Explicit gates decide whether a room is unlocked. Ungated entrances
                // must belong to the starting area. A gated room across a jump/gap can
                // spawn once a standing teammate is on its reachable side.
                bool starter = point.requiredOpenings == null || point.requiredOpenings.Length == 0;
                if (starter && (!NavMesh.CalculatePath(origin.position, nav.position, filter, spawnPath)
                    || spawnPath.status != NavMeshPathStatus.PathComplete)) continue;
                bool inRange = false;
                foreach (var player in PlayerHealthControl.ActivePlayers)
                {
                    if (!player.IsStanding || Vector3.Distance(nav.position, player.transform.position) > maximumEntrancePath) continue;
                    if (!NavMesh.SamplePosition(player.transform.position, out var target, 4f, filter)
                        || !NavMesh.CalculatePath(nav.position, target.position, filter, spawnPath)
                        || spawnPath.status != NavMeshPathStatus.PathComplete) continue;
                    float length = 0f;
                    var corners = spawnPath.corners;
                    for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                    if (length <= maximumEntrancePath) { inRange = true; break; }
                }
                if (!inRange) continue;
                point.MarkUsed();
                position = nav.position;
                return true;
            }
        }
        return false;
    }

    bool SafeFromPlayers(Vector3 position)
    {
        float radius = Mathf.Max(0f, minimumSpawnDistance);
        foreach (var player in PlayerHealthControl.ActivePlayers)
        {
            Vector3 delta = position - player.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < radius * radius) return false;
        }
        return true;
    }

    public void NotifyEnemyDeath(GameObject enemy)
    {
        currentEnemies.Remove(enemy);
        currentEnemies.RemoveAll(item => item == null);
        if (isActiveAndEnabled) CheckWaveComplete();
    }

    void CheckWaveComplete()
    {
        if (!waveActive || pending.Count > 0 || currentEnemies.Count > 0) return;
        waveActive = false;
        nextWaveAt = Time.time + (currentWave % 5 == 0 ? bossWaveBreak : waveBreak);
        Debug.Log(currentWave >= totalRounds ? "All waves complete!" : $"Wave {currentWave} complete — catch your breath.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}
