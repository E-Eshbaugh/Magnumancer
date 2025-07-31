using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CursedPlayer : MonoBehaviour
{
    [Header("Curse Logic")]
    public bool isCursed = false;
    public GameObject curseExplosionVFX;

    private PlayerHealthControl healthController;
    public LayerMask damageLayers;
    public float explosionRadius = 4f;
    public float maxDamage = 25f;
    public float explosionForce = 500f;
    public GameObject explosionEffect;
    public AudioClip explosionSound;
    public AudioSource audioSource; // Assign via inspector or get from this GameObject
    public Renderer[] renderersToHide; // Optional: assign body parts to hide


    void Awake()
    {
        healthController = GetComponent<PlayerHealthControl>();
        if (healthController == null)
        {
            Debug.LogError("[CursedPlayer] PlayerHealthControl not found on player.");
        }
    }

    public void ApplyCurse()
    {
        isCursed = true;
        Debug.Log($"[CursedPlayer] {name} has been cursed!");
    }

    public void ApplyDamage(int amount)
    {
        if (healthController == null) return;

        healthController.TakeDamage(amount);
        // ⚠️ Do NOT explode here — we’ll handle that only during stock loss.
    }

    public void OnStockLost()
    {
        if (!isCursed) return;
        isCursed = false;

        // VFX
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // SFX
        if (audioSource != null && explosionSound != null)
            audioSource.PlayOneShot(explosionSound);

        // Optional: Hide visuals if you want player to disappear momentarily
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
                // Hit an ice wall
                IceWallEffect wallBlock = hit.transform.GetComponent<IceWallEffect>();
                if (wallBlock != null && hit.transform != target)
                {
                    wallBlock.TakeDamage(Mathf.RoundToInt(maxDamage));
                    continue;
                }

                if (hit.transform != target)
                    continue; // Obstructed
            }

            // Damage falloff
            float distancePercent = Mathf.Clamp01(1f - (distance / explosionRadius));
            float damageToApply = maxDamage * distancePercent;

            // Rumble feedback
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

            // Apply health damage
            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply);

            // Directly damage walls
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));
        }

        Debug.Log($"[CursedPlayer] {name} exploded from curse on stock loss!");
    }

    private IEnumerator StopRumble(Gamepad pad, float delay)
    {
        yield return new WaitForSeconds(delay);
        pad.SetMotorSpeeds(0, 0);
    }



}
