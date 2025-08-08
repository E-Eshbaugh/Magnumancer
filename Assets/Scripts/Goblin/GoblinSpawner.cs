using System.Collections.Generic;
using UnityEngine;

public class GoblinSpawner : MonoBehaviour
{
    [Header("Goblin Prefabs")]
    public GameObject[] baseMonsters;
    public GameObject[] midBossMonsters;
    public GameObject[] bossMonsters;

    [Header("Wave Settings")]
    public int totalRounds = 10;
    public int baseMonsterFactor = 3;
    public int midBossFactor = 1;
    public int bossMonsterFactor = 1;

    [Header("Spawn Area")]
    public Vector3 center = Vector3.zero;
    public Vector3 size = new Vector3(20f, 0f, 20f);

    private int currentWave = 0;
    private List<GameObject> currentEnemies = new List<GameObject>();

    void Start()
    {
        StartNextWave();
    }

    public void StartNextWave()
    {
        currentWave++;

        if (currentWave > totalRounds)
        {
            Debug.Log("All waves complete!");
            return;
        }

        Debug.Log($"Starting Wave {currentWave}");

        int baseCount = currentWave * baseMonsterFactor;
        int midCount = Mathf.FloorToInt(currentWave / 2f) * midBossFactor;
        int bossCount = Mathf.FloorToInt(currentWave / 4f) * bossMonsterFactor;

        SpawnEnemies(baseMonsters, baseCount);
        SpawnEnemies(midBossMonsters, midCount);
        SpawnEnemies(bossMonsters, bossCount);
    }

    void SpawnEnemies(GameObject[] prefabs, int count)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = GetRandomPointInArea();
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            currentEnemies.Add(enemy);

            // Hook into destruction callback
            GoblinDeathTracker tracker = enemy.AddComponent<GoblinDeathTracker>();
            tracker.spawner = this;
        }
    }

    Vector3 GetRandomPointInArea()
    {
        Vector3 offset = new Vector3(
            Random.Range(-size.x / 2f, size.x / 2f),
            0f,
            Random.Range(-size.z / 2f, size.z / 2f)
        );
        return center + offset;
    }

    public void NotifyEnemyDeath(GameObject enemy)
    {
        currentEnemies.Remove(enemy);

        if (currentEnemies.Count == 0)
        {
            Debug.Log($"Wave {currentWave} complete!");
            Invoke(nameof(StartNextWave), 2f); // optional delay
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}
