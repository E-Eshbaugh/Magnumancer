using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CursedPlayer : MonoBehaviour
{
    [Header("Curse Logic")]
    public bool isCursed = false;
    public GameObject curseExplosionVFX;

    private PlayerHealthControl healthController;
    private GameObject curser; // The Hollow who applied the curse (gets damage credit)
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

    public void ApplyCurse(GameObject caster = null)
    {
        isCursed = true;
        curser = caster;
    }

    public void ApplyDamage(int amount, GameObject attacker = null)
    {
        if (healthController == null) return;

        healthController.TakeDamage(amount, attacker);
        // ⚠️ Do NOT explode here — we’ll handle that only during stock loss.
    }

    /// Called on stock loss and on death: a cursed player bursts once.
    public void OnStockLost()
    {
        if (!isCursed) return;
        isCursed = false;

        // Optional: Hide visuals if you want player to disappear momentarily
        foreach (Renderer r in renderersToHide)
            r.enabled = false;

        Explode(transform.position, gameObject, curser);
    }

    /// <summary>
    /// Void burst centered on center. exclude is the object bursting (not damaged);
    /// attacker gets credit for the damage. Also used for void-marked monsters.
    /// </summary>
    public void Explode(Vector3 center, GameObject exclude, GameObject attacker)
    {
        // VFX / SFX
        if (explosionEffect != null)
            Instantiate(explosionEffect, center, Quaternion.identity);
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, center);

        // Sets off mines/grenades and hits crystals
        Explosions.AffectWorld(center, explosionRadius, maxDamage, exclude);

        // Players have two colliders (CharacterController + capsule); hit each object once
        var alreadyHit = new HashSet<GameObject>();
        Collider[] affected = Physics.OverlapSphere(center, explosionRadius, damageLayers);
        foreach (Collider nearby in affected)
        {
            GameObject victim = DamageEvents.RootOf(nearby);
            if (!alreadyHit.Add(victim)) continue;
            if (victim == exclude) continue; // the burst shouldn't hit whoever is bursting
            Transform target = nearby.transform;
            float distance = Vector3.Distance(center, target.position);

            // Line-of-sight check
            if (Physics.Linecast(center, target.position, out RaycastHit hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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

            // Apply health damage
            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply, attacker);

            var goblin = nearby.GetComponent<GoblinHealth>();
            if (goblin != null)
                goblin.TakeDamage(damageToApply, attacker);

            // Directly damage walls
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));
        }
    }




}
