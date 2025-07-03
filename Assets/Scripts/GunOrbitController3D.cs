using UnityEngine;
using UnityEngine.InputSystem;

public class GunOrbitController : MonoBehaviour
{
    public Transform player;
    public float orbitRadius = 2f;
    public float fixedHeight = 1.5f;
    public float tilt = 0f;

    private Vector3 currentWorldOffset;

    void Start()
    {
        // Start with current offset from player
        currentWorldOffset = transform.position - player.position;
        currentWorldOffset.y = 0;
    }

    void Update()
    {
        Vector2 stick = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;

        if (stick.magnitude > 0.1f)
        {
            // Convert stick input to angle
            float angle = Mathf.Atan2(stick.x, stick.y);
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            currentWorldOffset = direction.normalized * orbitRadius;
        }

        // Apply world position (ignoring parent transform)
        Vector3 worldPos = player.position + currentWorldOffset;
        worldPos.y = player.position.y + fixedHeight;
        transform.position = worldPos;

        // Face away from the player
        Vector3 outward = (transform.position - player.position).normalized;
        if (outward != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            transform.Rotate(Vector3.right, tilt);
    }
}
