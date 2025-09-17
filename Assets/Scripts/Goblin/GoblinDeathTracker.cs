using UnityEngine;

public class GoblinDeathTracker : MonoBehaviour
{
    [HideInInspector] public GoblinSpawner spawner;
    [HideInInspector] public GameObject tracked;

    private bool _notified;

    private void OnDestroy()
    {
        // Avoid noise during scene unload or if spawner is gone
        if (_notified || spawner == null) return;

        _notified = true;
        spawner.NotifyEnemyDeath(tracked ? tracked : gameObject);
    }
}

