using UnityEngine;
using UnityEngine.InputSystem;

public class GunOrbitController : MonoBehaviour
{
    [Header("Player & Orbit Settings")]
    public Transform player;
    public float     orbitRadius = 2f;
    public float     fixedHeight = 1.5f;
    public float     tilt        = 0f;

    [Header("Controller")]
    public Gamepad gamepad;

    private Vector3 currentWorldOffset;
    public Vector3 aimDirection = Vector3.forward;  // flat XZ aim
    private bool    _logged;

    void Awake()
    {
        if (player == null)
            player = GetComponentInParent<PlayerMovement3D>()?.transform;

        if (player == null)
            Debug.LogError($"{name}: No player assigned!");
    }

    void Start()
    {
        // initialize offset
        currentWorldOffset = transform.position - player.position;
        currentWorldOffset.y = 0f;
    }

    void Update()
    {
        if (gamepad == null || player == null) return;

        // 1) Read stick & compute a flat XZ direction
        Vector2 stick = gamepad.rightStick.ReadValue();
        if (stick.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(stick.x, stick.y);
            aimDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            currentWorldOffset = aimDirection * orbitRadius;
            if (!_logged)
            {
                Debug.Log($"[{name}] aimDirection = {aimDirection}");
                _logged = true;
            }
        }

        // 2) Position in orbit circle
        Vector3 worldPos = player.position + currentWorldOffset;
        worldPos.y = player.position.y + fixedHeight;
        transform.position = worldPos;

        transform.rotation = Quaternion.LookRotation(-aimDirection, Vector3.up);
        transform.Rotate(Vector3.right, tilt, Space.Self);
    }
}
