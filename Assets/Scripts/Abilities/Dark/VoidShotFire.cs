using UnityEngine;
using Magnumancer.Abilities;
using UnityEngine.InputSystem;

public class VoidShotActivator : MonoBehaviour, IActiveAbility
{
    public GameObject voidShotPrefab;
    public float launchForce = 100f;
    public float yOffset = 0;
    public float rumbleLow = 0.2f;
    public float rumbleHigh = 0.6f;
    public float rumbleDuration = 0.25f;

    public void Activate(GameObject caster)
    {
        // Try to find the gun orbit controller on the player
        GunOrbitController orbit = caster.GetComponentInChildren<GunOrbitController>();
        if (orbit == null)
        {
            Debug.LogWarning("[VoidShotActivator] GunOrbitController not found on caster.");
            return;
        }

        Vector3 firePosition = orbit.transform.position + Vector3.up * yOffset;
        Vector3 fireDirection = orbit.aimDirection.normalized;

        // Instantiate the shot at gun position
        GameObject shot = Instantiate(voidShotPrefab, firePosition, Quaternion.LookRotation(fireDirection));
        shot.GetComponent<VoidShotProjectile>().caster = caster;

        // Fire the projectile
        Rigidbody rb = shot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = fireDirection * launchForce;
        }
        else
        {
            Debug.LogWarning("[VoidShotActivator] VoidShot prefab is missing a Rigidbody.");
        }

        // Rumble the controller
        if (orbit.gamepad != null)
        {
            orbit.gamepad.SetMotorSpeeds(rumbleLow, rumbleHigh);
            caster.GetComponent<MonoBehaviour>().StartCoroutine(StopRumbleAfterDelay(orbit.gamepad, rumbleDuration));
        }
    }

    private System.Collections.IEnumerator StopRumbleAfterDelay(Gamepad pad, float delay)
    {
        yield return new WaitForSeconds(delay);
        pad.SetMotorSpeeds(0f, 0f);
    }
}
