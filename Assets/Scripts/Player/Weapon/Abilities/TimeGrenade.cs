using UnityEngine;
using UnityEngine.InputSystem;

public class GrenadeExplodeAfterDelay : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 5f;
    public float explosionForce = 700f;
    public float maxDamage = 100f;
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
            Rigidbody rb = nearby.attachedRigidbody;
            var movement = nearby.GetComponent<PlayerMovement3D>();
            if (rb != null)
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);

            float distance = Vector3.Distance(transform.position, nearby.transform.position);
            float distancePercent = Mathf.Clamp01(1f - (distance / explosionRadius));
            float damageToApply = maxDamage * distancePercent;
            float intensity = distancePercent;
            float low = 0.2f * intensity;
            float high = 0.9f * intensity;
            float duration = 0.3f;

            if (movement != null && movement.gamepad != null)
            {
                movement.gamepad.SetMotorSpeeds(low, high);
                StartCoroutine(StopRumble(movement.gamepad, duration));
            }

            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply);
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
