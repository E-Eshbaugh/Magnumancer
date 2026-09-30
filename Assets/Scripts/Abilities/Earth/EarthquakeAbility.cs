using System.Collections.Generic;
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
    public float stunDuration = 1f;
    public float stunSpeedMultiplier = 0.3f;

    [Header("Dodge by jumping")]
    [Tooltip("Warning before the slam: a ring marks the area and a second ring grows to meet it. Jump when they meet.")]
    public float windup = 0.4f;

    [Header("Effects")]
    public GameObject quakeVFX;
    public AudioClip quakeSFX;
    public AudioSource audioSource;

    [Header("Misc")]
    public bool destroyAfterImpact = false;

    public void Activate(GameObject caster)
    {
        StartCoroutine(WindupThenSlam(caster, caster.transform.position));
    }

    // Seismic Judgement is a ground shockwave: airborne players are untouched. The slam
    // knocks back and stuns anyone on the ground (and stunned players can't jump), the
    // aftershocks only chip grounded players. The windup ring makes the timing readable.
    IEnumerator WindupThenSlam(GameObject caster, Vector3 origin)
    {
        if (windup > 0f)
        {
            var movement = caster.GetComponent<PlayerMovement3D>();
            if (movement != null) Rumble.Play(movement.gamepad, 0.2f, 0.3f, windup, fade: false);
            yield return Telegraph(caster, origin);
        }
        if (caster != null) Slam(caster, origin);
    }

    IEnumerator Telegraph(GameObject caster, Vector3 origin)
    {
        var host = new GameObject("QuakeWarning");
        Destroy(host, windup + 0.1f); // never left behind if this ability goes away mid-windup
        Vector3 ground = AbilityKit.Ground(origin + Vector3.up);
        Color color = AbilityKit.Theme(caster);
        var edge = GlowLine.Make(host.transform, "Edge", 48, 0.14f, AbilityKit.Glow());
        var closing = GlowLine.Make(host.transform, "Closing", 48, 0.22f, AbilityKit.Glow());
        edge.loop = closing.loop = true;
        Vector3 center = ground + Vector3.up * 0.08f;
        AbilityKit.Circle(edge, center, quakeRadius);

        float t = 0f;
        while (t < windup)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windup);
            AbilityKit.Circle(closing, center, Mathf.Lerp(0.3f, quakeRadius, k * k));
            GlowLine.SetColor(edge, color, 0.5f + 0.5f * Mathf.PingPong(t * 8f, 1f));
            GlowLine.SetColor(closing, Color.Lerp(color, Color.white, k), 0.4f + 0.6f * k);
            yield return null;
        }
        Destroy(host);
    }

    void Slam(GameObject caster, Vector3 origin)
    {
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
        if (movement != null)
            Rumble.Play(movement.gamepad, 0.6f, 1.0f, 1f);

        // The initial shockwave sets off mines/grenades and cracks crystals
        Explosions.AffectWorld(origin, quakeRadius, damagePerTick, caster);
        ElementReactions.OnElementArea(origin, quakeRadius, caster, Element.Earth);   // puddles churn into mud

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
            // Players have two colliders (CharacterController + capsule); hit each object once
            var alreadyHit = new HashSet<GameObject>();
            Collider[] affected = Physics.OverlapSphere(origin, quakeRadius, damageLayers);
            foreach (Collider nearby in affected)
            {
                GameObject victim = nearby.attachedRigidbody ? nearby.attachedRigidbody.gameObject : nearby.gameObject;
                if (!alreadyHit.Add(victim)) continue;
                GameObject target = nearby.gameObject;
                if (target == caster) continue;

                Vector3 direction = (target.transform.position - origin).normalized;
                float distance = Vector3.Distance(target.transform.position, origin);

                // Ground shockwave: players in the air are untouched (monsters can't jump)
                var mover = target.GetComponentInParent<PlayerMovement3D>();
                if (mover != null && mover.IsAirborne) continue;

                // Line-of-sight check
                if (Physics.Linecast(origin, target.transform.position, out RaycastHit hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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

                // Seismic Judgement: the first shockwave briefly stuns
                if (elapsed == 0f && DamageEvents.IsCombatant(victim))
                {
                    StatusEffects.Of(victim).Stun(stunSpeedMultiplier, stunDuration);
                    // Earth + heavy shove: Shatters the Frozen, Staggers everyone else
                    ElementReactions.AbilityHit(victim, caster, damagePerTick, heavy: true);
                }

                // Damage players
                var health = target.GetComponentInParent<PlayerHealthControl>();
                if (health != null)
                    health.TakeDamage(damagePerTick, caster);

                var goblin = nearby.GetComponent<GoblinHealth>();
                if (goblin != null)
                    goblin.TakeDamage(damagePerTick, caster);

                // Rumble
                if (movementScript != null)
                    Rumble.Play(movementScript.gamepad, 0.2f, 0.6f, 0.25f);

                // Ice wall damage
                var wall = target.GetComponentInParent<IceWallEffect>();
                if (wall != null)
                    wall.TakeDamage(damagePerTick);
            }

            elapsed += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }
    }

}