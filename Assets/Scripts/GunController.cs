using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

public class GunOrbit : MonoBehaviour {
    [Tooltip("Distance from the player's position at which the gun orbits.")]
    public float orbitRadius = 1.5f;
    [Tooltip("Minimum magnitude of input required to update orbit.")]
    public float deadZone = 0.2f;

    // Reference to the InputAction (should output a Vector2 from your right stick)
    public InputAction lookAction;
    [SerializeField]
    public string angleLog;
    [SerializeField]
    public float angleDeg;
    [SerializeField]
    public bool shouldFlip;

    void OnEnable() {
        lookAction.Enable();
    }
    void OnDisable() {
        lookAction.Disable();
    }

    void Update() {
        // Read the right stick input (assumed to be in global space)
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        if (lookInput.magnitude < deadZone)
            return;

        float angleRad = Mathf.Atan2(lookInput.y, lookInput.x);
        angleDeg = angleRad * Mathf.Rad2Deg;

        // Compute the angle from the joystick input (in radians)

        if (angleDeg < 90f || angleDeg > -90f){
            shouldFlip = false;
        } 
        if (angleDeg > 90f || angleDeg < -90f){
            shouldFlip = true;
        }
        // If flip is needed, adjust the angle so the sprite remains visually upright
        Vector3 scale = transform.localScale;
        if (shouldFlip) {
            scale.y = -Mathf.Abs(scale.y);
        } else {
            scale.y = Mathf.Abs(scale.y);
        }
        transform.localScale = scale;

        // Compute the offset in global space (ignoring parent's rotation)
        Vector3 offset = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * orbitRadius;

        // Set the gun's world position as the parent's position plus the offset.
        // This keeps the gun fixed relative to the parent's position regardless of parent's rotation.
        transform.position = transform.parent.position + offset;

        // Optionally, rotate the gun to align with the input
        transform.rotation = Quaternion.Euler(0, 0, angleDeg);
    }
}

