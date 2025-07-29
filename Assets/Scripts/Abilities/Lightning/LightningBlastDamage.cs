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
           // Blast logic
        Collider[] affected = Physics.OverlapSphere(position, blastRadius, hitLayer);
        foreach (Collider nearby in affected)
        {
            if (nearby.gameObject == caster)
                continue;

            Transform target = nearby.transform;
            Vector3 direction = (target.position - position).normalized;
            float distance = Vector3.Distance(position, target.position);
            float distancePercent = Mathf.Clamp01(1f - (distance / blastRadius));
            float damageToApply = maxDamage * distancePercent;

            // Line-of-sight check
            RaycastHit[] hits = Physics.RaycastAll(position, (target.position - position).normalized, Vector3.Distance(position, target.position));
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
                health.TakeDamage(damageToApply);
            
            var stun = nearby.GetComponent<StunEffect>();
            if (stun != null)
                stun.ApplyStun(0.2f, 0.2f, 5f); 

            // Direct hit to ice wall
            var wall = nearby.GetComponent<IceWallEffect>();
            if (wall != null)
                wall.TakeDamage(Mathf.RoundToInt(damageToApply));
        }
    }

}
