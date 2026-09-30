using UnityEngine;
using System.Collections.Generic;

public class LightningBlastDamage : MonoBehaviour
{
    public float blastRadius = 5f;
    public int maxDamage = 34;
    public LayerMask hitLayer;
    public int explosionForce = 10;

    public void TriggerBlast(Vector3 position, GameObject caster)
    {
        // Sets off mines/grenades and hits crystals
        Explosions.AffectWorld(position, blastRadius, maxDamage, caster);

        // Blast logic
        // Players have two colliders (CharacterController + capsule); hit each object once
        var alreadyHit = new HashSet<GameObject>();
        Collider[] affected = Physics.OverlapSphere(position, blastRadius, hitLayer);
        foreach (Collider nearby in affected)
        {
            GameObject victim = nearby.attachedRigidbody ? nearby.attachedRigidbody.gameObject : nearby.gameObject;
            if (!alreadyHit.Add(victim)) continue;
            if (nearby.gameObject == caster)
                continue;

            Transform target = nearby.transform;
            Vector3 direction = (target.position - position).normalized;
            float distance = Vector3.Distance(position, target.position);
            float distancePercent = Mathf.Clamp01(1f - (distance / blastRadius));
            float damageToApply = maxDamage * distancePercent;

            // Line-of-sight check
            RaycastHit[] hits = Physics.RaycastAll(position, (target.position - position).normalized, Vector3.Distance(position, target.position),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // RaycastAll returns hits in no particular order; walk them nearest-first
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool blocked = false;

            foreach (var hit in hits)
            {
                if (hit.transform == target)
                    break;

                var iceWall = hit.transform.GetComponent<IceWallEffect>();
                if (iceWall != null)
                {
                    iceWall.TakeDamage(Mathf.RoundToInt(maxDamage)); // or falloff damage
                    blocked = true;
                    break;
                }

                // Any other obstacle that's not the target
                if (hit.transform != target)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            // Health damage
            var health = nearby.GetComponent<PlayerHealthControl>();
            if (health != null)
                health.TakeDamage(damageToApply, caster);
            
            var goblin = nearby.GetComponent<GoblinHealth>();
            if (goblin != null)
                goblin.TakeDamage(damageToApply*2, caster);
            
            // Blinkstorm's shock pulse stuns players and monsters alike
            if (DamageEvents.IsCombatant(victim))
                StatusEffects.Of(victim).Stun(0.2f, 5f);

            // Direct hit to ice wall
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));
        }
    }

}
