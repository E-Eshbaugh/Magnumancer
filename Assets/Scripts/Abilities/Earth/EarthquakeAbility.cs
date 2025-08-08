// === EarthquakeAbility.cs ===
using System.Collections;
using Magnumancer.Abilities;
using UnityEngine;
using UnityEngine.InputSystem;

public class EarthquakeAbility : MonoBehaviour, IActiveAbility
{
    [Header("Earthquake Settings")]
    public float quakeRadius = 6f;
    public float knockbackForce = 25f;
    public LayerMask damageLayers;
    public float tickInterval = 0.25f;
    public int damagePerTick = 25;
    public float totalDuration = 1f;

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

        // Camera shake
        CameraShake.Shake(0.4f, 1.25f);

        // Rumble for the caster
        var movement = caster.GetComponent<PlayerMovement3D>();
        if (movement != null && movement.gamepad != null)
        {
            movement.gamepad.SetMotorSpeeds(0.6f, 1.0f);
            StartCoroutine(StopRumble(movement.gamepad, 1f));
        }

        // Start damage over time
        StartCoroutine(DamageOverTime(caster, origin));

        if (destroyAfterImpact)
        {
            float delay = (quakeSFX != null) ? quakeSFX.length : 0f;
            Destroy(gameObject, delay);
        }
    }

    private IEnumerator DamageOverTime(GameObject caster, Vector3 origin)
    {
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            Collider[] affected = Physics.OverlapSphere(origin, quakeRadius, damageLayers);
            foreach (Collider nearby in affected)
            {
                GameObject target = nearby.gameObject;
                if (target == caster) continue;

                Vector3 direction = (target.transform.position - origin).normalized;
                float distance = Vector3.Distance(target.transform.position, origin);

                // Line-of-sight check
                if (Physics.Linecast(origin, target.transform.position, out RaycastHit hit))
                {
                    IceWallEffect wallBlock = hit.transform.GetComponent<IceWallEffect>();
                    if (wallBlock != null && hit.transform != target.transform)
                    {
                        wallBlock.TakeDamage(damagePerTick);
                        continue;
                    }

                    if (hit.transform != target.transform)
                        continue;
                }

                // Apply knockback (once on first tick only)
                var movementScript = target.GetComponentInParent<PlayerMovement3D>();
                if (elapsed == 0f && movementScript != null)
                    movementScript.ApplyKnockback(direction * knockbackForce);

                // Damage players
                var health = target.GetComponentInParent<PlayerHealthControl>();
                if (health != null)
                    health.TakeDamage(damagePerTick);

                var goblin = nearby.GetComponent<GoblinHealth>();
                if (goblin != null)
                    goblin.TakeDamage(damagePerTick);

                // Rumble
                if (movementScript != null && movementScript.gamepad != null)
                {
                    float low = 0.2f;
                    float high = 0.6f;
                    movementScript.gamepad.SetMotorSpeeds(low, high);
                    StartCoroutine(StopRumble(movementScript.gamepad, 0.25f));
                }

                // Ice wall damage
                var wall = target.GetComponentInParent<IceWallEffect>();
                if (wall != null)
                    wall.TakeDamage(damagePerTick);
            }

            elapsed += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }
    }

    private IEnumerator StopRumble(Gamepad gamepad, float duration)
    {
        yield return new WaitForSeconds(duration);
        gamepad?.SetMotorSpeeds(0, 0);
    }
}