using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrenadeExplodeOnImpact : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 5f;
    public float explosionForce = 700f;
    private float maxDamage = 25f;
    public LayerMask damageLayers;
    [HideInInspector] public GameObject owner; // player who fired it (damage credit)

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
        Detonate();
    }

    /// Explode now or after `delay` (chain reactions, gunfire)
    public void Detonate(float delay = 0f)
    {
        if (hasExploded) return;
        if (delay > 0f)
        {
            CancelInvoke(nameof(DetonateNow));
            Invoke(nameof(DetonateNow), delay);
            return;
        }
        hasExploded = true;
        Explode();
    }

    void DetonateNow() => Detonate(0f);

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

        // It lingers invisibly while the explosion sound plays; stop it bouncing around
        // and absorbing bullets in the meantime
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;
        if (TryGetComponent<Rigidbody>(out var body))
            body.isKinematic = true;

        // Explosion logic
        // Players have two colliders (CharacterController + capsule); hit each object once
        var alreadyHit = new HashSet<GameObject>();
        Collider[] affected = Physics.OverlapSphere(transform.position, explosionRadius, damageLayers);
        foreach (Collider nearby in affected)
        {
            GameObject victim = DamageEvents.RootOf(nearby);
            if (!Teams.CanHarm(victim, owner)) continue;
            if (!alreadyHit.Add(victim)) continue;
            Transform target = nearby.transform;
            Vector3 direction = (target.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, target.position);

            // Line-of-sight check
            if (Physics.Linecast(transform.position, target.position, out RaycastHit hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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

            // Health damage
            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply, owner);

            // Damage ice wall directly if it's the actual target
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));

            var goblin = nearby.GetComponent<GoblinHealth>();
            if (goblin != null)
            {
                Debug.Log("Blow up Goblin");
                goblin.TakeDamage(damageToApply*2, owner);
            }
        }

        // Mines, other grenades, crystals
        Explosions.AffectWorld(transform.position, explosionRadius, maxDamage, gameObject);
        WeaponSynergy.OnGrenadeExploded(owner, transform.position);

        if (destroyAfterImpact)
        {
            float delay = (explosionSound != null) ? explosionSound.length : 0f;
            Destroy(gameObject, delay);
        }
    }

}
