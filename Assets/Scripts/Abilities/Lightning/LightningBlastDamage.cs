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
        // ...and electrifies water zones (Conduct)
        ElementReactions.OnElementArea(position, blastRadius, caster, Element.Lightning);

        // Blast logic
        // Players have two colliders (CharacterController + capsule); hit each object once
        var alreadyHit = new HashSet<GameObject>();
        Collider[] affected = Physics.OverlapSphere(position, blastRadius, hitLayer);
        foreach (Collider nearby in affected)
        {
            var victim = DamageEvents.RootOf(nearby);
            if (!alreadyHit.Add(victim) || victim == caster) continue;
            if (!DamageEvents.IsCombatant(victim))
            {
                nearby.GetComponentInParent<IceWallEffect>()?.TakeDamage(maxDamage);
                continue;
            }
            if (!DamageEvents.IsEnemy(victim, caster) || !DamageEvents.IsAlive(victim)) continue;
            if (!AbilityKit.ClearPath(position + Vector3.up, victim, caster)) continue;

            float distance = Vector3.Distance(position, victim.transform.position);
            float damageToApply = maxDamage * Mathf.Clamp01(1f - distance / blastRadius);
            // Keep Blinkstorm's existing double damage against monsters.
            float multiplier = victim.GetComponent<GoblinHealth>() != null ? 2f : 1f;
            DamageEvents.Deal(victim, damageToApply * multiplier, caster);
            StatusEffects.Of(victim).Stun(0.2f, 5f);
            ElementReactions.AbilityHit(victim, caster, damageToApply);
        }
    }
}
