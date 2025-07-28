using UnityEngine;
using UnityEngine.InputSystem;

public class GrenadeExplodeOnImpact : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 5f;
    public float explosionForce = 700f;
    public float maxDamage = 100f;
    public LayerMask damageLayers;

    [Header("Audio & VFX")]
    public GameObject explosionEffect;
    public AudioSource audioSource;
    public AudioClip explosionSound;

    [Header("Visibility")]
    public Renderer[] renderersToHide;

    [Header("Behavior")]
    public bool destroyAfterImpact = true;

    private bool hasExploded = false;

    void OnCollisionEnter(Collision collision)
    {
        if (!hasExploded)
        {
            hasExploded = true;
            Explode();
        }
    }

    void Explode()
    {
        // VFX
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // SFX
        if (audioSource != null && explosionSound != null)
            audioSource.PlayOneShot(explosionSound);

        // Hide visuals
        foreach (Renderer r in renderersToHide)
            r.enabled = false;

        // Explosion logic
        Collider[] affected = Physics.OverlapSphere(transform.position, explosionRadius, damageLayers);
        foreach (Collider nearby in affected)
        {
            Transform target = nearby.transform;
            Vector3 direction = (target.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, target.position);

            // Line-of-sight check
            if (Physics.Linecast(transform.position, target.position, out RaycastHit hit))
            {
                // Hit an ice wall that's blocking the target
                IceWallEffect wallBlock = hit.transform.GetComponent<IceWallEffect>();
                if (wallBlock != null && hit.transform != target)
                {
                    wallBlock.TakeDamage(Mathf.RoundToInt(maxDamage));
                    continue; // Blocked target behind ice wall
                }

                // Hit something else that isn't the target
                if (hit.transform != target)
                    continue;
            }

            // Damage falloff
            float distancePercent = Mathf.Clamp01(1f - (distance / explosionRadius));
            float damageToApply = maxDamage * distancePercent;

            // Explosion force
            Rigidbody rb = nearby.attachedRigidbody;
            if (rb != null)
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);

            // Rumble
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

            // Health damage
            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply);

            // Damage ice wall directly if it's the actual target
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));
        }

        if (destroyAfterImpact)
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
