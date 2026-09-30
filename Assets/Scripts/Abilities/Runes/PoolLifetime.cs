using UnityEngine;

/// Stops a pool emitting when it's done, then removes it once its particles fade
public class PoolLifetime : MonoBehaviour
{
    float stopAt, fade;
    bool stopped;

    public void Init(float duration, float fadeTime)
    {
        stopAt = Time.time + duration;
        fade = fadeTime;
    }

    void Update()
    {
        if (stopped || Time.time < stopAt) return;
        stopped = true;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); // let what's there fade out
        Destroy(gameObject, fade + 0.2f);
    }
}
