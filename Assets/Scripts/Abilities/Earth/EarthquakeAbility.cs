using System.Collections;
using Magnumancer.Abilities;
using UnityEngine;
using UnityEngine.InputSystem;

public class EarthquakeAbility : MonoBehaviour, IActiveAbility
{
    [Header("Earthquake Settings")]
    public float quakeRadius = 6f;
    public float maxDamage = 100f;
    public float knockbackForce = 25f;
    public LayerMask damageLayers;

    [Header("Effects")]
    public GameObject quakeVFX;
    public AudioClip quakeSFX;
    public AudioSource audioSource;

    [Header("Misc")]
    public bool destroyAfterImpact = false;

    public void Activate(GameObject caster)
    {
        Vector3 origin = caster.transform.position;

        // VFX
        if (quakeVFX != null)
            Instantiate(quakeVFX, origin, Quaternion.identity);

        // SFX
        if (audioSource != null && quakeSFX != null)
            audioSource.PlayOneShot(quakeSFX);

        CameraShake.Shake(0.4f, 1f);


        // AoE hit detection
        Collider[] affected = Physics.OverlapSphere(origin, quakeRadius, damageLayers);
        foreach (Collider nearby in affected)
        {
            Transform target = nearby.transform;
            Vector3 direction = (target.position - origin).normalized;
            float distance = Vector3.Distance(target.position, origin);

            // Line-of-sight check
            if (Physics.Linecast(origin, target.position, out RaycastHit hit))
            {
                // Ice wall block
                IceWallEffect wallBlock = hit.transform.GetComponent<IceWallEffect>();
                if (wallBlock != null && hit.transform != target)
                {
                    wallBlock.TakeDamage(Mathf.RoundToInt(maxDamage));
                    continue;
                }

                if (hit.transform != target)
                    continue;
            }

            // Damage falloff
            float distancePercent = Mathf.Clamp01(1f - (distance / quakeRadius));
            float damageToApply = maxDamage * distancePercent;

            // Apply knockback force
            Rigidbody rb = nearby.attachedRigidbody;
            if (rb != null)
                rb.AddForce(direction * knockbackForce * distancePercent, ForceMode.Impulse);

            // Controller rumble
            var movement = nearby.GetComponent<PlayerMovement3D>();
            if (movement != null && movement.gamepad != null)
            {
                float low = 0.3f * distancePercent;
                float high = 0.8f * distancePercent;
                float duration = 0.4f;
                movement.gamepad.SetMotorSpeeds(low, high);
                StartCoroutine(StopRumble(movement.gamepad, duration));
            }

            // Damage players
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
            float delay = (quakeSFX != null) ? quakeSFX.length : 0f;
            Destroy(gameObject, delay);
        }
    }

    private IEnumerator StopRumble(Gamepad gamepad, float duration)
    {
        yield return new WaitForSeconds(duration);
        gamepad?.SetMotorSpeeds(0, 0);
    }
}
