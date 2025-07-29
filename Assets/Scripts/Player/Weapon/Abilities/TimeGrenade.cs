using UnityEngine;
using UnityEngine.InputSystem;

public class GrenadeExplodeAfterDelay : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 5f;
    public float explosionForce = 700f;
    private float maxDamage = 25f;
    public LayerMask damageLayers;
    public float fuseTime = 3f;

    [Header("Audio & VFX")]
    public GameObject explosionEffect;
    public AudioSource audioSource;
    public AudioClip explosionSound;

    [Header("Visibility")]
    public Renderer[] renderersToHide;

    [Header("Behavior")]
    public bool destroyAfterExplosion = true;

    private bool hasExploded = false;

    void Start()
    {
        Invoke(nameof(Explode), fuseTime);
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // VFX
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // SFX
        if (audioSource != null && explosionSound != null)
            audioSource.PlayOneShot(explosionSound);

        // Hide visuals
        foreach (Renderer r in renderersToHide)
            r.enabled = false;

        // Physics + Damage + Rumble
        Collider[] affected = Physics.OverlapSphere(transform.position, explosionRadius, damageLayers);
        foreach (Collider nearby in affected)
        {
            Transform target = nearby.transform;
            Vector3 direction = (target.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, target.position);

            // Check if explosion is blocked
            if (Physics.Linecast(transform.position, target.position, out RaycastHit hit))
            {
                // Case: Ice wall is blocking line of sight
                var wallBlock = hit.transform.GetComponent<IceWallEffect>();
                if (wallBlock != null && hit.transform != target)
                {
                    // Damage the wall
                    wallBlock.TakeDamage(Mathf.RoundToInt(maxDamage));

                    // Do NOT damage the player or other target behind it
                    continue;
                }

                // Case: something else is blocking (e.g., real wall)
                if (hit.transform != target)
                {
                    continue;
                }
            }

            // ✅ Line of sight is clear — apply damage
            float distancePercent = Mathf.Clamp01(1f - (distance / explosionRadius));
            float damageToApply = maxDamage * distancePercent;

            // Explosion force
            Rigidbody rb = nearby.attachedRigidbody;
            if (rb != null)
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);

            // Controller rumble
            var movement = nearby.GetComponent<PlayerMovement3D>();
            if (movement != null && movement.gamepad != null)
            {
                float intensity = distancePercent;
                float low = 0.2f * intensity;
                float high = 0.9f * intensity;
                float duration = 0.3f;
                movement.gamepad.SetMotorSpeeds(low, high);
                StartCoroutine(StopRumble(movement.gamepad, duration));
            }

            // Damage player or object
            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply);

            // Also damage ice wall directly if it’s the target
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));
        }


        if (destroyAfterExplosion)
        {
            float delay = (explosionSound != null) ? explosionSound.length : 0f;
            Destroy(gameObject, delay);
        }
    }

    private System.Collections.IEnumerator StopRumble(Gamepad pad, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pad != null)
            pad.SetMotorSpeeds(0f, 0f);
    }
}
