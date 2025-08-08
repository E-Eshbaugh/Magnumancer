using UnityEngine;

public class GoblinAnimationControl : MonoBehaviour
{
    [Header("VFX")]
    public GameObject spawnVFX;
    public GameObject deathVFX;

    [Header("Optional Delay Before Enabling AI")]
    public float spawnEffectDuration = 1f;

    private bool isDead = false;

    void Start()
    {
        if (spawnVFX)
        {
            Instantiate(spawnVFX, transform.position, Quaternion.identity);
        }

        // Optionally delay enabling behavior (e.g., chasing) until after spawn VFX
        Invoke(nameof(OnSpawnComplete), spawnEffectDuration);
    }

    void OnSpawnComplete()
    {
        GetComponent<GoblinChaseNav>().enabled = true;
    }

    public void OnDeath()
    {
        if (isDead) return;
        isDead = true;

        if (deathVFX)
        {
            Instantiate(deathVFX, transform.position, Quaternion.identity);
        }

        // Optional: play death animation or disable components before destroy
        Destroy(gameObject);
    }
}
