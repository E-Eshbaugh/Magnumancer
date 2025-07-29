using UnityEngine;
using UnityEngine.InputSystem;

public class CloneGunOrbit : MonoBehaviour
{
    [Header("Clone Orbit Settings")]
    public Transform cloneTransform;           // Set to the clone root transform
    public float orbitRadius = 2f;
    public float fixedHeight = 1.5f;
    public float tilt = 0f;
    public float orbitSpeedMultiplier = 1f;

    [Header("Controller Input")]
    public Gamepad gamepad;                    // Set to caster's Gamepad

    [Header("Debug")]
    public Vector3 aimDirection = Vector3.forward;

    private float orbitAngle = 0f;

    public void Setup(Gamepad pad, Transform clone)
    {
        gamepad = pad;
        cloneTransform = clone;
    }

    void Update()
    {
        if (gamepad == null || cloneTransform == null)
            return;

        // Read caster’s stick and mirror vertical (Y)
        Vector2 input = gamepad.rightStick.ReadValue();

        if (input.sqrMagnitude > 0.01f)
        {
            aimDirection = new Vector3(input.x, 0f, -input.y).normalized; // Mirror Z
        }

        // Optional: spin orbit continuously if no input
        if (aimDirection.sqrMagnitude <= 0.01f)
        {
            orbitAngle += orbitSpeedMultiplier * Time.deltaTime;
            aimDirection = new Vector3(Mathf.Cos(orbitAngle), 0, Mathf.Sin(orbitAngle));
        }

        // Compute orbit position
        Vector3 offset = Quaternion.Euler(tilt, 0, 0) * aimDirection * orbitRadius;
        Vector3 targetPos = cloneTransform.position + offset;
        targetPos.y = cloneTransform.position.y + fixedHeight;

        transform.position = targetPos;

        // Optional: face outward
        transform.rotation = Quaternion.LookRotation(aimDirection);
    }
}
