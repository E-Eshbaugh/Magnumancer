using UnityEngine;

public class GoblinDeathTracker : MonoBehaviour
{
    public GoblinSpawner spawner;

    [Header("Camera Shake On Death")]
    public float shakeIntensity = 0.4f;
    public float shakeDuration = 0.15f;

    void OnDestroy()
    {
        if (spawner != null)
            spawner.NotifyEnemyDeath(gameObject);

        CameraShake.Shake(shakeIntensity, shakeDuration);
    }
}
