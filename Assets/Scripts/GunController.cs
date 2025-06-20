using UnityEngine;
using UnityEngine.InputSystem;

public class GunOrbit : MonoBehaviour {
    [Tooltip("Distance from the player's position at which the gun orbits.")]
    public float orbitRadius = 1f;
    [Tooltip("Minimum magnitude of input required to update orbit.")]
    public float deadZone = 0.2f;

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
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        if (lookInput.magnitude < deadZone)
            return;

        float angleRad = Mathf.Atan2(lookInput.y, lookInput.x);
        angleDeg = angleRad * Mathf.Rad2Deg;

        if (angleDeg < 90f || angleDeg > -90f){
            shouldFlip = false;
        } 
        if (angleDeg > 90f || angleDeg < -90f){
            shouldFlip = true;
        }

        Vector3 scale = transform.localScale;
        if (shouldFlip) {
            scale.y = -Mathf.Abs(scale.y);
        } else {
            scale.y = Mathf.Abs(scale.y);
        }
        transform.localScale = scale;

        Vector3 offset = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * orbitRadius;

        transform.position = transform.parent.position + offset;

        transform.rotation = Quaternion.Euler(0, 0, angleDeg);
    }   

}

