using UnityEngine;

public class GoblinDeathTracker : MonoBehaviour
{
    public GoblinSpawner spawner;

    void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.NotifyEnemyDeath(gameObject);
        }
    }
}
