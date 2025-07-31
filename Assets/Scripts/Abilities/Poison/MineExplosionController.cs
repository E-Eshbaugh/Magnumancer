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

    [Header("Audio")]
    public AudioClip explosionClip;

    private bool isArmed = false;
    private bool hasExploded = false;

    private static readonly string[] validPlayerTags = { "Player1", "Player2", "Player3", "Player4" };

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

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 1. Spawn poison cloud
        if (poisonCloudPrefab != null)
        {
            Vector3 pos = spawnPoint ? spawnPoint.position : transform.position;
            Instantiate(poisonCloudPrefab, pos, Quaternion.identity);
        }

        // 2. Camera shake
        CameraShake.Shake(cameraShakeIntensity, cameraShakeDuration);

        // 3. Play explosion sound from temp object
        PlayExplosionSound();

        // 4. Destroy the mine
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
