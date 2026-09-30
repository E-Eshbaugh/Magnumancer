using UnityEngine;

public class MineExplosionController : MonoBehaviour
{
    [Header("Explosion Settings")]
    public GameObject poisonCloudPrefab;
    public Transform spawnPoint;
    public float detectionRadius = 2.5f;
    public float armTime = 0.5f;
    public float cameraShakeIntensity = 0.3f;
    public float cameraShakeDuration = 0.2f;

    [Tooltip("Other mines/grenades this close get set off too (0 = off: a Viper Nest's three mines land close together and would all go off at once)")]
    public float chainRadius = 0f;

    [Header("Audio")]
    public AudioClip explosionClip;

    [HideInInspector] public GameObject owner; // Blightward who launched it

    private bool isArmed = false;
    private bool hasExploded = false;

    private static readonly string[] validPlayerTags = { "Player1", "Player2", "Player3", "Player4", "Monster" };

    void Start()
    {
        Invoke(nameof(Arm), armTime);
    }

    void Update()
    {
        if (!isArmed || hasExploded) return;

        Collider[] nearby = Physics.OverlapSphere(transform.position, detectionRadius);
        foreach (Collider col in nearby)
        {
            // the Blightward who planted it can walk over their own mines
            if (owner != null && DamageEvents.RootOf(col) == owner) continue;

            foreach (string tag in validPlayerTags)
            {
                if (col.CompareTag(tag))
                {
                    Explode();
                    return;
                }
            }
        }
    }

    void Arm()
    {
        isArmed = true;
    }

    /// Set off by a blast or gunfire (works even before it's armed)
    public void Detonate(float delay)
    {
        if (hasExploded) return;
        if (delay <= 0f) { Explode(); return; }
        CancelInvoke(nameof(Explode));
        Invoke(nameof(Explode), delay);
    }

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        CancelInvoke();

        // 1. Spawn poison cloud
        if (poisonCloudPrefab != null)
        {
            Vector3 pos = spawnPoint ? spawnPoint.position : transform.position;
            var cloud = Instantiate(poisonCloudPrefab, pos, Quaternion.identity);
            if (cloud.TryGetComponent<PoisonCloudHazard>(out var hazard))
            {
                hazard.owner = owner;
                hazard.ownerImmune = true; // your own poison doesn't hurt you
            }
        }

        // 2. Camera shake + controller rumble for anyone close
        CameraShake.Shake(cameraShakeIntensity, cameraShakeDuration);
        Rumble.Blast(transform.position, detectionRadius * 4f, 0.7f);

        // 3. Play explosion sound from temp object
        PlayExplosionSound();

        // 4. Chain into nearby mines/grenades
        Explosions.AffectWorld(transform.position, chainRadius, 0f, gameObject);

        // 5. Destroy the mine
        Destroy(gameObject);
    }

    void PlayExplosionSound()
    {
        if (explosionClip == null) return;

        GameObject audioObj = new GameObject("ExplosionSFX");
        audioObj.transform.position = transform.position;

        AudioSource audioSource = audioObj.AddComponent<AudioSource>();
        audioSource.clip = explosionClip;
        audioSource.spatialBlend = 0f;         // 🧨 FORCE 2D for debug (plays everywhere)
        audioSource.volume = 1f;
        audioSource.Play();

        Destroy(audioObj, explosionClip.length);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
