using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Blightward — Rune III: a low purple cloud rolls out along the ground; every enemy it
/// reaches is infected (burns, and spreads to anyone who gets close).
public class ContagionAbility : MonoBehaviour, IActiveAbility
{
    public float radius = 8f;
    [Tooltip("Seconds for the cloud's front to roll out to full radius")]
    public float travelTime = 0.8f;

    static readonly Color CloudDark = new Color(0.36f, 0.12f, 0.55f);
    static readonly Color CloudLight = new Color(0.66f, 0.38f, 0.95f);

    public void Activate(GameObject caster)
    {
        Vector3 origin = caster.transform.position;
        ForgedRunes.ContagionCast(caster);   // Outbreak: a forged gun spreads it further
        RollingSmokeFx.Spawn(origin, radius, travelTime, CloudDark, CloudLight);

        // infect each enemy as the rolling front reaches them
        foreach (var e in AbilityKit.Enemies(origin, radius, caster))
        {
            float d = Vector3.Distance(origin, e.transform.position);
            StartCoroutine(InfectWhenReached(e, caster, Mathf.Clamp01(d / radius) * travelTime));
        }
    }

    IEnumerator InfectWhenReached(GameObject enemy, GameObject caster, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (enemy != null) Infection.Apply(enemy, caster, CloudLight);
    }
}
