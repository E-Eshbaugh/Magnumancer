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
    public Renderer[] renderersToHide; // Assign any MeshRenderers or SpriteRenderers here

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

        // Physics + Damage
        Collider[] affected = Physics.OverlapSphere(transform.position, explosionRadius, damageLayers);
        foreach (Collider nearby in affected)
        {
            Rigidbody rb = nearby.attachedRigidbody;
            Gamepad gamepad = nearby.GetComponent<PlayerMovement3D>().gamepad;
            if (rb != null)
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);

            float distance = Vector3.Distance(transform.position, nearby.transform.position);
            float distancePercent = Mathf.Clamp01(1f - (distance / explosionRadius));
            float damageToApply = maxDamage * distancePercent;
            float intensity = Mathf.Clamp01(1f - (distance / explosionRadius));
            float low = 0.2f * intensity;
            float high = 0.9f * intensity;
            float duration = 0.3f;

            gamepad.SetMotorSpeeds(low, high);
            StartCoroutine(StopRumble(gamepad, duration));

            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply);
        }

        // Delayed cleanup
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
