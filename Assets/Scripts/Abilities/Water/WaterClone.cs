using System.Collections;
using Magnumancer.Abilities;
using UnityEngine;

/// <summary>
/// Shadow Clone: the wizard bursts into a water bomb and two of them come flying out of
/// the mist in opposite directions, the real one and a clone (same look, gun and health
/// bar) that copies their stick mirrored. The clone pops in a splash when time runs out.
/// </summary>
public class WaterClone : MonoBehaviour, IActiveAbility
{
    public GameObject clonePrefab; // Prefab to instantiate as a clone
    public float cloneDuration = 5f; // Duration before the clone is destroyed
    [Tooltip("How far the wizard and the clone are each thrown out of the splash (opposite ways)")]
    public float scatterDistance = 1.6f;
    public float scatterTime = 0.2f;
    [Tooltip("Optional extra puff layered over the splash")]
    public GameObject smoke;

    public void Activate(GameObject caster)
    {
        if (caster == null) return;
        var mover = caster.GetComponent<PlayerMovement3D>();
        Vector3 center = caster.transform.position;
        Color tint = GlowLine.Brighten(WizardShade.Of(caster));

        // 💦 the water bomb both of them come out of
        WaterCloneBurst.Play(center, tint);
        CameraShake.Shake(0.15f, 0.2f);
        if (smoke != null)
            Instantiate(smoke, center, Quaternion.identity);

        GameObject clone = Instantiate(clonePrefab, center, caster.transform.rotation);
        clone.name = $"{caster.name}_Clone";

        var appearance = clone.GetComponentInChildren<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.makeTransparent = false; // a solid double, not a see-through ghost
            appearance.Setup(mover != null ? mover.wizard : null, WizardShade.IndexOfPlayer(caster));
        }

        var cloneMovement = clone.GetComponent<CloneMovement>();
        if (cloneMovement != null && mover != null)
        {
            cloneMovement.gamepad = mover.gamepad;
            cloneMovement.moveSpeed = mover.currentMoveSpeed * mover.moveSpeedMultiplier;
        }

        var orbit = clone.GetComponentInChildren<CloneGunOrbit>();
        if (orbit != null && mover != null)
        {
            orbit.Setup(mover.gamepad, clone.transform);
        }

        clone.AddComponent<WaterCloneDecoy>().Setup(caster, cloneDuration, tint);
        WizardHealthBar.AddDecoy(caster.GetComponent<PlayerHealthControl>(), clone.transform);

        // both fly out of the splash, opposite ways, so nobody knows which one is real
        var casterBody = caster.GetComponent<CharacterController>();
        var cloneBody = clone.GetComponent<CharacterController>();
        if (casterBody != null && cloneBody != null)
            Physics.IgnoreCollision(casterBody, cloneBody);

        Vector2 r = Random.insideUnitCircle.normalized;
        Vector3 dir = r.sqrMagnitude > 0.5f ? new Vector3(r.x, 0f, r.y) : Vector3.right;
        if (scatterDistance > 0f)
        {
            StartCoroutine(Scatter(casterBody, dir));
            StartCoroutine(Scatter(cloneBody, -dir));
        }
    }

    // Eased shove through the CharacterController, so walls still stop it
    IEnumerator Scatter(CharacterController body, Vector3 dir)
    {
        float t = 0f, moved = 0f;
        while (t < scatterTime && body != null)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / scatterTime);
            float target = scatterDistance * (1f - (1f - f) * (1f - f));
            if (body.enabled) body.Move(dir * (target - moved));
            moved = target;
            yield return null;
        }
    }
}
